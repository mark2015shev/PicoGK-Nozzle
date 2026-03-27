namespace PicoGK_Run.Parameters;

/// <summary>How <see cref="RunConfiguration.UseAutotune"/> explores the design space (SI-only trials).</summary>
public enum AutotuneStrategy
{
    /// <summary>One pool of <see cref="RunConfiguration.AutotuneTrials"/> random candidates (legacy).</summary>
    SingleStage,

    /// <summary>Three phases: broad exploration → refine top seeds → polish best (same scoring path throughout).</summary>
    CoarseToFine,

    /// <summary>
    /// Stages A→B→C: only five geometry parameters (chamber D/L, inlet capture, expander half-angle, stator angle);
    /// injector yaw fixed 90°; no synthesis baseline in search; physics-based score via PhysicsAutotuneScoring.
    /// </summary>
    PhysicsControlledFiveParameter
}
