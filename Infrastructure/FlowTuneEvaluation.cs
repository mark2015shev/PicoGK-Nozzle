using System.Collections.Generic;
using PicoGK_Run.Parameters;
namespace PicoGK_Run.Infrastructure;

/// <summary>
/// One forward pass of the SI pipeline for a candidate <see cref="NozzleDesignInputs"/> (no voxels).
/// <see cref="Score"/> is meaningful only when set by <see cref="AutotuneScoring"/> during search; otherwise 0.
/// </summary>
public sealed class FlowTuneEvaluation
{
    /// <summary>Seed geometry evaluated (before FlowDriven merge is applied inside the solver).</summary>
    public NozzleDesignInputs CandidateDesign { get; init; } = null!;

    /// <summary>Design after SI + <see cref="Geometry.FlowDrivenNozzleBuilder"/> merge — useful for debugging.</summary>
    public NozzleDesignInputs DrivenDesign { get; init; } = null!;

    public double NetThrustN { get; init; }
    public double SourceOnlyThrustN { get; init; }
    public double EntrainmentRatio { get; init; }

    /// <summary>0–1 tuning composite (chamber physics bundle) or legacy vortex metric.</summary>
    public double VortexQualityMetric { get; init; }

    /// <summary>Extended metrics for weighted autotune (breakdown, separation, losses, ejector).</summary>
    public FlowTunePhysicsMetrics PhysicsMetrics { get; init; } = new();

    /// <summary>Composite autotune objective when scored; 0 if not yet scored.</summary>
    public double Score { get; init; }

    public int HealthCount { get; init; }
    public bool HasDesignError { get; init; }
    public IReadOnlyList<string> HealthMessages { get; init; } = System.Array.Empty<string>();

    public double AmbientAirMassFlowKgS { get; init; }
    public double CoreMassFlowKgS { get; init; }
}
