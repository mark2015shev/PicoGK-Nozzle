using PicoGK_Run.Infrastructure;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Infrastructure.PhysicsAutotune;

/// <summary>One SI evaluation in the genome-based physics autotune with printable diagnostics.</summary>
public sealed class CandidatePhysicsAutotuneResult
{
    public NozzleGeometryGenome Genome { get; init; } = null!;

    /// <summary>Legacy five-parameter slice (subset of <see cref="Genome"/>).</summary>
    public CandidateGeometry Geometry => new(
        Genome.SwirlChamberDiameterMm,
        Genome.SwirlChamberLengthMm,
        Genome.InletDiameterMm,
        Genome.ExpanderHalfAngleDeg,
        Genome.StatorVaneAngleDeg);

    public NozzleDesignInputs DesignUsed { get; init; } = null!;
    public FlowTuneEvaluation Evaluation { get; init; } = null!;
    public AutoTuneScoreBreakdown ScoreBreakdown { get; init; } = null!;
    public int Stage { get; init; }
}
