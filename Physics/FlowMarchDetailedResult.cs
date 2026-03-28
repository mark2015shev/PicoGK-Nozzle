using System.Collections.Generic;

namespace PicoGK_Run.Physics;

/// <summary>Axial march with compressible entrainment and per-step diagnostics.</summary>
public sealed class FlowMarchDetailedResult
{
    public IReadOnlyList<JetState> FlowStates { get; init; } = System.Array.Empty<JetState>();
    public IReadOnlyList<FlowMarchStepResult> StepResults { get; init; } = System.Array.Empty<FlowMarchStepResult>();

    public IReadOnlyList<FlowStepState> StepPhysicsStates { get; init; } = System.Array.Empty<FlowStepState>();

    /// <summary>Optional per-step consistency warnings from the SI march (validation mode).</summary>
    public IReadOnlyList<string> MarchInvariantWarnings { get; init; } = System.Array.Empty<string>();

    public MarchClosureResult? MarchClosure { get; init; }

    /// <summary>Tangential velocity at end of mixing march (before stator in composition root).</summary>
    public double FinalTangentialVelocityMps { get; init; }

    /// <summary>Axial velocity at end of march (before stator).</summary>
    public double FinalAxialVelocityMps { get; init; }

    /// <summary>Primary-stream tangential speed after last decay step (before mixed Vt recomputation).</summary>
    public double FinalPrimaryTangentialVelocityMps { get; init; }
}
