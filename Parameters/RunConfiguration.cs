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

    /// <summary>Forward evaluations per autotune run (each is a full SI solve, no voxels).</summary>
    public int AutotuneTrials { get; init; } = 160;

    /// <summary>Objective weight for ṁ_amb/ṁ_core (normalized to baseline).</summary>
    public double AutotuneWeightEntrainment { get; init; } = 0.52;

    /// <summary>Objective weight for F_net / F_source-only (with soft floor near 0.88×).</summary>
    public double AutotuneWeightThrust { get; init; } = 0.48;

    /// <summary>Seed for reproducible random search.</summary>
    public int AutotuneRandomSeed { get; init; } = 20260213;

    /// <summary>If true, search centers on <see cref="NozzleGeometrySynthesis.Synthesize"/>; if false, on a copy of template design.</summary>
    public bool AutotuneUseSynthesisBaseline { get; init; } = true;

    /// <summary>
    /// Run flags after autotune: no second autotune pass, and no <c>UsePhysicsInformedGeometry</c> so the winning seed is not re-synthesized away.
    /// </summary>
    public RunConfiguration AfterAutotune() => new()
    {
        VoxelSizeMM = VoxelSizeMM,
        ShowInViewer = ShowInViewer,
        UsePhysicsInformedGeometry = false,
        UseAutotune = false,
        AutotuneTrials = AutotuneTrials,
        AutotuneWeightEntrainment = AutotuneWeightEntrainment,
        AutotuneWeightThrust = AutotuneWeightThrust,
        AutotuneRandomSeed = AutotuneRandomSeed,
        AutotuneUseSynthesisBaseline = AutotuneUseSynthesisBaseline
    };
}
