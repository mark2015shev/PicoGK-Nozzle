namespace PicoGK_Run.Parameters;

/// <summary>How <see cref="RunConfiguration.UseAutotune"/> explores the design space (SI-only trials).</summary>
public enum AutotuneStrategy
{
    /// <summary>One pool of <see cref="RunConfiguration.AutotuneTrials"/> random candidates (legacy).</summary>
    SingleStage,

    /// <summary>Three phases: broad exploration → refine top seeds → polish best (same scoring path throughout).</summary>
    CoarseToFine,

    /// <summary>
    /// Stages A→B→C over <see cref="NozzleGeometryGenome"/> Tier A (full skeleton: chamber, inlet, injector axial ratio,
    /// expander length/angle, exit, stator angle); Tier B optional in stage C; injector port template merged by mapper;
    /// physics score via PhysicsAutotuneScoring.
    /// </summary>
    PhysicsControlledFiveParameter
}
