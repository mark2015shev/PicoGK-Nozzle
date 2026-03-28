using System;
using PicoGK_Run.Core;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Physics.Solvers;

/// <summary>
/// Stage 1: authoritative ṁ from <see cref="SourceInputs.MassFlowKgPerSec"/>; ρ from derived source discharge;
/// |V| = ṁ/(ρ A_inj); yaw/pitch → V_a, V_t. Upstream P₀ is diagnostic (from derived stagnation), not from blind PressureRatio.
/// </summary>
public static class InjectorDischargeSolver
{
    /// <summary>Solve injector state. <paramref name="upstreamTotalPressurePa"/> is derived P₀ (stagnation), not P_amb×PR.</summary>
    public static InjectorDischargeResult Solve(
        SourceInputs source,
        NozzleDesignInputs design,
        double gasDensityKgM3,
        double upstreamTotalPressurePa,
        double chamberReferenceStaticPressurePa,
        double? injectorYawAngleDegOverride = null,
        double? injectorPitchAngleDegOverride = null)
    {
        double yawDeg = injectorYawAngleDegOverride ?? design.InjectorYawAngleDeg;
        double pitchDeg = injectorPitchAngleDegOverride ?? design.InjectorPitchAngleDeg;

        double rho = Math.Max(gasDensityKgM3, 1e-9);
        double aInj = Math.Max(design.TotalInjectorAreaMm2 * 1e-6, 1e-12);
        double pCh = Math.Max(chamberReferenceStaticPressurePa, 1.0);
        double p0 = Math.Max(upstreamTotalPressurePa, pCh + 1.0);
        double dP = Math.Max(p0 - pCh, 0.0);

        double cd = Math.Clamp(ChamberPhysicsCoefficients.InjectorDischargeCoefficient, 0.45, 1.0);
        double mdotOrifice = cd * aInj * Math.Sqrt(2.0 * rho * dP);

        double mdotAuth = source.MassFlowKgPerSec > 1e-12 ? source.MassFlowKgPerSec : mdotOrifice;
        double impliedDp = mdotAuth > 1e-18 && cd * aInj > 1e-18
            ? (mdotAuth / (cd * aInj)) * (mdotAuth / (cd * aInj)) / (2.0 * rho)
            : dP;

        double vCont = mdotAuth / (rho * aInj);

        double vEff = InjectorLossModel.EffectiveJetVelocityMps(vCont, rho, yawDeg);
        var (vt, va) = SwirlMath.ResolveInjectorComponents(vEff, yawDeg, pitchDeg);
        double s = Math.Abs(vt) / Math.Max(Math.Abs(va), 1e-6);

        string notes = mdotAuth > 1e-12 && Math.Abs(mdotOrifice - mdotAuth) / mdotAuth > 0.12
            ? "Authoritative ṁ from source; orifice ṁ(P0−P_ch) differs >12% — refine P_chamber or C_d vs datasheet."
            : "Discharge uses derived-source ρ and P₀ (stagnation); PressureRatio is deprecated and not used in live SI path.";

        return new InjectorDischargeResult
        {
            DischargeCoefficient = cd,
            TotalPressureUpstreamPa = p0,
            ChamberReferenceStaticPressurePa = pCh,
            DrivingPressureDropPa = dP,
            ImpliedDeltaPFromMassFlowPa = impliedDp,
            MassFlowKgS = mdotAuth,
            InjectorAreaM2 = aInj,
            DensityKgM3 = rho,
            VelocityMagnitudeFromContinuityMps = vCont,
            EffectiveVelocityMagnitudeMps = vEff,
            AxialVelocityMps = va,
            TangentialVelocityMps = vt,
            SwirlNumberVtOverVa = s,
            LegacyBlendedDriverVelocityMps = vCont,
            Notes = notes
        };
    }
}
