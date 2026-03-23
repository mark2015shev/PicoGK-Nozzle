using PicoGK_Run.Core;

namespace PicoGK_Run.Parameters;

/// <summary>
/// K320 G4+ style <b>source boundary</b> and example nozzle design. All thrust-related source
/// scalars are set here; verify against your KingTech datasheet before relying on numbers.
/// </summary>
public static class K320Baseline
{
    /// <summary>Authoritative turbine exit / source flow area [mm²].</summary>
    public const double DefaultSourceAreaMm2 = 3737.4;

    // --- Typical published K-320G4+ class figures (retail listings / manufacturer pages — verify) ---
    // Mass flow ~0.53 kg/s, pressure ratio ~3.6, exhaust speed often quoted ~2173 km/h ≈ 603 m/s.
    private const double K320G4_MassFlowKgPerSec = 0.53;
    private const double K320G4_ExhaustSpeedMps = 2173.0 * (1000.0 / 3600.0);
    private const double K320G4_PressureRatio = 3.6;

    /// <summary>
    /// Used for a <b>heuristic</b> ρ_core in the continuity <b>blend</b> with V_core×(A_source/A_inj).
    /// Omit or set null to fall back to <see cref="SourceInputs.AmbientDensityKgPerM3"/>.
    /// </summary>
    private const double K320G4_ExhaustTemperatureK = 1003.15;

    private const double IsaSeaLevelPressurePa = 101_325.0;
    private const double IsaSeaLevelTemperatureK = 288.15;
    private const double IsaSeaLevelDensityKgPerM3 = 1.225;

    public static SourceInputs CreateSource() => new(
        sourceOutletAreaMm2: DefaultSourceAreaMm2,
        massFlowKgPerSec: K320G4_MassFlowKgPerSec,
        sourceVelocityMps: K320G4_ExhaustSpeedMps,
        pressureRatio: K320G4_PressureRatio,
        ambientPressurePa: IsaSeaLevelPressurePa,
        ambientTemperatureK: IsaSeaLevelTemperatureK,
        ambientDensityKgPerM3: IsaSeaLevelDensityKgPerM3,
        exhaustTemperatureK: K320G4_ExhaustTemperatureK);

    public static NozzleDesignInputs CreateDesign() => new()
    {
        InletDiameterMm = 90.0,
        SwirlChamberDiameterMm = 68.0,
        SwirlChamberLengthMm = 75.0,
        InjectorAxialPositionRatio = 0.5,
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
        ShowInViewer = true,
        UsePhysicsInformedGeometry = false,
        UseAutotune = false
    };

    /// <summary>Same as <see cref="CreateRun"/> but enables synthesis so chamber/expander/stator/inlet follow first-order rules from the source.</summary>
    public static RunConfiguration CreateRunPhysicsInformed() => new()
    {
        VoxelSizeMM = 0.3f,
        ShowInViewer = true,
        UsePhysicsInformedGeometry = true,
        UseAutotune = false
    };

    /// <summary>
    /// Runs <c>NozzleDesignAutotune</c> before the full pipeline (many fast SI-only evaluations, then one voxel build).
    /// Tune <see cref="RunConfiguration.AutotuneTrials"/> / weights for your CFD correlation loop.
    /// </summary>
    public static RunConfiguration CreateRunWithAutotune(int trials = 200) => new()
    {
        VoxelSizeMM = 0.3f,
        ShowInViewer = true,
        UsePhysicsInformedGeometry = false,
        UseAutotune = true,
        AutotuneTrials = trials,
        AutotuneWeightEntrainment = 0.52,
        AutotuneWeightThrust = 0.48,
        AutotuneRandomSeed = 20260213,
        AutotuneUseSynthesisBaseline = true
    };

    /// <summary>Convenience: K320 source + hand template + autotune run config.</summary>
    public static NozzleInput CreateInputWithAutotune(int trials = 200) => new(
        CreateSource(),
        CreateDesign(),
        CreateRunWithAutotune(trials));
}
