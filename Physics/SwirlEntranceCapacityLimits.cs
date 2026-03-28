namespace PicoGK_Run.Physics;

/// <summary>
/// First-order Mach bands for mixed flow through the effective swirl-entrance / chamber annulus (tunable).
/// Documented alongside <see cref="ChamberAerodynamicsConfiguration.ChamberSizingTargetMachMixedFlow"/> (sizing) vs capacity audit (here).
/// </summary>
public sealed class SwirlEntranceCapacityLimits
{
    /// <summary>Mach ≤ this → <see cref="SwirlEntranceCapacityClassification.Pass"/> (good).</summary>
    public double MachGoodMax { get; init; } = 0.30;

    /// <summary>Mach above <see cref="MachGoodMax"/> and ≤ this → <see cref="SwirlEntranceCapacityClassification.Warning"/> (caution).</summary>
    public double MachCautionMax { get; init; } = 0.60;

    /// <summary>Mach &gt; <see cref="MachCautionMax"/> and &lt; <see cref="MachChokingMin"/> → <see cref="SwirlEntranceCapacityClassification.FailRestrictive"/> (bad).</summary>
    public double MachChokingMin { get; init; } = 1.00;

    /// <summary>
    /// Live march entrainment governor (active limit): ṁ_total ≤ ρ_mix A_eff a M. Default matches <see cref="MachGoodMax"/> (conservative).
    /// Classification bands above still use all four thresholds; this is the Mach used inside <see cref="FlowMarcher"/> to trim demand.
    /// </summary>
    public double EntrainmentGovernorMachMax { get; init; } = 0.30;

    public static SwirlEntranceCapacityLimits Default { get; } = new();
}
