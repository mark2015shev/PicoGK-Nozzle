using System;
using System.Collections.Generic;
namespace PicoGK_Run.Physics;

/// <summary>
/// Mixed stream ṁ_total = ṁ_p + ṁ_s through min(capture, effective annulus) with ideal-gas a(T).
/// </summary>
public sealed class SwirlEntranceCapacityResult
{
    public double MdotPrimaryKgS { get; init; }
    public double MdotSecondaryKgS { get; init; }
    public double MdotTotalKgS { get; init; }
    public double RhoMixKgM3 { get; init; }
    public double TMixK { get; init; }
    public double SpeedOfSoundMixMps { get; init; }

    public double AInletCaptureM2 { get; init; }
    public double AChamberBoreM2 { get; init; }
    public double AFreeAnnulusM2 { get; init; }
    /// <summary>min(A_capture, A_free_annulus, A_bore) — conservative series bottleneck for bulk throughflow in this 1-D model.</summary>
    public double EffectiveSwirlEntranceAreaM2 { get; init; }
    public string GoverningAreaDescription { get; init; } = "";

    public double VRequiredMps { get; init; }
    public double MachRequired { get; init; }
    public double VAxialFromMarchMps { get; init; }
    public SwirlEntranceCapacityClassification Classification { get; init; }
    public SwirlEntranceCapacityLimits LimitsApplied { get; init; } = SwirlEntranceCapacityLimits.Default;

    /// <summary>Label for dual-station reports (e.g. swirl entrance vs chamber end).</summary>
    public string StationLabel { get; init; } = "";

    /// <summary>Health / autotune hooks: choking → DESIGN ERROR; restrictive → CAPACITY FAIL; caution → WARNING.</summary>
    public IEnumerable<string> EnumerateHealthMessages()
    {
        var L = LimitsApplied;
        switch (Classification)
        {
            case SwirlEntranceCapacityClassification.FailChoking:
                yield return
                    $"DESIGN ERROR: SWIRL ENTRANCE CAPACITY: Mach_required={MachRequired:F3} ≥ {L.MachChokingMin:F2} (choking risk) for mdot_total={MdotTotalKgS:F4} kg/s through A_eff={EffectiveSwirlEntranceAreaM2:E4} m².";
                break;
            case SwirlEntranceCapacityClassification.FailRestrictive:
                yield return
                    $"CAPACITY FAIL: SWIRL ENTRANCE: Mach_required={MachRequired:F3} > {L.MachCautionMax:F2} (too restrictive vs mixed flow) — increase effective capture/annulus or reduce total mass flow.";
                break;
            case SwirlEntranceCapacityClassification.Warning:
                yield return
                    $"WARNING: SWIRL ENTRANCE CAPACITY: Mach_required={MachRequired:F3} in caution band ({L.MachGoodMax:F2} < M ≤ {L.MachCautionMax:F2}).";
                break;
        }
    }

    public IReadOnlyList<string> FormatReportLines()
    {
        string cls = Classification switch
        {
            SwirlEntranceCapacityClassification.Pass => "PASS",
            SwirlEntranceCapacityClassification.Warning => "WARNING",
            SwirlEntranceCapacityClassification.FailRestrictive => "FAIL (restrictive)",
            SwirlEntranceCapacityClassification.FailChoking => "FAIL (choking risk)",
            _ => Classification.ToString()
        };
        string title = string.IsNullOrEmpty(StationLabel)
            ? "SWIRL ENTRANCE CAPACITY CHECK (mixed flow vs effective area)"
            : $"SWIRL ENTRANCE CAPACITY — {StationLabel}";
        return new List<string>
        {
            title,
            $"  mdot_primary [kg/s]:           {MdotPrimaryKgS:F6}",
            $"  mdot_secondary [kg/s]:        {MdotSecondaryKgS:F6}",
            $"  mdot_total [kg/s]:            {MdotTotalKgS:F6}",
            $"  rho_mix [kg/m3]:              {RhoMixKgM3:F6}",
            $"  T_mix [K]:                    {TMixK:F2}",
            $"  a_mix = sqrt(gamma*R*T) [m/s]: {SpeedOfSoundMixMps:F2}",
            $"  A_capture [m2]:               {AInletCaptureM2:E6}",
            $"  A_chamber_bore [m2]:          {AChamberBoreM2:E6}",
            $"  A_free_annulus [m2]:          {AFreeAnnulusM2:E6}",
            $"  EffectiveSwirlEntranceAreaM2: {EffectiveSwirlEntranceAreaM2:E6}  ({GoverningAreaDescription})",
            $"  V_required = mdot/(rho*A) [m/s]: {VRequiredMps:F3}",
            $"  V_axial march (reference) [m/s]: {VAxialFromMarchMps:F3}",
            $"  Mach_required = V_req/a:       {MachRequired:F4}",
            $"  result:                        {cls}"
        };
    }
}
