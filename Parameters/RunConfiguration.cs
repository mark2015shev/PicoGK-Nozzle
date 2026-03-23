namespace PicoGK_Run.Parameters;

/// <summary>Viewer + optional physics-informed geometry + optional autotune search.</summary>
public sealed class RunConfiguration
{
    public float VoxelSizeMM { get; init; } = 0.3f;
    public bool ShowInViewer { get; init; } = true;

    /// <summary>
    /// When true, <c>NozzleGeometrySynthesis</c> (Physics) replaces hand-sized diameters/lengths/expander/stator/inlet
    /// before the SI march (template still supplies injector yaw/pitch, count, wall, axial ratio). Ignored if <see cref="UseAutotune"/> runs first with a chosen seed.
    /// </summary>
    public bool UsePhysicsInformedGeometry { get; init; }

    /// <summary>
    /// When true, <c>NozzleDesignAutotune</c> searches scaled geometries (random search on SI model),
    /// then the winning seed is used for the full run (voxels + report). <b>Validate results in CFD.</b>
    /// </summary>
    public bool UseAutotune { get; init; }

    /// <summary>Random search layout: single pool vs three-phase coarse-to-fine.</summary>
    public AutotuneStrategy AutotuneStrategy { get; init; } = AutotuneStrategy.SingleStage;

    /// <summary>Forward evaluations per autotune run (each is a full SI solve, no voxels).</summary>
    public int AutotuneTrials { get; init; } = 160;

    /// <summary>Stage 1 (coarse-to-fine): broad SI trials.</summary>
    public int AutotuneStage1Trials { get; init; } = 120;

    /// <summary>Stage 2: trials split across diverse top seeds from stage 1.</summary>
    public int AutotuneStage2Trials { get; init; } = 96;

    /// <summary>Stage 3: polish trials around the best stage-2 design.</summary>
    public int AutotuneStage3Trials { get; init; } = 48;

    /// <summary>How many top-scoring candidates to keep after stage 1 (diversity filter applied).</summary>
    public int AutotuneTopSeedCountStage1 { get; init; } = 4;

    /// <summary>How many refined winners feed stage 3 (best overall used as polish center).</summary>
    public int AutotuneTopSeedCountStage2 { get; init; } = 2;

    /// <summary>Minimum normalized design-space distance between stage-1 seeds (greedy diversity). ~0.35 default.</summary>
    public double AutotuneDiversityMinDistance { get; init; } = 0.35;

    /// <summary>If false, pitch stays at the reference design value during search (yaw still varies).</summary>
    public bool AutotuneVaryPitch { get; init; } = true;

    public AutotunePerturbationBand AutotuneStage1Band { get; init; } = AutotunePerturbationBand.DefaultStage1Broad;

    public AutotunePerturbationBand AutotuneStage2Band { get; init; } = AutotunePerturbationBand.DefaultStage2Focused;

    public AutotunePerturbationBand AutotuneStage3Band { get; init; } = AutotunePerturbationBand.DefaultStage3Polish;

    /// <summary>Objective weight for ṁ_amb/ṁ_core (normalized to baseline).</summary>
    public double AutotuneWeightEntrainment { get; init; } = 0.26;

    /// <summary>Objective weight for F_net / F_source-only (with soft floor near 0.88×).</summary>
    public double AutotuneWeightThrust { get; init; } = 0.34;

    /// <summary>Objective weight for controlled-vortex quality (moderate swirl, stable regime, recovery) — not axial-ejector bias.</summary>
    public double AutotuneWeightVortexQuality { get; init; } = 0.18;

    /// <summary>Weight for useful radial pressure structure (core drop + wall rise), normalized vs baseline.</summary>
    public double AutotuneWeightRadialPressure { get; init; } = 0.12;

    /// <summary>Penalty weight for vortex breakdown risk [0–1].</summary>
    public double AutotuneWeightBreakdownPenalty { get; init; } = 0.14;

    /// <summary>Penalty weight for diffuser separation risk [0–1].</summary>
    public double AutotuneWeightSeparationPenalty { get; init; } = 0.10;

    /// <summary>Penalty weight for injector+stator total-pressure loss metric [0–1].</summary>
    public double AutotuneWeightLossPenalty { get; init; } = 0.10;

    /// <summary>Penalty weight for ejector regime stress score [0–1].</summary>
    public double AutotuneWeightEjectorPenalty { get; init; } = 0.08;

    /// <summary>Penalty weight for low axial momentum proxy [0–1].</summary>
    public double AutotuneWeightLowAxialPenalty { get; init; } = 0.06;

    /// <summary>Seed for reproducible random search.</summary>
    public int AutotuneRandomSeed { get; init; } = 20260213;

    /// <summary>If true, search centers on <see cref="NozzleGeometrySynthesis.Synthesize"/>; if false, on a copy of template design.</summary>
    public bool AutotuneUseSynthesisBaseline { get; init; } = true;

    /// <summary>Hard cap on swirl chamber axial length after each trial’s length scale [mm]. Keeps the green segment short.</summary>
    public double AutotuneSwirlChamberLengthMaxMm { get; init; } = 100.0;

    /// <summary>Per-trial random multiplier range on baseline <c>SwirlChamberLengthMm</c> (autotune search axis).</summary>
    public double AutotuneSwirlChamberLengthScaleMin { get; init; } = 0.82;

    public double AutotuneSwirlChamberLengthScaleMax { get; init; } = 1.06;

    /// <summary>Per-trial random multiplier range on baseline <c>SwirlChamberDiameterMm</c> (autotune search axis).</summary>
    public double AutotuneSwirlChamberDiameterScaleMin { get; init; } = 0.84;

    public double AutotuneSwirlChamberDiameterScaleMax { get; init; } = 1.20;

    /// <summary>
    /// Run flags after autotune: no second autotune pass, and no <c>UsePhysicsInformedGeometry</c> so the winning seed is not re-synthesized away.
    /// </summary>
    public RunConfiguration AfterAutotune() => new()
    {
        VoxelSizeMM = VoxelSizeMM,
        ShowInViewer = ShowInViewer,
        UsePhysicsInformedGeometry = false,
        UseAutotune = false,
        AutotuneStrategy = AutotuneStrategy,
        AutotuneTrials = AutotuneTrials,
        AutotuneStage1Trials = AutotuneStage1Trials,
        AutotuneStage2Trials = AutotuneStage2Trials,
        AutotuneStage3Trials = AutotuneStage3Trials,
        AutotuneTopSeedCountStage1 = AutotuneTopSeedCountStage1,
        AutotuneTopSeedCountStage2 = AutotuneTopSeedCountStage2,
        AutotuneDiversityMinDistance = AutotuneDiversityMinDistance,
        AutotuneVaryPitch = AutotuneVaryPitch,
        AutotuneStage1Band = AutotuneStage1Band,
        AutotuneStage2Band = AutotuneStage2Band,
        AutotuneStage3Band = AutotuneStage3Band,
        AutotuneWeightEntrainment = AutotuneWeightEntrainment,
        AutotuneWeightThrust = AutotuneWeightThrust,
        AutotuneWeightVortexQuality = AutotuneWeightVortexQuality,
        AutotuneWeightRadialPressure = AutotuneWeightRadialPressure,
        AutotuneWeightBreakdownPenalty = AutotuneWeightBreakdownPenalty,
        AutotuneWeightSeparationPenalty = AutotuneWeightSeparationPenalty,
        AutotuneWeightLossPenalty = AutotuneWeightLossPenalty,
        AutotuneWeightEjectorPenalty = AutotuneWeightEjectorPenalty,
        AutotuneWeightLowAxialPenalty = AutotuneWeightLowAxialPenalty,
        AutotuneRandomSeed = AutotuneRandomSeed,
        AutotuneUseSynthesisBaseline = AutotuneUseSynthesisBaseline,
        AutotuneSwirlChamberLengthMaxMm = AutotuneSwirlChamberLengthMaxMm,
        AutotuneSwirlChamberLengthScaleMin = AutotuneSwirlChamberLengthScaleMin,
        AutotuneSwirlChamberLengthScaleMax = AutotuneSwirlChamberLengthScaleMax,
        AutotuneSwirlChamberDiameterScaleMin = AutotuneSwirlChamberDiameterScaleMin,
        AutotuneSwirlChamberDiameterScaleMax = AutotuneSwirlChamberDiameterScaleMax
    };
}
