namespace PicoGK_Run.Parameters;

/// <summary>Controls optional SI diagnostic logging volume (full pipeline and validation sweeps).</summary>
public enum SiVerbosityLevel
{
    /// <summary>Minimal summary; suppress per-solve SOURCE / swirl capacity blocks (use for sweep batches).</summary>
    Low = 0,

    /// <summary>Standard physics blocks for a single run.</summary>
    Normal = 1,

    /// <summary>Full blocks + march invariant details when enabled.</summary>
    High = 2
}
