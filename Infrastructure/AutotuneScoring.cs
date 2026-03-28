using System;
using PicoGK_Run.Infrastructure.Pipeline;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Infrastructure;

/// <summary>Weighted score vs baseline SI evaluation (1-D model, pre-CFD).</summary>
public static class AutotuneScoring
{
    /// <summary>
    /// Higher is better. When <see cref="FlowTuneEvaluation.UnifiedEvaluation"/> is set (normal path), uses
    /// <see cref="UnifiedOptimizerScore"/> so tuning matches the same penalty ledger as the unified solve.
    /// Otherwise falls back to the legacy weighted breakdown.
    /// </summary>
    public static double ComputeScore(
        FlowTuneEvaluation candidate,
        FlowTuneEvaluation baseline,
        RunConfiguration run)
    {
        if (candidate.UnifiedEvaluation != null && baseline.UnifiedEvaluation != null)
            return UnifiedOptimizerScore.Compute(candidate.UnifiedEvaluation, baseline.UnifiedEvaluation, run).Score;

        double wE = Math.Clamp(run.AutotuneWeightEntrainment, 0.03, 0.92);
        double wT = Math.Clamp(run.AutotuneWeightThrust, 0.03, 0.92);
        double wV = Math.Clamp(run.AutotuneWeightVortexQuality, 0.03, 0.92);
        double wP = Math.Clamp(run.AutotuneWeightRadialPressure, 0.03, 0.92);
        double sumPos = wE + wT + wV + wP;
        wE /= sumPos;
        wT /= sumPos;
        wV /= sumPos;
        wP /= sumPos;

        double wB = Math.Clamp(run.AutotuneWeightBreakdownPenalty, 0.0, 0.6);
        double wS = Math.Clamp(run.AutotuneWeightSeparationPenalty, 0.0, 0.6);
        double wL = Math.Clamp(run.AutotuneWeightLossPenalty, 0.0, 0.6);
        double wM = Math.Clamp(run.AutotuneWeightEjectorPenalty, 0.0, 0.6);
        double wAx = Math.Clamp(run.AutotuneWeightLowAxialPenalty, 0.0, 0.6);

        double f0 = Math.Max(baseline.SourceOnlyThrustN, 1.0);
        double er0 = Math.Max(baseline.EntrainmentRatio, 0.02);

        FlowTunePhysicsMetrics mc = candidate.PhysicsMetrics;
        FlowTunePhysicsMetrics mb = baseline.PhysicsMetrics;

        double erN = candidate.EntrainmentRatio / er0;
        double thN = candidate.NetThrustN / f0;

        const double thrustTarget = 0.88;
        double thrustFloor = thN >= thrustTarget
            ? 1.0
            : Math.Pow(Math.Max(thN, 1e-6) / thrustTarget, 2.4);

        double shortfall = Math.Max(0.0, thrustTarget - thN);
        double thrustDeficitPenalty = wT * (1.2 * shortfall + 2.8 * shortfall * shortfall);

        double vqN = mc.VortexQualityComposite / Math.Max(mb.VortexQualityComposite, 0.08);
        double radialN = mc.RadialPressureUsefulNorm / Math.Max(mb.RadialPressureUsefulNorm, 0.10);
        double recN = mc.RecoverableSwirlAtStatorNorm / Math.Max(mb.RecoverableSwirlAtStatorNorm, 0.10);

        double positive = wE * erN
                          + wT * thN * thrustFloor
                          + wV * (0.72 * vqN + 0.28 * recN)
                          + wP * radialN;

        double penalties =
            wB * mc.BreakdownRisk01
            + wS * mc.SeparationRisk01
            + wL * mc.TotalLoss01
            + wM * mc.EjectorStress01
            + wAx * mc.LowAxialMomentum01;

        const double healthPenaltyPerIssue = 0.025;
        return positive - penalties - thrustDeficitPenalty - healthPenaltyPerIssue * candidate.HealthCount;
    }
}
