using System.Collections.Generic;

namespace PicoGK_Run.Physics;

/// <summary>Aggregated first-order SI march outputs for reporting (not CFD).</summary>
public sealed class SiFlowDiagnostics
{
    public IReadOnlyList<FlowMarchStepResult> MarchSteps { get; init; } = System.Array.Empty<FlowMarchStepResult>();

    public double MinInletLocalStaticPressurePa { get; init; }
    public double MaxInletMach { get; init; }
    public bool AnyEntrainmentStepChoked { get; init; }

    /// <summary>Sum of per-step requested entrainment increments [kg/s] (Σ Δṁ_req).</summary>
    public double SumRequestedEntrainmentIncrementsKgS { get; init; }

    /// <summary>Sum of per-step actual entrainment increments [kg/s].</summary>
    public double SumActualEntrainmentIncrementsKgS { get; init; }

    /// <summary>Σ (requested − actual) per step [kg/s].</summary>
    public double EntrainmentShortfallSumKgS { get; init; }

    public double ExpanderAxialPressureForceN { get; init; }
    public double InletAxialPressureForceN { get; init; }
    public double StatorRecoveredPressureRisePa { get; init; }
    /// <summary>Mixed tangential speed after stator recovery step (first-order).</summary>
    public double FinalTangentialVelocityMps { get; init; }

    public double FinalAxialVelocityMps { get; init; }

    public double MomentumThrustN { get; init; }
    public double PressureThrustN { get; init; }
    public double NetThrustN { get; init; }

    /// <summary>Chamber vortex / radial pressure / swirl budget bookkeeping (first-order, not CFD).</summary>
    public VortexFlowDiagnostics? Vortex { get; init; }
}
