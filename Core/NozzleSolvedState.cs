namespace PicoGK_Run.Core;

/// <summary>
/// Physics-side derived state. Values are solver outputs, not design inputs.
/// </summary>
public sealed class NozzleSolvedState
{
    public double SourceAreaMm2 { get; init; }
    public double TotalInjectorAreaMm2 { get; init; }
    public double InjectorJetVelocityMps { get; init; }
    public double TangentialVelocityComponentMps { get; init; }
    public double AxialVelocityComponentMps { get; init; }
    public double SwirlStrength { get; init; }

    public double AmbientAirMassFlowKgPerSec { get; init; }
    public double EntrainmentRatio { get; init; }
    public double MixedMassFlowKgPerSec { get; init; }
    public double MixedVelocityMps { get; init; }

    public double ExpansionEfficiency { get; init; }
    public double AxialRecoveryEfficiency { get; init; }
    public double ExitVelocityMps { get; init; }

    public double SourceOnlyThrustN { get; init; }
    public double FinalThrustN { get; init; }
    public double ExtraThrustN { get; init; }
    public double ThrustGainRatio { get; init; }
}

