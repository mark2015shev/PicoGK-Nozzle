using System;
using System.Collections.Generic;

namespace PicoGK_Run.Physics;

/// <summary>
/// Capacity at swirl entrance (first march station) and chamber end: V_req = ṁ_total/(ρ_mix A_eff),
/// Mach_req = V_req/a_mix, a_mix = sqrt(γ R T_mix). Combined classification uses the more restrictive station.
/// </summary>
public sealed class SwirlEntranceCapacityDualResult
{
    public const double MachDivergenceWarningThreshold = 0.10;

    /// <summary>First interior march station (x = dx).</summary>
    public SwirlEntranceCapacityResult EntrancePlane { get; init; } = null!;

    /// <summary>Last march station in the swirl chamber segment.</summary>
    public SwirlEntranceCapacityResult ChamberEnd { get; init; } = null!;

    public SwirlEntranceCapacityClassification CombinedClassification { get; init; }

    /// <summary>Station whose Mach_required is higher (series bottleneck in this 1-D view).</summary>
    public string GoverningStationLabel { get; init; } = "";

    public double MachAbsoluteDelta { get; init; }

    public bool StationsDivergeSignificantly { get; init; }

    public SwirlEntranceCapacityResult GoverningResult =>
        EntrancePlane.MachRequired >= ChamberEnd.MachRequired ? EntrancePlane : ChamberEnd;

    public static SwirlEntranceCapacityClassification Worst(
        SwirlEntranceCapacityClassification a,
        SwirlEntranceCapacityClassification b)
    {
        static int Rank(SwirlEntranceCapacityClassification c) => c switch
        {
            SwirlEntranceCapacityClassification.Pass => 0,
            SwirlEntranceCapacityClassification.Warning => 1,
            SwirlEntranceCapacityClassification.FailRestrictive => 2,
            SwirlEntranceCapacityClassification.FailChoking => 3,
            _ => 0
        };
        return Rank(a) >= Rank(b) ? a : b;
    }

    public IEnumerable<string> EnumerateHealthMessages()
    {
        if (StationsDivergeSignificantly)
        {
            yield return
                $"SWIRL ENTRANCE CAPACITY: entrance Mach_required={EntrancePlane.MachRequired:F3} vs chamber-end {ChamberEnd.MachRequired:F3} (|Δ|={MachAbsoluteDelta:F3}); governing={GoverningStationLabel}.";
        }

        foreach (string m in GoverningResult.EnumerateHealthMessages())
            yield return m;
    }

    public IReadOnlyList<string> FormatReportLines()
    {
        var lines = new List<string>
        {
            "SWIRL ENTRANCE CAPACITY CHECK (dual station)",
            $"  entrance Mach_required:    {EntrancePlane.MachRequired:F4}  ({EntrancePlane.Classification})",
            $"  chamber-end Mach_required: {ChamberEnd.MachRequired:F4}  ({ChamberEnd.Classification})",
            $"  |ΔMach|:                   {MachAbsoluteDelta:F4}  (warn if ≥ {MachDivergenceWarningThreshold:F2})",
            $"  governing station:         {GoverningStationLabel}",
            $"  combined classification:   {FormatCls(CombinedClassification)}"
        };
        lines.Add("");
        lines.Add("--- Entrance plane detail ---");
        lines.AddRange(EntrancePlane.FormatReportLines());
        lines.Add("");
        lines.Add("--- Chamber end detail ---");
        lines.AddRange(ChamberEnd.FormatReportLines());
        return lines;
    }

    private static string FormatCls(SwirlEntranceCapacityClassification c) => c switch
    {
        SwirlEntranceCapacityClassification.Pass => "PASS",
        SwirlEntranceCapacityClassification.Warning => "WARNING",
        SwirlEntranceCapacityClassification.FailRestrictive => "FAIL (restrictive)",
        SwirlEntranceCapacityClassification.FailChoking => "FAIL (choking risk)",
        _ => c.ToString()
    };
}
