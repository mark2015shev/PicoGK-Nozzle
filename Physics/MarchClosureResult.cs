namespace PicoGK_Run.Physics;

/// <summary>End-of-march aggregates (chamber SI path).</summary>
public sealed class MarchClosureResult
{
    public double FinalMachBulk { get; init; }
    public double FinalReynolds { get; init; }
    public bool AnyEntrainmentChoked { get; init; }
    public double FinalFluxSwirlNumber { get; init; }

    /// <summary>Last step |Vt|/max(|Va|, Va_floor).</summary>
    public double FinalChamberSwirlBulk { get; init; }

    /// <summary>Last step entrainment correlation input (bounded).</summary>
    public double FinalEntrainmentSwirlCorrelation { get; init; }

    public double FinalContinuityResidualRelative { get; init; }
}
