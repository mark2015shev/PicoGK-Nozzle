using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// SI thrust authority: momentum + exit pressure + inlet / expander pressure CV terms (same budget as composition root).
/// </summary>
public static class ThrustCalculator
{
    /// <summary>ṁ(V_exit − V_∞) [N].</summary>
    public static double MomentumThrustN(
        double massFlowExitKgS,
        double exitAxialVelocityMps,
        double ambientVelocityMps)
    {
        return Math.Max(massFlowExitKgS, 0.0) * (exitAxialVelocityMps - ambientVelocityMps);
    }

    /// <summary>(P_exit − P_∞) A_exit [N].</summary>
    public static double ExitPlanePressureThrustN(
        double exitStaticPressurePa,
        double ambientPressurePa,
        double exitAreaM2)
    {
        return (exitStaticPressurePa - ambientPressurePa) * Math.Max(exitAreaM2, 0.0);
    }

    public static double PressureThrustTotalN(
        double exitStaticPressurePa,
        double ambientPressurePa,
        double exitAreaM2,
        double inletAxialPressureForceN,
        double expanderAxialPressureForceN)
    {
        return ExitPlanePressureThrustN(exitStaticPressurePa, ambientPressurePa, exitAreaM2)
               + inletAxialPressureForceN
               + expanderAxialPressureForceN;
    }

    public static (double MomentumN, double PressureN, double NetN) NetThrustBreakdown(
        double massFlowExitKgS,
        double exitAxialVelocityMps,
        double ambientVelocityMps,
        double exitStaticPressurePa,
        double ambientPressurePa,
        double exitAreaM2,
        double inletAxialPressureForceN,
        double expanderAxialPressureForceN)
    {
        double fM = MomentumThrustN(massFlowExitKgS, exitAxialVelocityMps, ambientVelocityMps);
        double fP = PressureThrustTotalN(
            exitStaticPressurePa,
            ambientPressurePa,
            exitAreaM2,
            inletAxialPressureForceN,
            expanderAxialPressureForceN);
        return (fM, fP, fM + fP);
    }

    /// <summary>Stream thrust F = ṁ(V−V₀) + (P−P₀)A — used when no full CV diagnostics exist.</summary>
    public static double ComputeStreamThrustN(
        double totalMassFlowKgS,
        double exitVelocityMps,
        double freestreamVelocityMps,
        double exitPressurePa,
        double ambientPressurePa,
        double exitAreaM2)
    {
        return NetThrustBreakdown(
            totalMassFlowKgS,
            exitVelocityMps,
            freestreamVelocityMps,
            exitPressurePa,
            ambientPressurePa,
            exitAreaM2,
            0.0,
            0.0).NetN;
    }
}
