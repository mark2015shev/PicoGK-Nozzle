using System.Collections.Generic;

namespace PicoGK_Run.Physics;

/// <summary>SI march duct + entrainment audit (chamber segment) — not CFD.</summary>
public sealed class SwirlChamberMarchDiagnostics
{
    public double AInletMm2 { get; init; }
    public double AChamberBoreMm2 { get; init; }
    public double AHubMm2 { get; init; }
    public double AFreeChamberMm2 { get; init; }
    public double AInjTotalMm2 { get; init; }
    public double AExitMm2 { get; init; }
    public double RatioInletToChamber { get; init; }
    public double RatioInjToChamber { get; init; }
    public double RatioFreeToChamber { get; init; }
    public double RatioExitToChamber { get; init; }
    public double DuctEffectiveAreaM2 { get; init; }
    public double EntrainmentPerimeterM { get; init; }
    public double CaptureAreaM2 { get; init; }
    public double EntrainmentCeBase { get; init; }
    public double EntrainmentCeAtFirstStep { get; init; }

    /// <summary>Optional Pa added to (P_amb − P_local) in capture-boundary entrainment driver when core suction coupling is on.</summary>
    public double CaptureStaticPressureDeficitAugmentationPa { get; init; }

    /// <summary>Mixed ṁ_total vs min(capture, free annulus) at swirl entrance plane and chamber end.</summary>
    public SwirlEntranceCapacityDualResult? SwirlEntranceCapacityStations { get; init; }

    /// <summary>Live governor vs post-hoc dual-station audit (demand vs allowed secondary, areas, Mach).</summary>
    public SwirlEntrainmentGovernorSummary? EntrainmentGovernor { get; init; }

    /// <summary>Last-step radial shaping audit lines (bulk vs core/wall).</summary>
    public IReadOnlyList<string> RadialShapingReportLines { get; init; } = System.Array.Empty<string>();

    public IReadOnlyList<string> ValidationWarnings { get; init; } = System.Array.Empty<string>();

    /// <summary>Last-station quasi-steady inlet vs expander escape split (bulk P from march).</summary>
    public SwirlChamberDualPathDischargeResult? ChamberDischargeSplit { get; init; }
}
