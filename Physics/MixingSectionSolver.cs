using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// Simple momentum mixing between two streams. Future: pressure-loss, heat transfer, compressibility.
/// </summary>
public sealed class MixingSectionSolver
{
    /// <summary>V_mix = (ṁ₁ v₁ + ṁ₂ v₂) / (ṁ₁ + ṁ₂).</summary>
    public double ComputeMixedVelocity(
        double primaryMassFlowKgS,
        double primaryVelocityMps,
        double entrainedMassFlowKgS,
        double ambientVelocityMps)
    {
        double m1 = Math.Max(primaryMassFlowKgS, 0.0);
        double m2 = Math.Max(entrainedMassFlowKgS, 0.0);
        double sum = m1 + m2;
        if (sum < 1e-18)
            return 0.0;
        return (m1 * primaryVelocityMps + m2 * ambientVelocityMps) / sum;
    }
}
