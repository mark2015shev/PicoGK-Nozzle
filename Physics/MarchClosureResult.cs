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

    /// <summary>Last step flux swirl number S (diagnostic; not used to scale entrainment).</summary>
    public double FinalEntrainmentSwirlCorrelation { get; init; }

    public double FinalContinuityResidualRelative { get; init; }

    /// <summary>Steps where ṁ was capped so implied bulk Mach through min(A_capture,A_annulus,A_bore) stayed ≤ limit.</summary>
    public int EntrainmentStepsLimitedBySwirlPassageCapacity { get; init; }
}
