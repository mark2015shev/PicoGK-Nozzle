using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// Finite control-volume discharge split: two escape resistances fed by bulk chamber P and ρ from the SI march.
/// </summary>
public static class SwirlChamberDualPathDischargeModel
{
    /// <summary>
    /// ṁ = C_d A √(2 ρ ΔP) for ΔP = max(0, P_chamber − P_reference); SI units.
    /// </summary>
    public static double OrificeMassFlowKgS(double cd, double areaM2, double densityKgM3, double deltaPPa)
    {
        if (deltaPPa <= 0 || areaM2 <= 0 || cd <= 0 || densityKgM3 <= 0)
            return 0.0;
        return cd * areaM2 * Math.Sqrt(2.0 * densityKgM3 * deltaPPa);
    }

    public static SwirlChamberDualPathDischargeResult Compute(
        double pChamberBulkPa,
        double rhoChamberKgM3,
        double mdotPrimaryKgS,
        double mdotSecondaryKgS,
        double vAxialContinuityMps,
        double vTangentialMps,
        in SwirlChamberDischargePathSpec spec)
    {
        double mdotIn = Math.Max(mdotPrimaryKgS + mdotSecondaryKgS, 0.0);
        double dPu = pChamberBulkPa - spec.PUpstreamReferencePa;
        double dPd = pChamberBulkPa - spec.PDownstreamReferencePa;

        double mUpR = OrificeMassFlowKgS(spec.CdUpstream, spec.EffectiveUpstreamEscapeAreaM2, rhoChamberKgM3, dPu);
        double mDnR = OrificeMassFlowKgS(spec.CdDownstream, spec.EffectiveDownstreamEscapeAreaM2, rhoChamberKgM3, dPd);

        double sumR = mUpR + mDnR;
        double mUpB;
        double mDnB;
        double fUp;
        double fDn;
        if (sumR > 1e-18 && mdotIn > 1e-18)
        {
            mUpB = mdotIn * (mUpR / sumR);
            mDnB = mdotIn * (mDnR / sumR);
            fUp = mUpB / mdotIn;
            fDn = mDnB / mdotIn;
        }
        else if (mdotIn > 1e-18)
        {
            mUpB = 0.5 * mdotIn;
            mDnB = 0.5 * mdotIn;
            fUp = 0.5;
            fDn = 0.5;
        }
        else
        {
            mUpB = 0.0;
            mDnB = 0.0;
            fUp = 0.0;
            fDn = 0.0;
        }

        double residual = mdotIn - mUpR - mDnR;
        double k = Math.Clamp(spec.VaSplitBlendFactor, 0.0, 0.5);
        double bias = mdotIn > 1e-18 ? k * (mDnB - mUpB) / mdotIn : 0.0;
        double vaW = vAxialContinuityMps * (1.0 + bias);
        vaW = Math.Clamp(vaW, 0.15 * Math.Max(Math.Abs(vAxialContinuityMps), 0.1), 3.5 * Math.Max(Math.Abs(vAxialContinuityMps), 1.0));

        string cls;
        if (fUp > 0.55)
            cls = "UPSTREAM-DOMINANT";
        else if (fDn > 0.55)
            cls = "DOWNSTREAM-DOMINANT";
        else
            cls = "SPLIT";

        return new SwirlChamberDualPathDischargeResult
        {
            MdotPrimaryKgS = mdotPrimaryKgS,
            MdotSecondaryKgS = mdotSecondaryKgS,
            MdotTotalInKgS = mdotIn,
            MdotUpstreamRawKgS = mUpR,
            MdotDownstreamRawKgS = mDnR,
            MdotUpstreamBalancedKgS = mUpB,
            MdotDownstreamBalancedKgS = mDnB,
            FractionUpstream = fUp,
            FractionDownstream = fDn,
            ChamberBulkPressurePa = pChamberBulkPa,
            ChamberBulkDensityKgM3 = rhoChamberKgM3,
            DeltaPUpstreamPa = Math.Max(0.0, dPu),
            DeltaPDownstreamPa = Math.Max(0.0, dPd),
            PUpstreamReferencePa = spec.PUpstreamReferencePa,
            PDownstreamReferencePa = spec.PDownstreamReferencePa,
            EffectiveUpstreamEscapeAreaM2 = spec.EffectiveUpstreamEscapeAreaM2,
            EffectiveDownstreamEscapeAreaM2 = spec.EffectiveDownstreamEscapeAreaM2,
            QuasiSteadyOrificeResidualKgS = residual,
            VAxialContinuityMps = vAxialContinuityMps,
            VAxialDischargeWeightedMps = vaW,
            VTangentialMps = vTangentialMps,
            DirectionalClassification = cls
        };
    }
}
