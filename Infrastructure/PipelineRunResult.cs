using System.Collections.Generic;
using PicoGK_Run.Core;
using PicoGK_Run.Geometry;

namespace PicoGK_Run.Infrastructure;

public sealed class PipelineRunResult
{
    public NozzleInput Input { get; }
    public NozzleSolvedState Solved { get; }
    public NozzleGeometryResult Geometry { get; }
    public IReadOnlyList<string> SolverWarnings { get; }

    public PipelineRunResult(
        NozzleInput input,
        NozzleSolvedState solved,
        NozzleGeometryResult geometry,
        IReadOnlyList<string> solverWarnings)
    {
        Input = input;
        Solved = solved;
        Geometry = geometry;
        SolverWarnings = solverWarnings;
    }
}
