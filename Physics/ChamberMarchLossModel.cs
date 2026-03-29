using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// Named first-order total-pressure (P₀) decrements for the SI chamber march — not CFD.
/// Applied after mass-weighted P₀ mixing; static (P,T) are derived from P₀, h₀, and |V|.
/// </summary>
public static class ChamberMarchLossModel
{
    /// <summary>Irreversibility from incremental secondary mass entrainment [Pa].</summary>
    public static double MixingTotalPressureLossPa(double deltaMassFlowKgS, double massFlowNewKgS, double dynamicPressurePa)
    {
        if (massFlowNewKgS < 1e-18)
            return 0.0;
        double f = Math.Clamp(deltaMassFlowKgS / massFlowNewKgS, 0.0, 0.65);
        return f * ChamberPhysicsCoefficients.MarchMixingDp0OverDynamicHead * Math.Max(dynamicPressurePa, 0.0);
    }

    /// <summary>Wall / hydraulic friction proxy on P₀ [Pa].</summary>
    public static double WallTotalPressureLossPa(double deltaXM, double hydraulicDiameterM, double dynamicPressurePa)
    {
        double d = Math.Max(hydraulicDiameterM, 1e-5);
        double dx = Math.Max(deltaXM, 0.0);
        double q = Math.Max(dynamicPressurePa, 0.0);
        return ChamberPhysicsCoefficients.MarchWallDp0OverDynamicHeadPerDxOverD * (dx / d) * q;
    }

    /// <summary>Legacy: decay factor on Ġ_θ mapped to P₀ loss (reference path only).</summary>
    public static double SwirlDecayTotalPressureLossPa(double swirlDecayFactor, double swirlDynamicPressurePa)
    {
        double decay = Math.Clamp(swirlDecayFactor, 0.5, 1.0);
        double lossFrac = 1.0 - decay;
        return ChamberPhysicsCoefficients.MarchSwirlDecayDp0OverSwirlDynamicHead * lossFrac * Math.Max(swirlDynamicPressurePa, 0.0);
    }

    /// <summary>
    /// Lumped angular-momentum flux loss [kg·m²/s²] per march step: wall friction proxy, mixing with secondary, entrainment dilution.
    /// Ġ_θ_new = Ġ_θ − wall − mix − dilution (same sign as Ġ_θ).
    /// </summary>
    public static void AngularMomentumFluxLossesStep(
        double angularMomentumFluxKgM2PerS2,
        double deltaXM,
        double hydraulicDiameterM,
        double deltaMassFlowKgS,
        double massFlowOldKgS,
        out double wallLossMagnitudeKgM2PerS2,
        out double mixingLossMagnitudeKgM2PerS2,
        out double entrainmentDilutionLossMagnitudeKgM2PerS2)
    {
        double lAbs = Math.Abs(angularMomentumFluxKgM2PerS2);
        double mOld = Math.Max(massFlowOldKgS, 1e-18);
        double dm = Math.Max(deltaMassFlowKgS, 0.0);
        double d = Math.Max(hydraulicDiameterM, 1e-5);
        double dx = Math.Max(deltaXM, 0.0);
        double dxOverD = dx / d;

        wallLossMagnitudeKgM2PerS2 = ChamberPhysicsCoefficients.AngularMomentumWallLossPerDxOverD * dxOverD * lAbs;
        mixingLossMagnitudeKgM2PerS2 = ChamberPhysicsCoefficients.AngularMomentumMixingLossPerDeltaMOverM * (dm / mOld) * lAbs;
        entrainmentDilutionLossMagnitudeKgM2PerS2 =
            ChamberPhysicsCoefficients.AngularMomentumEntrainmentDilutionPerDeltaMOverM * (dm / mOld) * lAbs;
    }

    /// <summary>P₀ loss from fractional reduction of |Ġ_θ| this step, tied to tangential dynamic head [Pa].</summary>
    public static double AngularMomentumTotalPressureLossPa(
        double angularMomentumFluxOldKgM2PerS2,
        double angularMomentumFluxNewKgM2PerS2,
        double swirlDynamicPressurePa)
    {
        double l0 = Math.Abs(angularMomentumFluxOldKgM2PerS2);
        if (l0 < 1e-24)
            return 0.0;
        double l1 = Math.Abs(angularMomentumFluxNewKgM2PerS2);
        double lossFrac = Math.Clamp(1.0 - l1 / l0, 0.0, 0.95);
        return ChamberPhysicsCoefficients.MarchSwirlDecayDp0OverSwirlDynamicHead * lossFrac * Math.Max(swirlDynamicPressurePa, 0.0);
    }
}
