using System;
using System.Collections.Generic;
using System.Linq;
using PicoGK_Run.Core;
using PicoGK_Run.Geometry;
using PicoGK_Run.Infrastructure;
using PicoGK_Run.Infrastructure.Pipeline;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Infrastructure.Services;

/// <summary>
/// Authoritative Prepare → Solve → continuity → penalties path for autotune and final run (no voxels).
/// </summary>
internal static class UnifiedPhysicsEvaluationService
{
    public static UnifiedEvaluationResult EvaluateCandidateUnified(
        SourceInputs source,
        NozzleDesignInputs seedDesign,
        RunConfiguration run,
        NozzleEvaluationMode mode)
    {
        PreparedNozzleDesignHandoff prep = DesignPreparationService.PrepareActiveDesignForSolve(source, seedDesign, run);
        SiPathSolveResult path = PhysicsSiPathService.Solve(source, prep.ActiveDesignAfterSynthesis, run);
        bool checkContinuity = mode == NozzleEvaluationMode.FinalDetailed
            ? run.RunGeometryContinuityCheck
            : run.EvaluateGeometryContinuityDuringAutotune;
        GeometryContinuityReport? continuity = GeometryConsistencyService.EvaluateDrivenDesign(path.DrivenDesign, checkContinuity, run);
        return BuildUnifiedEvaluation(prep, path, continuity, run);
    }

    public static UnifiedEvaluationResult BuildUnifiedAfterSolve(
        PreparedNozzleDesignHandoff prep,
        SiPathSolveResult path,
        GeometryContinuityReport? continuity,
        RunConfiguration run) =>
        BuildUnifiedEvaluation(prep, path, continuity, run);

    private static UnifiedEvaluationResult BuildUnifiedEvaluation(
        PreparedNozzleDesignHandoff prep,
        SiPathSolveResult path,
        GeometryContinuityReport? continuity,
        RunConfiguration run)
    {
        SiFlowDiagnostics si = path.SiDiag;
        FlowTunePhysicsMetrics metrics = FlowTunePhysicsMetrics.FromChamber(si.Chamber, si.FinalAxialVelocityMps);
        int designErr = path.HealthMessages.Count(m => m.StartsWith("DESIGN ERROR", StringComparison.Ordinal));
        double mdotTot = path.PhysicsStages.FinalTotalMassFlowKgS;
        PhysicsPenaltyBreakdown phys = PenaltyBreakdownBuilder.BuildPhysics(
            si,
            metrics,
            path.HealthMessages,
            designErr,
            mdotTot);
        DownstreamGeometryTargets downstream = DownstreamGeometryResolver.Resolve(path.DrivenDesign, run);
        GeometryPenaltyBreakdown geom = PenaltyBreakdownBuilder.BuildGeometry(continuity, downstream, run, path.DrivenDesign);
        ConstraintViolationBreakdown cv = PenaltyBreakdownBuilder.BuildConstraints(
            si,
            continuity,
            designErr > 0,
            path.HealthMessages,
            mdotTot,
            downstream,
            run,
            path.DrivenDesign);
        bool hard = designErr > 0 || cv.Reject;
        string? hr = null;
        if (hard)
        {
            var parts = new List<string>();
            if (designErr > 0)
                parts.Add("DESIGN_ERROR");
            foreach (string r in cv.Reasons)
            {
                if (!parts.Contains(r))
                    parts.Add(r);
            }

            hr = string.Join("; ", parts);
        }

        return new UnifiedEvaluationResult(
            prep,
            path.DrivenDesign,
            path.Solved,
            si,
            path.CriticalRatios,
            path.HealthMessages is List<string> hl ? hl : new List<string>(path.HealthMessages),
            path.PhysicsStages,
            path.DesignResult,
            path.InletState,
            continuity,
            phys,
            geom,
            cv,
            hard,
            hr);
    }

    public static FlowTuneEvaluation ToFlowTuneEvaluation(UnifiedEvaluationResult u)
    {
        int hard = u.HealthMessages.Count(m => m.StartsWith("DESIGN ERROR", StringComparison.Ordinal));
        bool hasErr = hard > 0 || u.HardReject;
        return new FlowTuneEvaluation
        {
            CandidateDesign = u.Preparation.ActiveDesignAfterSynthesis,
            DrivenDesign = u.DrivenDesign,
            EntrainmentRatio = u.Solved.EntrainmentRatio,
            NetThrustN = u.SiDiagnostics.NetThrustN,
            SourceOnlyThrustN = u.Solved.SourceOnlyThrustN,
            VortexQualityMetric = u.SiDiagnostics.Chamber?.TuningCompositeQuality
                ?? u.SiDiagnostics.Vortex?.VortexQualityMetric
                ?? 0.0,
            PhysicsMetrics = FlowTunePhysicsMetrics.FromChamber(u.SiDiagnostics.Chamber, u.SiDiagnostics.FinalAxialVelocityMps),
            AmbientAirMassFlowKgS = u.Solved.AmbientAirMassFlowKgPerSec,
            CoreMassFlowKgS = u.Solved.CoreMassFlowKgPerSec,
            HealthCount = u.HealthMessages.Count,
            HasDesignError = hasErr,
            HealthMessages = u.HealthMessages is List<string> list ? list : new List<string>(u.HealthMessages),
            SiDiagnostics = u.SiDiagnostics,
            Score = 0.0,
            UnifiedEvaluation = u,
            PhysicsPenalties = u.PhysicsPenalties,
            GeometryPenalties = u.GeometryPenalties,
            ConstraintBreakdown = u.Constraints,
            TopPenaltySource = u.HardReject ? (u.HardRejectReason ?? "HardReject") : u.PhysicsPenalties.TopSource
        };
    }
}
