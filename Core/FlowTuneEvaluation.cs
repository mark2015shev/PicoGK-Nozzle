using PicoGK_Run.Parameters;

namespace PicoGK_Run.Core;

/// <summary>Scalar outcomes from one forward SI evaluation (used by autotune; not CFD).</summary>
public sealed class FlowTuneEvaluation
{
    public double EntrainmentRatio { get; init; }
    public double NetThrustN { get; init; }
    public double SourceOnlyThrustN { get; init; }
    public double AmbientAirMassFlowKgS { get; init; }
    public double CoreMassFlowKgS { get; init; }
    public int HealthIssueCount { get; init; }
    public bool HasDesignErrors { get; init; }
    public NozzleDesignInputs DrivenDesign { get; init; } = null!;
}
