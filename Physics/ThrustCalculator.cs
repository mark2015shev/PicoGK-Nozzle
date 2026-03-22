using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// One-dimensional thrust accounting for momentum and exit pressure vs ambient.
/// </summary>
public sealed class ThrustCalculator
{
    /// <summary>F = ṁ (V_exit - V₀) + (P_exit - P_amb) A_exit.</summary>
    public double ComputeThrustN(
        double totalMassFlowKgS,
        double exitVelocityMps,
        double freestreamVelocityMps,
        double exitPressurePa,
        double ambientPressurePa,
        double exitAreaM2)
    {
        double mdot = Math.Max(totalMassFlowKgS, 0.0);
        double a = Math.Max(exitAreaM2, 0.0);
        return mdot * (exitVelocityMps - freestreamVelocityMps)
            + (exitPressurePa - ambientPressurePa) * a;
    }
}
