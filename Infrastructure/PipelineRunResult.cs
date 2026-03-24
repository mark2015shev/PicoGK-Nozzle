using System.Collections.Generic;
using PicoGK_Run.Core;
using PicoGK_Run.Geometry;
using PicoGK_Run.Physics;
using PicoGK_Run.Physics.Solvers;

namespace PicoGK_Run.Infrastructure;

public sealed class PipelineRunResult
{
    public NozzleInput Input { get; }
    public NozzleSolvedState Solved { get; }
    public NozzleGeometryResult Geometry { get; }
    public IReadOnlyList<string> SolverWarnings { get; }

    /// <summary>Compressible SI march diagnostics when the detailed path ran; null if legacy-only.</summary>
    public SiFlowDiagnostics? SiFlow { get; }

    /// <summary>Four critical ratio groups + supporting radii (heuristic design envelope).</summary>
    public NozzleCriticalRatiosSnapshot? CriticalRatios { get; }

    /// <summary>Populated when autotune selected the seed before this run.</summary>
    public AutotuneRunSummary? Autotune { get; }

    /// <summary>Explicit staged physics ledger (swirl-vortex SI path).</summary>
    public NozzlePhysicsStageResult? PhysicsStages { get; }

    /// <summary>Assembly-path continuity checks (mm geometry).</summary>
    public GeometryContinuityReport? GeometryContinuity { get; }

    /// <summary>Wall-clock breakdown when <see cref="Parameters.RunConfiguration.EnablePipelineProfiling"/> was true.</summary>
    public PipelineProfileReport? PerformanceProfile { get; }

    /// <summary>How swirl chamber bore was chosen (user / heuristic synthesis / entrainment-derived).</summary>
    public SwirlChamberSizingModel.SizingDiagnostics? ChamberSizing { get; }

    /// <summary>End-to-end bore diameter trace for this run.</summary>
    public ChamberDiameterAudit? ChamberDiameterAudit { get; }

    public PipelineRunResult(
        NozzleInput input,
        NozzleSolvedState solved,
        NozzleGeometryResult geometry,
        IReadOnlyList<string> solverWarnings,
        SiFlowDiagnostics? siFlow = null,
        NozzleCriticalRatiosSnapshot? criticalRatios = null,
        AutotuneRunSummary? autotune = null,
        NozzlePhysicsStageResult? physicsStages = null,
        GeometryContinuityReport? geometryContinuity = null,
        PipelineProfileReport? performanceProfile = null,
        SwirlChamberSizingModel.SizingDiagnostics? chamberSizing = null,
        ChamberDiameterAudit? chamberDiameterAudit = null)
    {
        Input = input;
        Solved = solved;
        Geometry = geometry;
        SolverWarnings = solverWarnings;
        SiFlow = siFlow;
        CriticalRatios = criticalRatios;
        Autotune = autotune;
        PhysicsStages = physicsStages;
        GeometryContinuity = geometryContinuity;
        PerformanceProfile = performanceProfile;
        ChamberSizing = chamberSizing;
        ChamberDiameterAudit = chamberDiameterAudit;
    }
}
