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

    /// <summary>Decay of angular-momentum content modeled as P₀ loss tied to tangential dynamic head [Pa].</summary>
    public static double SwirlDecayTotalPressureLossPa(double swirlDecayFactor, double swirlDynamicPressurePa)
    {
        double decay = Math.Clamp(swirlDecayFactor, 0.5, 1.0);
        double lossFrac = 1.0 - decay;
        return ChamberPhysicsCoefficients.MarchSwirlDecayDp0OverSwirlDynamicHead * lossFrac * Math.Max(swirlDynamicPressurePa, 0.0);
    }
}
