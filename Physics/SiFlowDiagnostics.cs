using System.Collections.Generic;
using PicoGK_Run.Physics.Reports;

namespace PicoGK_Run.Physics;

/// <summary>Aggregated first-order SI march outputs for reporting (not CFD).</summary>
public sealed class SiFlowDiagnostics
{
    /// <summary>Injector-plane flux swirl S = Ġθ/(R·ġx) used in governing correlations and stage ledger.</summary>
    public double InjectorPlaneFluxSwirlNumber { get; init; }

    public IReadOnlyList<FlowMarchStepResult> MarchSteps { get; init; } = System.Array.Empty<FlowMarchStepResult>();

    public IReadOnlyList<FlowStepState> PhysicsStepStates { get; init; } = System.Array.Empty<FlowStepState>();

    public MarchClosureResult? MarchPhysicsClosure { get; init; }

    public double MinInletLocalStaticPressurePa { get; init; }
    public double MaxInletMach { get; init; }
    public bool AnyEntrainmentStepChoked { get; init; }

    /// <summary>Sum of per-step requested entrainment increments [kg/s] (Σ Δṁ_req).</summary>
    public double SumRequestedEntrainmentIncrementsKgS { get; init; }

    /// <summary>Sum of per-step actual entrainment increments [kg/s].</summary>
    public double SumActualEntrainmentIncrementsKgS { get; init; }

    /// <summary>Σ (requested − actual) per step [kg/s].</summary>
    public double EntrainmentShortfallSumKgS { get; init; }

    /// <summary>diagnostic_force_only — expander ΔP×A_proj heuristic; not added to net thrust CV.</summary>
    public double ExpanderAxialPressureForceN { get; init; }

    /// <summary>diagnostic_force_only — Σ per-step inlet capture (P_amb−P_local)×A; not added to net thrust CV.</summary>
    public double InletAxialPressureForceN { get; init; }
    public double StatorRecoveredPressureRisePa { get; init; }
    /// <summary>Mixed tangential speed after stator recovery step (first-order).</summary>
    public double FinalTangentialVelocityMps { get; init; }

    public double FinalAxialVelocityMps { get; init; }

    /// <summary>ṁ_exit (V_exit − V_∞) from the single exit control volume [N].</summary>
    public double MomentumThrustN { get; init; }

    /// <summary>(P_exit − P_amb) A_exit only [N] — not inlet/expander wall diagnostics.</summary>
    public double PressureThrustN { get; init; }

    /// <summary>Single authoritative thrust: MomentumThrustN + PressureThrustN [N].</summary>
    public double NetThrustN { get; init; }

    /// <summary>Sanity scale: ṁ_core |V_a| at injector (order-of-magnitude vs net thrust).</summary>
    public double CoreMomentumEstimateN { get; init; }

    public bool ThrustControlVolumeIsValid { get; init; }

    public string? ThrustControlVolumeInvalidReason { get; init; }

    /// <summary>Optional warning when thrust is reported but exit P or |V| looks suspicious.</summary>
    public string? ThrustControlVolumeSoftWarning { get; init; }

    public double ThrustCvMdotExitKgS { get; init; }
    public double ThrustCvVExitMps { get; init; }
    public double ThrustCvPExitPa { get; init; }
    public double ThrustCvPAmbientPa { get; init; }
    public double ThrustCvAExitM2 { get; init; }

    /// <summary>Always 0 — inlet/expander/capture/wall diagnostics are not added to net thrust.</summary>
    public double ThrustOtherForcesAddedToNetN { get; init; }

    /// <summary>True when near-injector or march-inlet static exceeded 10 bar abs (hard assertion).</summary>
    public bool ChamberPressureHardAssertionTripped { get; init; }

    /// <summary>Chamber vortex / radial pressure / swirl budget bookkeeping (first-order, not CFD).</summary>
    public VortexFlowDiagnostics? Vortex { get; init; }

    /// <summary>Extended radial / structure / diffuser / ejector / loss diagnostics — not CFD.</summary>
    public ChamberFirstOrderPhysics? Chamber { get; init; }

    /// <summary>Raw vs effective coupling audit for SI thrust path (first-order).</summary>
    public SiVortexCouplingDiagnostics? Coupling { get; init; }

    /// <summary>Hub-based stator span/blockage and swirl audit — not CFD.</summary>
    public HubStatorFlowDiagnostics? HubStator { get; init; }

    /// <summary>Upstream P0 vs injector q, first-order exit static, chamber/core statics — not CFD.</summary>
    public InjectorPressureVelocityDiagnostics? InjectorPressureVelocity { get; init; }

    /// <summary>Chamber SI march duct areas, capture, Ce sample, and validation warnings — not CFD.</summary>
    public SwirlChamberMarchDiagnostics? ChamberMarch { get; init; }

    /// <summary>Swirl-vortex chamber openness, blockage, entrainment, and plain-language geometry warnings.</summary>
    public SwirlChamberHealthReport? SwirlChamberHealth { get; init; }
}
