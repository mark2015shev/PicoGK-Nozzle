using System;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

/// <summary>
/// Resolves one downstream annulus radius and expander build length so expander outlet, stator ID, and exit start match.
/// Default: constant-area recovery exit (no post-stator contraction) unless <see cref="RunConfiguration.EnablePostStatorExitTaper"/>.
/// </summary>
public static class DownstreamGeometryResolver
{
    private const double MinExpanderLengthMm = 0.5;
    private const double MaxLengthFactorVsNominal = 8.0;
    private const double AbsoluteMaxExpanderLengthMm = 600.0;
    private const double TanEpsilon = 1e-9;

    public static DownstreamGeometryTargets Resolve(NozzleDesignInputs d, RunConfiguration? run)
    {
        double chamberD = Math.Max(d.SwirlChamberDiameterMm, 1.0);
        double chamberInnerR = 0.5 * chamberD;
        double nominalL = Math.Max(d.ExpanderLengthMm, MinExpanderLengthMm);
        double halfRad = d.ExpanderHalfAngleDeg * (Math.PI / 180.0);
        double tanA = Math.Tan(halfRad);
        double rDeclaredExit = 0.5 * Math.Max(d.ExitDiameterMm, 1.0);

        double nominalConeR = tanA > TanEpsilon
            ? chamberInnerR + tanA * nominalL
            : chamberInnerR;

        bool taper = run?.EnablePostStatorExitTaper ?? false;
        double lMax = Math.Min(AbsoluteMaxExpanderLengthMm, Math.Max(nominalL * MaxLengthFactorVsNominal, nominalL + 1.0));
        bool lengthClamped = false;
        bool coneInfeasible = false;
        double rRecovery;
        double lEff;
        double rExitEnd;

        if (taper)
        {
            // Mode B: expander follows nominal cone; exit frustum is explicit to declared exit diameter.
            lEff = nominalL;
            rRecovery = nominalConeR;
            rExitEnd = rDeclaredExit;
        }
        else if (tanA <= TanEpsilon)
        {
            lEff = nominalL;
            rRecovery = chamberInnerR;
            rExitEnd = rRecovery;
            if (Math.Abs(rDeclaredExit - rRecovery) > 0.25)
                coneInfeasible = true;
        }
        else
        {
            // Mode A: recovery radius = declared exit; expander length solves cone to hit it exactly when possible.
            rExitEnd = rDeclaredExit;
            rRecovery = rDeclaredExit;
            double lReq = (rRecovery - chamberInnerR) / tanA;

            if (rDeclaredExit + 1e-6 < chamberInnerR)
            {
                coneInfeasible = true;
                lEff = nominalL;
                rRecovery = nominalConeR;
                rExitEnd = rRecovery;
            }
            else if (lReq < MinExpanderLengthMm)
            {
                lEff = MinExpanderLengthMm;
                rRecovery = chamberInnerR + tanA * lEff;
                rExitEnd = rRecovery;
                coneInfeasible = Math.Abs(rRecovery - rDeclaredExit) > 0.05;
            }
            else if (lReq > lMax)
            {
                lEff = lMax;
                lengthClamped = true;
                rRecovery = chamberInnerR + tanA * lEff;
                rExitEnd = rRecovery;
                coneInfeasible = Math.Abs(rRecovery - rDeclaredExit) > 0.05;
            }
            else
            {
                lEff = lReq;
            }
        }

        return new DownstreamGeometryTargets(
            ChamberInnerRadiusMm: chamberInnerR,
            RecoveryAnnulusRadiusMm: rRecovery,
            DeclaredExitInnerRadiusMm: rDeclaredExit,
            ExitEndInnerRadiusMm: rExitEnd,
            NominalExpanderLengthMm: nominalL,
            EffectiveExpanderLengthMm: lEff,
            NominalConeOutletInnerRadiusMm: nominalConeR,
            UsesPostStatorExitTaper: taper,
            ConeCannotReachDeclaredExit: coneInfeasible,
            ExpanderLengthClampedToMax: lengthClamped,
            MaxExpanderLengthUsedMm: lMax);
    }
}
