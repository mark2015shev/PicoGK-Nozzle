namespace PicoGK_Run.Parameters;

/// <summary>
/// Parametric nozzle design controls. Pure geometry intent.
/// </summary>
public sealed class NozzleDesignInputs
{
    public double InletDiameterMm { get; init; }
    public int InjectorCount { get; init; }
    public double TotalInjectorAreaMm2 { get; init; }
    public double InjectorWidthMm { get; init; }
    public double InjectorHeightMm { get; init; }
    public double InjectorYawAngleDeg { get; init; }
    public double InjectorPitchAngleDeg { get; init; }
    public double InjectorRollAngleDeg { get; init; }
    public double SwirlChamberDiameterMm { get; init; }
    public double SwirlChamberLengthMm { get; init; }
    public double ExpanderLengthMm { get; init; }
    public double ExpanderHalfAngleDeg { get; init; }
    public double ExitDiameterMm { get; init; }
    public double StatorVaneAngleDeg { get; init; }
    public int StatorVaneCount { get; init; }
}

