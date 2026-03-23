using System;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Infrastructure;

/// <summary>Weighted score vs baseline SI evaluation (1-D model, pre-CFD).</summary>
public static class AutotuneScoring
{
    /// <summary>
    /// Higher is better. Balances entrainment, net thrust (with thrust floor vs F0), controlled-vortex quality,
    /// and health penalties — not an axial-ejector-only objective.
    /// </summary>
    public static double ComputeScore(
        FlowTuneEvaluation candidate,
        FlowTuneEvaluation baseline,
        RunConfiguration run)
    {
        double wE = Math.Clamp(run.AutotuneWeightEntrainment, 0.05, 0.92);
        double wT = Math.Clamp(run.AutotuneWeightThrust, 0.05, 0.92);
        double wV = Math.Clamp(run.AutotuneWeightVortexQuality, 0.05, 0.92);
        double sumW = wE + wT + wV;
        wE /= sumW;
        wT /= sumW;
        wV /= sumW;

        double f0 = Math.Max(baseline.SourceOnlyThrustN, 1.0);
        double er0 = Math.Max(baseline.EntrainmentRatio, 0.02);
        double vq0 = Math.Max(baseline.VortexQualityMetric, 0.08);

        double erN = candidate.EntrainmentRatio / er0;
        double thN = candidate.NetThrustN / f0;
        double vqN = candidate.VortexQualityMetric / vq0;

        // Soft floor: steeper than linear when net thrust is far below ~0.88× source-only (normalized to baseline F0).
        const double thrustTarget = 0.88;
        double thrustFloor = thN >= thrustTarget
            ? 1.0
            : Math.Pow(Math.Max(thN, 1e-6) / thrustTarget, 2.4);

        // Harder deficit penalty: strongly disfavor candidates that remain deep below source-only thrust.
        double shortfall = Math.Max(0.0, thrustTarget - thN);
        double thrustDeficitPenalty = wT * (1.2 * shortfall + 2.8 * shortfall * shortfall);

        const double healthPenaltyPerIssue = 0.025;
        return wE * erN
               + wT * thN * thrustFloor
               + wV * vqN
               - thrustDeficitPenalty
               - healthPenaltyPerIssue * candidate.HealthCount;
    }
}
