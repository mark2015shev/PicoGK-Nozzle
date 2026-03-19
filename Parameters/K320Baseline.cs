using PicoGK_Run.Core;

namespace PicoGK_Run.Parameters;

/// <summary>
/// Centralized baseline configuration so Program.cs stays minimal.
/// </summary>
public static class K320Baseline
{
    private const double DefaultSourceAreaMm2 = 3737.4;

    public static SourceInputs CreateSource() => new(
        sourceOutletAreaMm2: DefaultSourceAreaMm2,
        massFlowKgPerSec: 0.80,
        sourceVelocityMps: 520.0,
        pressureRatio: 2.8,
        exhaustTemperatureK: 950.0);

    public static AmbientAir CreateAmbient() => new(
        pressurePa: 101_325.0,
        temperatureK: 288.15,
        densityKgPerM3: 1.225);

    public static NozzleDesignInputs CreateDesign() => new()
    {
        InletDiameterMm = 90.0,
        SwirlChamberDiameterMm = 68.0,
        SwirlChamberLengthMm = 75.0,
        TotalInjectorAreaMm2 = DefaultSourceAreaMm2,
        InjectorCount = 16,
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
        ExportStl = false,
        StlFileName = "nozzle_result.stl",
        ShowInViewer = true
    };
}

