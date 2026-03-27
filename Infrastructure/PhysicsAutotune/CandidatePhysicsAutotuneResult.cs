using PicoGK_Run.Infrastructure;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Infrastructure.PhysicsAutotune;

/// <summary>One SI evaluation in the five-parameter search with printable diagnostics.</summary>
public sealed class CandidatePhysicsAutotuneResult
{
    public CandidateGeometry Geometry { get; init; }
    public NozzleDesignInputs DesignUsed { get; init; } = null!;
    public FlowTuneEvaluation Evaluation { get; init; } = null!;
    public AutoTuneScoreBreakdown ScoreBreakdown { get; init; } = null!;
    public int Stage { get; init; }
}
