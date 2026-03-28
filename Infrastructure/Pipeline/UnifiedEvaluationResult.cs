using System.Collections.Generic;
using PicoGK_Run.Core;
using PicoGK_Run.Geometry;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;
using PicoGK_Run.Physics.Solvers;
namespace PicoGK_Run.Infrastructure.Pipeline;

/// <summary>Single authoritative result of prepare → SI solve → continuity (optional) for one candidate.</summary>
public sealed record UnifiedEvaluationResult(
    PreparedNozzleDesignHandoff Preparation,
    NozzleDesignInputs DrivenDesign,
    NozzleSolvedState Solved,
    SiFlowDiagnostics SiDiagnostics,
    NozzleCriticalRatiosSnapshot CriticalRatios,
    IReadOnlyList<string> HealthMessages,
    NozzlePhysicsStageResult PhysicsStages,
    NozzleDesignResult DesignResult,
    JetState InletState,
    GeometryContinuityReport? GeometryContinuity,
    PhysicsPenaltyBreakdown PhysicsPenalties,
    GeometryPenaltyBreakdown GeometryPenalties,
    ConstraintViolationBreakdown Constraints,
    bool HardReject,
    string? HardRejectReason);
