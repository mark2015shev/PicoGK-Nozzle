using PicoGK_Run.Core;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Infrastructure;

/// <summary>Metadata when <see cref="RunConfiguration.UseAutotune"/> ran before the final pipeline.</summary>
public sealed class AutotuneRunSummary
{
    public AutotuneStrategy Strategy { get; init; } = AutotuneStrategy.SingleStage;

    public int Trials { get; init; }
    public double BestScore { get; init; }

    /// <summary>Hand/template design from the incoming <see cref="NozzleInput"/> before search (not synthesis baseline).</summary>
    public NozzleDesignInputs BaselineTemplateDesign { get; init; } = null!;

    public NozzleDesignInputs WinningSeedDesign { get; init; } = null!;

    /// <summary>Multi-stage search log when coarse-to-fine autotune was used.</summary>
    public string? CoarseToFineLog { get; init; }

    /// <summary>True if the search run had <c>UseDerivedSwirlChamberDiameter</c> (bore from continuity model during trials when synthesis baseline on).</summary>
    public bool SearchUsedEntrainmentDerivedBoreSizing { get; init; }

    /// <summary>True if autotune was allowed to rescale bore via ChamberD knobs during search.</summary>
    public bool SearchAllowedDirectChamberDiameterOverride { get; init; }

    /// <summary>True if final pass overwrote winning seed bore via entrainment-derived model at configured target ER.</summary>
    public bool FinalPassAppliedEntrainmentDerivedChamberBore { get; init; }
}
