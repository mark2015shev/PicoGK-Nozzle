using System.Collections.Generic;
using PicoGK_Run.Core;
using PicoGK_Run.Geometry;
using PicoGK_Run.Physics;

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

    public PipelineRunResult(
        NozzleInput input,
        NozzleSolvedState solved,
        NozzleGeometryResult geometry,
        IReadOnlyList<string> solverWarnings,
        SiFlowDiagnostics? siFlow = null,
        NozzleCriticalRatiosSnapshot? criticalRatios = null)
    {
        Input = input;
        Solved = solved;
        Geometry = geometry;
        SolverWarnings = solverWarnings;
        SiFlow = siFlow;
        CriticalRatios = criticalRatios;
    }
}
