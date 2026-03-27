using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// Explicit mass-weighted axial / tangential momentum mixing (control-volume increments). Pressure-gradient axial impulse is not added here.
/// </summary>
public sealed class MixingSectionSolver
{
    /// <summary>ġ_x = ṁ₁ v_a1 + ṁ₂ v_a2 [kg·m/s²] before dividing by total ṁ.</summary>
    public static double AxialMomentumFluxRateKgMps(
        double primaryMassFlowKgS,
        double primaryAxialVelocityMps,
        double entrainedMassFlowKgS,
        double entrainedAxialVelocityMps) =>
        Math.Max(primaryMassFlowKgS, 0.0) * primaryAxialVelocityMps
        + Math.Max(entrainedMassFlowKgS, 0.0) * entrainedAxialVelocityMps;

    /// <summary>V_a,mix = ġ_x / (ṁ₁ + ṁ₂).</summary>
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
        return AxialMomentumFluxRateKgMps(m1, primaryVelocityMps, m2, ambientVelocityMps) / sum;
    }

    /// <summary>
    /// Mass-flux–weighted V_θ (legacy helper). The compressible chamber march uses explicit angular-momentum flux
    /// in <see cref="FlowMarcher.SolveDetailed"/> instead of this closure.
    /// </summary>
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
