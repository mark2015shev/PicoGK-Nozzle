namespace PicoGK_Run.Parameters;

/// <summary>How <see cref="RunConfiguration.UseAutotune"/> explores the design space (SI-only trials).</summary>
public enum AutotuneStrategy
{
    /// <summary>One pool of <see cref="RunConfiguration.AutotuneTrials"/> random candidates (legacy).</summary>
    SingleStage,

    /// <summary>Three phases: broad exploration → refine top seeds → polish best (same scoring path throughout).</summary>
    CoarseToFine
}
