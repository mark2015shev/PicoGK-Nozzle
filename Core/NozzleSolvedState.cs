namespace PicoGK_Run.Core;

/// <summary>
/// Physics solver outputs only.
/// </summary>
public sealed class NozzleSolvedState
{
    public double CoreMassFlowKgPerSec { get; init; }
    public double SourceAreaMm2 { get; init; }
    public double TotalInjectorAreaMm2 { get; init; }

    /// <summary>Continuity: mdot / (rho_core * A_injector).</summary>
    public double InjectorJetVelocityMps { get; init; }

    /// <summary>Heuristic core gas density used for injector continuity [kg/m3].</summary>
    public double CoreGasDensityKgPerM3 { get; init; }

    public double TangentialVelocityComponentMps { get; init; }
    public double AxialVelocityComponentMps { get; init; }

    /// <summary>
    /// Tangential-to-axial speed ratio of the injector jet direction (from yaw/pitch).
    /// Interpretable as a dimensionless injector swirl directive; not a CFD swirl number.
    /// </summary>
    public double InjectorSwirlNumber { get; init; }

    /// <summary>
    /// Same metric after a heuristic decay along the chamber (used only for stator model).
    /// </summary>
    public double ChamberSwirlNumberForStator { get; init; }

    public PressureLossBreakdown PressureLoss { get; init; }

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
