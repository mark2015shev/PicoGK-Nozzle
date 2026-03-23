using System;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Infrastructure;

/// <summary>Weighted score vs baseline SI evaluation (1-D model, pre-CFD).</summary>
public static class AutotuneScoring
{
    /// <summary>
    /// Higher is better. Uses normalized entrainment, normalized net thrust vs source-only thrust,
    /// soft penalty below 0.88×F_source-only, and health warning count penalty.
    /// </summary>
    public static double ComputeScore(
        FlowTuneEvaluation candidate,
        FlowTuneEvaluation baseline,
        RunConfiguration run)
    {
        double wE = Math.Clamp(run.AutotuneWeightEntrainment, 0.05, 0.95);
        double wT = Math.Clamp(run.AutotuneWeightThrust, 0.05, 0.95);
        double sum = wE + wT;
        wE /= sum;
        wT /= sum;

        double f0 = Math.Max(baseline.SourceOnlyThrustN, 1.0);
        double er0 = Math.Max(baseline.EntrainmentRatio, 0.02);

        double erN = candidate.EntrainmentRatio / er0;
        double thN = candidate.NetThrustN / f0;
        double thrustFloor = thN >= 0.88 ? 1.0 : 0.35 + 0.65 * (thN / 0.88);

        const double healthPenaltyPerIssue = 0.025;
        return wE * erN + wT * thN * thrustFloor - healthPenaltyPerIssue * candidate.HealthCount;
    }
}
