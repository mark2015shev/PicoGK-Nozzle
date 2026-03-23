using PicoGK_Run.Parameters;

namespace PicoGK_Run.Infrastructure;

/// <summary>Metadata when <see cref="RunConfiguration.UseAutotune"/> ran before the final pipeline.</summary>
public sealed class AutotuneRunSummary
{
    public int Trials { get; init; }
    public double BestScore { get; init; }
    public NozzleDesignInputs WinningSeedDesign { get; init; } = null!;
}
