namespace PicoGK_Run.Infrastructure.Pipeline;

/// <summary>Where the same geometric + SI path is shared, mode only skips expensive work (voxels, profiling), not physics meaning.</summary>
public enum NozzleEvaluationMode
{
    /// <summary>Autotune / validation: no voxels; continuity optional per <see cref="Parameters.RunConfiguration"/>.</summary>
    TuningFast,

    /// <summary>Full pipeline solve leg: same preparation + <c>SolveSiPath</c> as tuning; voxels built by caller.</summary>
    FinalDetailed
}
