using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// Simple momentum mixing between two streams. Future: pressure-loss, heat transfer, compressibility.
/// </summary>
public sealed class MixingSectionSolver
{
    /// <summary>V_a,mix = (ṁ₁ v_a1 + ṁ₂ v_a2) / (ṁ₁ + ṁ₂).</summary>
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

    /// <summary>V_θ,mix = (ṁ₁ v_θ1 + ṁ₂ v_θ2) / (ṁ₁ + ṁ₂).</summary>
    public double ComputeMixedTangentialVelocity(
        double stream1MassFlowKgS,
        double stream1TangentialMps,
        double stream2MassFlowKgS,
        double stream2TangentialMps)
    {
        double m1 = Math.Max(stream1MassFlowKgS, 0.0);
        double m2 = Math.Max(stream2MassFlowKgS, 0.0);
        double sum = m1 + m2;
        if (sum < 1e-18)
            return 0.0;
        return (m1 * stream1TangentialMps + m2 * stream2TangentialMps) / sum;
    }
}
