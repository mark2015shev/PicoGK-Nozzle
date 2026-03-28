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

    public static SwirlEntranceCapacityLimits Default { get; } = new();
}
