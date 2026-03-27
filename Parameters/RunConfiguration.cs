using PicoGK_Run.Physics;

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

    /// <summary>Multiplies inlet-based capture area for entrainment intake (default 1 = full inlet lip area).</summary>
    public double CaptureAreaFactor { get; init; } = 1.0;

    /// <summary>
    /// When true, capture area is min(inlet area, free chamber annulus after hub+vane blockage); when false, inlet area × factor only.
    /// </summary>
    public bool UseExplicitInletCapture { get; init; }

    /// <summary>Apply core-suction entrainment demand boost B_vortex (still capped by dynamic head + absolute max).</summary>
    public bool UseSwirlEntrainmentBoost { get; init; } = true;

    /// <summary>Optional Re_D factor in Ce (chamber diameter, kinematic ν).</summary>
    public bool UseReynoldsEntrainmentFactor { get; init; }

    /// <summary>When true, SI path and autotune use 90° injector yaw (pure tangential injection in the axisymmetric model).</summary>
    public bool LockInjectorYawTo90Degrees { get; init; } = true;

    /// <summary>Fraction of gas-path annulus area (bore − hub) removed by stator/vane frontal blockage in the SI march [0–1].</summary>
    public double ChamberVaneBlockageFractionOfAnnulus { get; init; }

    /// <summary>
    /// When true, <see cref="Geometry.FlowDrivenNozzleBuilder"/> nudges chamber length (and optional exit) from <see cref="Physics.NozzleDesignResult"/> within safe clamps.
    /// </summary>
    public bool ApplySolvedGeometryHints { get; init; }

    /// <summary>Run <see cref="Geometry.GeometryContinuityValidator"/> on the driven design and surface issues in the report.</summary>
    public bool RunGeometryContinuityCheck { get; init; } = true;

    /// <summary>Record <see cref="Infrastructure.PipelineProfiler"/> stages on full voxel runs (timing + approximate heap/GC).</summary>
    public bool EnablePipelineProfiling { get; init; } = true;

    /// <summary>
    /// Run independent SI autotune trials on multiple cores (RNG sampling stays single-threaded for reproducibility).
    /// Set false for deterministic single-thread order (debug / diff baselines).
    /// </summary>
    public bool AutotuneUseParallelEvaluation { get; init; } = true;

    /// <summary>0 = use <see cref="Environment.ProcessorCount"/>.</summary>
    public int AutotuneMaxDegreeOfParallelism { get; init; }

    // --- Physics-controlled five-parameter autotune (see AutotuneStrategy.PhysicsControlledFiveParameter) ---

    /// <summary>Stage A coarse random trials (clamped 100–300 in runner).</summary>
    public int PhysicsAutotuneStageACandidates { get; init; } = 200;

    /// <summary>Top seeds carried from stage A to local refinement B (10–20).</summary>
    public int PhysicsAutotuneStageBTopSeeds { get; init; } = 15;

    /// <summary>Local perturbation trials per stage-B seed.</summary>
    public int PhysicsAutotuneStageBLocalTrialsPerSeed { get; init; } = 6;

    /// <summary>Stage C polish trials around best stage-B design.</summary>
    public int PhysicsAutotuneStageCPolishTrials { get; init; } = 24;

    /// <summary>Relative ± span for stage-B local search (fraction of each parameter).</summary>
    public double PhysicsAutotuneStageBRelativeSpan { get; init; } = 0.04;

    /// <summary>Narrower relative ± span for stage-C polish.</summary>
    public double PhysicsAutotuneStageCRelativeSpan { get; init; } = 0.015;

    /// <summary>
    /// When true, autotune does not overwrite the winning swirl chamber diameter with entrainment-derived bore before the final voxel pass
    /// (preserves direct tuned D_ch). Five-parameter physics autotune also skips that finalize step.
    /// </summary>
    public bool PhysicsAutotunePreserveWinningChamberDiameter { get; init; }

    // --- Swirl chamber bore sizing (first-order continuity / area; not CFD) ---

    /// <summary>
    /// When true with <see cref="UsePhysicsInformedGeometry"/>, chamber bore is sized from target ER and
    /// <see cref="ChamberSizingTargetAxialVelocityMps"/> via <see cref="Physics.SwirlChamberSizingModel"/> instead of jet×scale only.
    /// </summary>
    public bool UseDerivedSwirlChamberDiameter { get; init; }

    /// <summary>Target ṁ_amb/ṁ_core used in synthesis and autotune baseline when varying ER.</summary>
    public double GeometrySynthesisTargetEntrainmentRatio { get; init; } = NozzleGeometrySynthesis.DefaultTargetEntrainmentRatio;

    /// <summary>Nominal mixed axial velocity in the chamber for area sizing [m/s].</summary>
    public double ChamberSizingTargetAxialVelocityMps { get; init; } = 72.0;

    /// <summary>
    /// Mixed density for A = ṁ/(ρ V). If ≤ 0.5, <see cref="Physics.SwirlChamberSizingModel.EstimateRhoMixKgPerM3"/> is used.
    /// </summary>
    public double ChamberSizingRhoMixKgPerM3 { get; init; }

    /// <summary>Preferred maximum A_inj / A_bore (full circle); bore enlarged in sizing until satisfied when possible.</summary>
    public double ChamberSizingInjToChamberPreferredMax { get; init; } = 0.70;

    /// <summary>Warning threshold for A_inj / A_bore.</summary>
    public double ChamberSizingInjToChamberWarning { get; init; } = 0.85;

    /// <summary>Severe warning threshold for A_inj / A_bore.</summary>
    public double ChamberSizingInjToChamberSevere { get; init; } = 0.95;

    /// <summary>Cap bore: D_ch ≤ multiplier × (jet diameter from source area).</summary>
    public double DerivedChamberMaxDiameterMultiplierVsJet { get; init; } = 3.0;

    /// <summary>Base multiplier on jet diameter for minimum bore (vortex preservation floor); scaled by swirl in the model.</summary>
    public double DerivedChamberMinDiameterMultiplierVsJet { get; init; } = 0.88;

    /// <summary>After length heuristic, clamp L/D to at least this when derived sizing is on (0 = skip).</summary>
    public double DerivedChamberTargetMinLd { get; init; } = 0.88;

    /// <summary>After length heuristic, clamp L/D to at most this when derived sizing is on (0 = skip).</summary>
    public double DerivedChamberTargetMaxLd { get; init; } = 1.28;

    /// <summary>
    /// When false with <see cref="UseDerivedSwirlChamberDiameter"/>, autotune must not rescale bore via ChamberD knobs (tune ER / other axes only).
    /// Set true to allow direct ChamberD multipliers to fight derived sizing.
    /// </summary>
    public bool AllowAutotuneDirectChamberDiameterOverride { get; init; }

    /// <summary>
    /// After autotune search, overwrite winning seed <c>SwirlChamberDiameterMm</c> with
    /// <see cref="PicoGK_Run.Physics.SwirlChamberSizingModel.ComputeDerived"/> at <see cref="GeometrySynthesisTargetEntrainmentRatio"/>
    /// before the final SI + voxel pass (other trial dimensions preserved). First-order continuity only — not CFD.
    /// </summary>
    public bool AutotuneFinalizeApplyEntrainmentDerivedChamberBore { get; init; } = true;

    /// <summary>
    /// When true (K320-class presets), SI path applies hard invalidation: chamber/march static &gt; 10 bar abs,
    /// pressure-thrust inconsistent with low momentum, and |F_net| &gt; 5000 N (see SiThrustSanity).
    /// </summary>
    public bool ApplyHardSiThrustAndPressureAssertions { get; init; }

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
        AutotuneSwirlChamberDiameterScaleMax = AutotuneSwirlChamberDiameterScaleMax,
        CaptureAreaFactor = CaptureAreaFactor,
        UseExplicitInletCapture = UseExplicitInletCapture,
        UseSwirlEntrainmentBoost = UseSwirlEntrainmentBoost,
        UseReynoldsEntrainmentFactor = UseReynoldsEntrainmentFactor,
        LockInjectorYawTo90Degrees = LockInjectorYawTo90Degrees,
        ChamberVaneBlockageFractionOfAnnulus = ChamberVaneBlockageFractionOfAnnulus,
        ApplySolvedGeometryHints = ApplySolvedGeometryHints,
        RunGeometryContinuityCheck = RunGeometryContinuityCheck,
        EnablePipelineProfiling = EnablePipelineProfiling,
        AutotuneUseParallelEvaluation = AutotuneUseParallelEvaluation,
        AutotuneMaxDegreeOfParallelism = AutotuneMaxDegreeOfParallelism,
        UseDerivedSwirlChamberDiameter = UseDerivedSwirlChamberDiameter,
        GeometrySynthesisTargetEntrainmentRatio = GeometrySynthesisTargetEntrainmentRatio,
        ChamberSizingTargetAxialVelocityMps = ChamberSizingTargetAxialVelocityMps,
        ChamberSizingRhoMixKgPerM3 = ChamberSizingRhoMixKgPerM3,
        ChamberSizingInjToChamberPreferredMax = ChamberSizingInjToChamberPreferredMax,
        ChamberSizingInjToChamberWarning = ChamberSizingInjToChamberWarning,
        ChamberSizingInjToChamberSevere = ChamberSizingInjToChamberSevere,
        DerivedChamberMaxDiameterMultiplierVsJet = DerivedChamberMaxDiameterMultiplierVsJet,
        DerivedChamberMinDiameterMultiplierVsJet = DerivedChamberMinDiameterMultiplierVsJet,
        DerivedChamberTargetMinLd = DerivedChamberTargetMinLd,
        DerivedChamberTargetMaxLd = DerivedChamberTargetMaxLd,
        AllowAutotuneDirectChamberDiameterOverride = AllowAutotuneDirectChamberDiameterOverride,
        AutotuneFinalizeApplyEntrainmentDerivedChamberBore = AutotuneFinalizeApplyEntrainmentDerivedChamberBore,
        PhysicsAutotuneStageACandidates = PhysicsAutotuneStageACandidates,
        PhysicsAutotuneStageBTopSeeds = PhysicsAutotuneStageBTopSeeds,
        PhysicsAutotuneStageBLocalTrialsPerSeed = PhysicsAutotuneStageBLocalTrialsPerSeed,
        PhysicsAutotuneStageCPolishTrials = PhysicsAutotuneStageCPolishTrials,
        PhysicsAutotuneStageBRelativeSpan = PhysicsAutotuneStageBRelativeSpan,
        PhysicsAutotuneStageCRelativeSpan = PhysicsAutotuneStageCRelativeSpan,
        PhysicsAutotunePreserveWinningChamberDiameter = PhysicsAutotunePreserveWinningChamberDiameter,
        ApplyHardSiThrustAndPressureAssertions = ApplyHardSiThrustAndPressureAssertions
    };
}
