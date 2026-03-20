namespace PicoGK_Run.Core;

/// <summary>
/// Physics solver outputs only.
/// </summary>
public sealed class NozzleSolvedState
{
    public double CoreMassFlowKgPerSec { get; init; }
    public double SourceAreaMm2 { get; init; }
    public double TotalInjectorAreaMm2 { get; init; }

    /// <summary>
    /// Scalar speed used for yaw/pitch decomposition (magnitude of modeled primary jet at injectors).
    /// </summary>
    public double InjectorJetVelocityMps { get; init; }

    /// <summary>HEURISTIC: V_core × (A_source/A_inj) — drives jet magnitude; not CFD.</summary>
    public double InjectorJetVelocityAreaDriverMps { get; init; }

    /// <summary>mdot/(ρ_core×A_inj) — continuity cross-check / light blend only.</summary>
    public double InjectorJetVelocityContinuityCheckMps { get; init; }

    /// <summary>Heuristic core gas density (ideal gas when T_exhaust set); used for continuity check blend.</summary>
    public double CoreGasDensityKgPerM3 { get; init; }

    public double TangentialVelocityComponentMps { get; init; }
    public double AxialVelocityComponentMps { get; init; }

    /// <summary>
    /// Tangential-to-axial speed ratio of the injection vector (|Vt|/|Va|).
    /// Not a CFD swirl number.
    /// </summary>
    public double InjectorSwirlNumber { get; init; }

    /// <summary>Chamber-decayed swirl metric for stator heuristic only.</summary>
    public double ChamberSwirlNumberForStator { get; init; }

    public PressureLossBreakdown PressureLoss { get; init; }

    /// <summary>Which named loss fraction is largest (area / swirl_dissipation / short_mixing_length).</summary>
    public string DominantPressureLossContribution { get; init; } = "";

    public double AmbientAirMassFlowKgPerSec { get; init; }
    public double EntrainmentRatio { get; init; }
    public double MixedMassFlowKgPerSec { get; init; }
    public double MixedVelocityMps { get; init; }

    public double ExpansionEfficiency { get; init; }
    public double AxialRecoveryEfficiency { get; init; }
    public double ExitVelocityMps { get; init; }

    /// <summary>First-order: mdot_core * V_core (pressure thrust omitted).</summary>
    public double SourceOnlyThrustN { get; init; }

    public double FinalThrustN { get; init; }
    public double ExtraThrustN { get; init; }
    public double ThrustGainRatio { get; init; }
}
