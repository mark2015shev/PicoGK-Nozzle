namespace PicoGK_Run.Parameters;

/// <summary>
/// User-controlled nozzle design intent. No physics inside this type.
/// </summary>
public sealed class NozzleDesignInputs
{
    public double InletDiameterMm { get; init; }
    public double SwirlChamberDiameterMm { get; init; }
    public double SwirlChamberLengthMm { get; init; }

    /// <summary>
    /// 0 = upstream end of swirl chamber (just after inlet), 1 = downstream end (just before expander).
    /// Positions reference injector <b>markers</b> / intended station along the chamber axis.
    /// </summary>
    public double InjectorAxialPositionRatio { get; init; }

    public double TotalInjectorAreaMm2 { get; init; }
    public int InjectorCount { get; init; }
    public double InjectorWidthMm { get; init; }
    public double InjectorHeightMm { get; init; }
    public double InjectorYawAngleDeg { get; init; }
    public double InjectorPitchAngleDeg { get; init; }
    public double InjectorRollAngleDeg { get; init; }
    public double ExpanderLengthMm { get; init; }
    public double ExpanderHalfAngleDeg { get; init; }
    public double ExitDiameterMm { get; init; }
    public double StatorVaneAngleDeg { get; init; }
    public int StatorVaneCount { get; init; }
    public double WallThicknessMm { get; init; }
}
