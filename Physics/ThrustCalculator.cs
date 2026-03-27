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
        return SanitizeBreakdown(fM, fP);
    }

    /// <summary>Same CV as <see cref="NetThrustBreakdown"/> with clamped pressures and finite guards on all inputs.</summary>
    public static (double MomentumN, double PressureN, double NetN) NetThrustBreakdownSanitized(
        double massFlowExitKgS,
        double exitAxialVelocityMps,
        double ambientVelocityMps,
        double exitStaticPressurePa,
        double ambientPressurePa,
        double exitAreaM2,
        double inletAxialPressureForceN,
        double expanderAxialPressureForceN)
    {
        double md = ClampFiniteNonNegative(massFlowExitKgS);
        double ve = ClampFinite(exitAxialVelocityMps, 0.0);
        double vinf = ClampFinite(ambientVelocityMps, 0.0);
        double pe = SiPressureGuards.ClampStaticPressurePa(exitStaticPressurePa);
        double p0 = SiPressureGuards.ClampStaticPressurePa(ambientPressurePa);
        double ae = ClampFiniteNonNegative(exitAreaM2);
        double fin = FiniteClampSigned(inletAxialPressureForceN);
        double fex = FiniteClampSigned(expanderAxialPressureForceN);
        double fM = MomentumThrustN(md, ve, vinf);
        double fP = PressureThrustTotalN(pe, p0, ae, fin, fex);
        return SanitizeBreakdown(fM, fP);
    }

    private static (double MomentumN, double PressureN, double NetN) SanitizeBreakdown(double fM, double fP)
    {
        const double cap = 5e7;
        fM = double.IsFinite(fM) ? Math.Clamp(fM, -cap, cap) : 0.0;
        fP = double.IsFinite(fP) ? Math.Clamp(fP, -cap, cap) : 0.0;
        double sum = fM + fP;
        double net = double.IsFinite(sum) ? Math.Clamp(sum, -cap, cap) : 0.0;
        return (fM, fP, net);
    }

    private static double ClampFinite(double x, double fallback) =>
        double.IsFinite(x) ? x : fallback;

    private static double ClampFiniteNonNegative(double x) =>
        double.IsFinite(x) ? Math.Max(x, 0.0) : 0.0;

    private static double FiniteClampSigned(double x)
    {
        const double cap = 5e7;
        return double.IsFinite(x) ? Math.Clamp(x, -cap, cap) : 0.0;
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
