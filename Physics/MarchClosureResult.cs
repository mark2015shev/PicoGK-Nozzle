namespace PicoGK_Run.Physics;

/// <summary>End-of-march aggregates (chamber SI path).</summary>
public sealed class MarchClosureResult
{
    public double FinalMachBulk { get; init; }
    public double FinalReynolds { get; init; }
    public bool AnyEntrainmentChoked { get; init; }
    public double FinalFluxSwirlNumber { get; init; }
    public double FinalContinuityResidualRelative { get; init; }
}
