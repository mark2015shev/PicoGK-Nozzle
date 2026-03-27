using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// Single control-volume net thrust: F = ṁ(V_exit − V_∞) + (P_exit − P_∞)A_exit.
/// Inlet capture and expander wall forces are not part of this CV — report them elsewhere as diagnostics only.
/// </summary>
public static class ThrustCalculator
{
    /// <summary>Result of the authoritative thrust calculation with validation.</summary>
    public readonly record struct ThrustControlVolumeResult(
        double MomentumN,
        double ExitPlanePressureN,
        double NetN,
        bool IsValid,
        string? InvalidReason,
        string? SoftWarning);

    /// <summary>ṁ(V_exit − V_∞) [N].</summary>
    public static double MomentumThrustN(
        double massFlowExitKgS,
        double exitAxialVelocityMps,
        double ambientVelocityMps)
    {
        return Math.Max(massFlowExitKgS, 0.0) * (exitAxialVelocityMps - ambientVelocityMps);
    }

    /// <summary>(P_exit − P_∞) A_exit [N] — exit plane only.</summary>
    public static double ExitPlanePressureThrustN(
        double exitStaticPressurePa,
        double ambientPressurePa,
        double exitAreaM2)
    {
        return (exitStaticPressurePa - ambientPressurePa) * Math.Max(exitAreaM2, 0.0);
    }

    /// <summary>
    /// Authoritative net thrust [N] from one CV. Does not add inlet suction or expander wall forces.
    /// </summary>
    public static ThrustControlVolumeResult ComputeControlVolumeThrustSanitized(
        double massFlowExitKgS,
        double exitAxialVelocityMps,
        double ambientVelocityMps,
        double exitStaticPressurePa,
        double ambientPressurePa,
        double exitAreaM2)
    {
        if (!double.IsFinite(massFlowExitKgS) || !double.IsFinite(exitAxialVelocityMps)
            || !double.IsFinite(ambientVelocityMps) || !double.IsFinite(exitStaticPressurePa)
            || !double.IsFinite(ambientPressurePa) || !double.IsFinite(exitAreaM2))
        {
            return new ThrustControlVolumeResult(0.0, 0.0, 0.0, false, "Non-finite input (ṁ, V, P, or A).", null);
        }

        if (massFlowExitKgS <= 0.0)
        {
            return new ThrustControlVolumeResult(0.0, 0.0, 0.0, false, "Exit mass flow ≤ 0.", null);
        }

        if (exitAreaM2 < 0.0)
        {
            return new ThrustControlVolumeResult(0.0, 0.0, 0.0, false, "Negative exit area.", null);
        }

        double md = massFlowExitKgS;
        double ve = exitAxialVelocityMps;
        double vinf = ambientVelocityMps;
        double pe = SiPressureGuards.ClampStaticPressurePa(exitStaticPressurePa);
        double p0 = SiPressureGuards.ClampStaticPressurePa(ambientPressurePa);
        double ae = Math.Max(exitAreaM2, 0.0);

        string? softWarn = null;
        if (pe < 0.4 * p0 || pe > 2.8 * p0)
            softWarn = $"Exit static P ({pe:F0} Pa) is far from ambient ({p0:F0} Pa) — check SI state.";

        double vAbs = Math.Abs(ve);
        if (vAbs > 2200.0)
            softWarn = (softWarn ?? "") + (softWarn != null ? " " : "") + $"|V_exit|={vAbs:F0} m/s is unusually high.";

        double fM = MomentumThrustN(md, ve, vinf);
        double fP = ExitPlanePressureThrustN(pe, p0, ae);
        var (net, ok) = SanitizeNet(fM, fP);
        if (!ok)
            return new ThrustControlVolumeResult(0.0, 0.0, 0.0, false, "Thrust components non-finite after sanitize.", null);

        return new ThrustControlVolumeResult(fM, fP, net, true, null, softWarn);
    }

    private static (double NetN, bool Ok) SanitizeNet(double fM, double fP)
    {
        const double cap = 5e7;
        if (!double.IsFinite(fM) || !double.IsFinite(fP))
            return (0.0, false);
        fM = Math.Clamp(fM, -cap, cap);
        fP = Math.Clamp(fP, -cap, cap);
        double net = fM + fP;
        if (!double.IsFinite(net))
            return (0.0, false);
        net = Math.Clamp(net, -cap, cap);
        return (net, true);
    }

    /// <summary>Stream thrust when only exit-plane CV data exist (same equation as <see cref="ComputeControlVolumeThrustSanitized"/>).</summary>
    public static double ComputeStreamThrustN(
        double totalMassFlowKgS,
        double exitVelocityMps,
        double freestreamVelocityMps,
        double exitPressurePa,
        double ambientPressurePa,
        double exitAreaM2)
    {
        ThrustControlVolumeResult r = ComputeControlVolumeThrustSanitized(
            totalMassFlowKgS,
            exitVelocityMps,
            freestreamVelocityMps,
            exitPressurePa,
            ambientPressurePa,
            exitAreaM2);
        return r.IsValid ? r.NetN : 0.0;
    }
}
