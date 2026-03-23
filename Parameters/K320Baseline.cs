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
        InletDiameterMm = 96.0,
        // Bore must exceed A_inj (~3737 mm²) with annulus margin — 68 mm bore made A_inj/A_ch > 1 (choked).
        SwirlChamberDiameterMm = 82.0,
        // Longer chamber for vortex development before expander (hand template; autotune may still vary when enabled).
        SwirlChamberLengthMm = 80.0,
        // Mid-chamber injection gives axial runout before expander (90° tangential case).
        InjectorAxialPositionRatio = 0.74,
        TotalInjectorAreaMm2 = DefaultSourceAreaMm2,
        InjectorCount = 16,
        InjectorWidthMm = 10.0,
        InjectorHeightMm = DefaultSourceAreaMm2 / (16.0 * 10.0),
        // 90° yaw = purely tangential jet in the cylindrical frame (see SwirlMath); 0° pitch = no inward radial tilt.
        InjectorYawAngleDeg = 90.0,
        InjectorPitchAngleDeg = 0.0,
        InjectorRollAngleDeg = 0.0,
        ExpanderLengthMm = 120.0,
        // ~7° full-angle class diffuser unless synthesis/autotune overrides.
        ExpanderHalfAngleDeg = 7.0,
        ExitDiameterMm = 110.0,
        StatorVaneAngleDeg = 28.0,
        StatorVaneCount = 12,
        StatorHubDiameterMm = 22.0,
        StatorAxialLengthMm = 26.0,
        StatorBladeChordMm = 7.0,
        WallThicknessMm = 3.0
    };

    public static RunConfiguration CreateRun() => new()
    {
        VoxelSizeMM = 0.3f,
        ShowInViewer = true,
        UsePhysicsInformedGeometry = false,
        UseAutotune = false
    };

    /// <summary>
    /// Fast SI-only presets (no viewer) — e.g. <see cref="PicoGK_Run.Infrastructure.ValidationSweepRunner.RunDefaultK320Validation"/>.
    /// </summary>
    public static RunConfiguration CreateValidationRun() => new()
    {
        VoxelSizeMM = 0.3f,
        ShowInViewer = false,
        UsePhysicsInformedGeometry = false,
        UseAutotune = false,
        EnablePipelineProfiling = false
    };

    /// <summary>Same as <see cref="CreateRun"/> but enables synthesis so chamber/expander/stator/inlet follow first-order rules from the source.</summary>
    public static RunConfiguration CreateRunPhysicsInformed() => new()
    {
        VoxelSizeMM = 0.3f,
        ShowInViewer = true,
        UsePhysicsInformedGeometry = true,
        UseDerivedSwirlChamberDiameter = true,
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
        AutotuneWeightEntrainment = 0.26,
        AutotuneWeightThrust = 0.34,
        AutotuneWeightVortexQuality = 0.18,
        AutotuneWeightRadialPressure = 0.12,
        AutotuneWeightBreakdownPenalty = 0.14,
        AutotuneWeightSeparationPenalty = 0.10,
        AutotuneWeightLossPenalty = 0.10,
        AutotuneWeightEjectorPenalty = 0.08,
        AutotuneWeightLowAxialPenalty = 0.06,
        AutotuneRandomSeed = 20260213,
        AutotuneUseSynthesisBaseline = true,
        UseDerivedSwirlChamberDiameter = true,
        // Compact swirl: cap axial chamber + tight length scale (diameter from ER when derived; else diameter scale knobs).
        AutotuneSwirlChamberLengthMaxMm = 92.0,
        AutotuneSwirlChamberLengthScaleMin = 0.80,
        AutotuneSwirlChamberLengthScaleMax = 1.04
    };

    /// <summary>Convenience: <see cref="CreateSource"/>, <see cref="CreateDesign"/>, and <see cref="CreateRunWithAutotune"/> (viewer on for the final geometry pass).</summary>
    public static NozzleInput CreateInputWithAutotune(int trials = 160) => new(
        CreateSource(),
        CreateDesign(),
        CreateRunWithAutotune(trials));

    /// <summary>
    /// Three-phase autotune (broad → diverse seeds → polish); same scoring as single-stage. Default stage trials: 120 / 96 / 48.
    /// </summary>
    public static RunConfiguration CreateRunWithCoarseToFineAutotune(
        int stage1 = 120,
        int stage2 = 96,
        int stage3 = 48) =>
        new()
        {
            VoxelSizeMM = 0.3f,
            ShowInViewer = true,
            UsePhysicsInformedGeometry = false,
            UseAutotune = true,
            AutotuneStrategy = AutotuneStrategy.CoarseToFine,
            AutotuneStage1Trials = stage1,
            AutotuneStage2Trials = stage2,
            AutotuneStage3Trials = stage3,
            AutotuneTopSeedCountStage1 = 4,
            AutotuneTopSeedCountStage2 = 2,
            AutotuneWeightEntrainment = 0.26,
            AutotuneWeightThrust = 0.34,
            AutotuneWeightVortexQuality = 0.18,
            AutotuneWeightRadialPressure = 0.12,
            AutotuneWeightBreakdownPenalty = 0.14,
            AutotuneWeightSeparationPenalty = 0.10,
            AutotuneWeightLossPenalty = 0.10,
            AutotuneWeightEjectorPenalty = 0.08,
            AutotuneWeightLowAxialPenalty = 0.06,
            AutotuneRandomSeed = 20260213,
            AutotuneUseSynthesisBaseline = true,
            UseDerivedSwirlChamberDiameter = true,
            AutotuneSwirlChamberLengthMaxMm = 92.0,
            AutotuneSwirlChamberLengthScaleMin = 0.80,
            AutotuneSwirlChamberLengthScaleMax = 1.04
        };

    /// <summary>Source + design + <see cref="CreateRunWithCoarseToFineAutotune"/>.</summary>
    public static NozzleInput CreateInputWithCoarseToFineAutotune(int stage1 = 120, int stage2 = 96, int stage3 = 48) =>
        new(CreateSource(), CreateDesign(), CreateRunWithCoarseToFineAutotune(stage1, stage2, stage3));
}
