using System;
using PicoGK_Run.Infrastructure;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Infrastructure.Pipeline;

/// <summary>
/// Scalar objective: weighted positive physics terms minus structured penalties.
/// score = thrust_w * T̂ + er_w * ER̂ + recovery_w * R̂ − Σ physics − Σ geometry − reject_gate
/// </summary>
public static class UnifiedOptimizerScore
{
    public sealed record ScoreResult(double Score, double PositivePart, double PenaltyPart, string TopPenaltySource);

    public static ScoreResult Compute(
        UnifiedEvaluationResult candidate,
        UnifiedEvaluationResult baseline,
        RunConfiguration run)
    {
        if (candidate.HardReject)
        {
            return new ScoreResult(
                -1e9,
                0,
                1e9,
                candidate.HardRejectReason ?? "HardReject");
        }

        var si = candidate.SiDiagnostics;
        var si0 = baseline.SiDiagnostics;
        double f0 = Math.Max(Math.Abs(si0?.NetThrustN ?? 1.0), 1.0);
        double thrustN = Math.Max(si?.NetThrustN ?? 0.0, 0.0);
        double tHat = thrustN / f0;

        double er0 = Math.Max(baseline.Solved.EntrainmentRatio, 0.02);
        double erHat = Math.Max(candidate.Solved.EntrainmentRatio, 0.0) / er0;

        double dp0 = Math.Max(si0?.StatorRecoveredPressureRisePa ?? 400.0, 400.0);
        double dp = si?.StatorRecoveredPressureRisePa ?? 0.0;
        double rHat = Math.Clamp(dp / dp0, 0.2, 2.0);

        double wT = Math.Clamp(run.AutotuneWeightThrust, 0.05, 0.9);
        double wE = Math.Clamp(run.AutotuneWeightEntrainment, 0.05, 0.9);
        double wR = Math.Clamp(run.AutotuneWeightVortexQuality * 0.35, 0.02, 0.45);
        double sum = wT + wE + wR;
        wT /= sum;
        wE /= sum;
        wR /= sum;

        double positive = wT * tHat + wE * Math.Min(erHat, 2.2) + wR * rHat;

        double phys = candidate.PhysicsPenalties.Sum;
        double geom = candidate.GeometryPenalties.Sum;

        FlowTunePhysicsMetrics mc = FlowTunePhysicsMetrics.FromChamber(si?.Chamber, si?.FinalAxialVelocityMps ?? 0.0);
        double lossPen = Math.Clamp(run.AutotuneWeightLossPenalty, 0, 0.5) * mc.TotalLoss01;

        double penaltyTotal = phys + geom + lossPen;

        string top = candidate.GeometryPenalties.Sum >= candidate.PhysicsPenalties.Sum * 0.85 && candidate.GeometryPenalties.Sum > 0.05
            ? "Geometry:" + candidate.GeometryPenalties.TopSource
            : "Physics:" + candidate.PhysicsPenalties.TopSource;

        double score = positive - penaltyTotal;
        if (candidate.Constraints.Reject)
            score -= 2.5;

        return new ScoreResult(score, positive, penaltyTotal, top);
    }
}
