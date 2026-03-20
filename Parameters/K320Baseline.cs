using PicoGK_Run.Core;

namespace PicoGK_Run.Parameters;

/// <summary>
/// Default K320 G4–style source boundary and example nozzle design. Adjust here, not in Program.cs.
/// </summary>
public static class K320Baseline
{
    private const double DefaultSourceAreaMm2 = 3737.4;

    public static SourceInputs CreateSource() => new(
        sourceOutletAreaMm2: DefaultSourceAreaMm2,
        massFlowKgPerSec: 0.53,
        sourceVelocityMps: 603.6,
        pressureRatio: 3.6,
        ambientPressurePa: 101_325.0,
        ambientTemperatureK: 288.15,
        ambientDensityKgPerM3: 1.225,
        exhaustTemperatureK: 1003.15);

    public static NozzleDesignInputs CreateDesign() => new()
    {
        InletDiameterMm = 90.0,
        SwirlChamberDiameterMm = 68.0,
        SwirlChamberLengthMm = 75.0,
        TotalInjectorAreaMm2 = DefaultSourceAreaMm2,
        InjectorCount = 16,
        InjectorWidthMm = 10.0,
        InjectorHeightMm = DefaultSourceAreaMm2 / (16.0 * 10.0),
        InjectorYawAngleDeg = 80.0,
        InjectorPitchAngleDeg = 10.0,
        InjectorRollAngleDeg = 0.0,
        ExpanderLengthMm = 120.0,
        ExpanderHalfAngleDeg = 9.0,
        ExitDiameterMm = 110.0,
        StatorVaneAngleDeg = 28.0,
        StatorVaneCount = 12,
        WallThicknessMm = 3.0
    };

    public static RunConfiguration CreateRun() => new()
    {
        VoxelSizeMM = 0.3f,
        ShowInViewer = true
    };
}
