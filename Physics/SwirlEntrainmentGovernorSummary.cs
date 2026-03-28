using System.Collections.Generic;

namespace PicoGK_Run.Physics;

/// <summary>
/// Post-march audit: dual-station capacity + correlation demand vs what the live governor allowed (not CFD).
/// </summary>
public sealed class SwirlEntrainmentGovernorSummary
{
    public double EntrainmentGovernorMachLimitUsed { get; init; }

    public double EntranceEffectiveAreaM2 { get; init; }
    public double EndEffectiveAreaM2 { get; init; }
    public double GoverningEffectiveAreaM2 { get; init; }
    public string GoverningStationLabel { get; init; } = "";

    public double MdotPrimaryKgS { get; init; }
    /// <summary>Σ per-step correlation demand Δṁ_req [kg/s] over the march (increment sum).</summary>
    public double MdotSecondaryDemandSumKgS { get; init; }
    public double MdotSecondaryFinalKgS { get; init; }
    public double MdotTotalFinalKgS { get; init; }

    public double RhoMixGoverningKgM3 { get; init; }
    public double TMixGoverningK { get; init; }
    public double AMixGoverningMps { get; init; }
    public double VRequiredGoverningMps { get; init; }
    public double MachRequiredGoverning { get; init; }
    public SwirlEntranceCapacityClassification CapacityClassificationGoverning { get; init; }

    public int StepsWhereGovernorTrimmedEntrainment { get; init; }
    public double SumMassFlowTrimmedByGovernorKgS { get; init; }
    public bool EntrainmentCappedByGovernor => StepsWhereGovernorTrimmedEntrainment > 0;

    public IReadOnlyList<string> FormatReportLines()
    {
        string capCls = CapacityClassificationGoverning switch
        {
            SwirlEntranceCapacityClassification.Pass => "PASS",
            SwirlEntranceCapacityClassification.Warning => "WARNING",
            SwirlEntranceCapacityClassification.FailRestrictive => "FAIL (restrictive)",
            SwirlEntranceCapacityClassification.FailChoking => "FAIL (choking risk)",
            _ => CapacityClassificationGoverning.ToString()
        };
        return new List<string>
        {
            "SWIRL CAPACITY / ENTRAINMENT GOVERNOR",
            $"  Live governor Mach limit (march trim):     {EntrainmentGovernorMachLimitUsed:F4}  (named: SwirlEntranceCapacityLimits.EntrainmentGovernorMachMax)",
            $"  EffectiveSwirlEntranceAreaM2 entrance:      {EntranceEffectiveAreaM2:E6}",
            $"  EffectiveSwirlEntranceAreaM2 chamber end:   {EndEffectiveAreaM2:E6}",
            $"  Governing A_eff (bottleneck):                {GoverningEffectiveAreaM2:E6}  ({GoverningStationLabel})",
            $"  mdot_primary [kg/s]:                         {MdotPrimaryKgS:F6}",
            $"  mdot_secondary demand (Σ Δṁ_req steps):      {MdotSecondaryDemandSumKgS:F6}",
            $"  mdot_secondary allowed (final march):        {MdotSecondaryFinalKgS:F6}",
            $"  mdot_total (final):                         {MdotTotalFinalKgS:F6}",
            $"  rho_mix / T_mix / a_mix (governing audit):   {RhoMixGoverningKgM3:F5} kg/m3  {TMixGoverningK:F2} K  {AMixGoverningMps:F2} m/s",
            $"  V_required / Mach_required (governing):      {VRequiredGoverningMps:F3} m/s  {MachRequiredGoverning:F4}",
            $"  Post-hoc capacity classification (governing): {capCls}",
            $"  Governor trimmed entrainment:                 {(EntrainmentCappedByGovernor ? "YES" : "NO")}  (steps: {StepsWhereGovernorTrimmedEntrainment}, ΣΔṁ trimmed [kg/s]: {SumMassFlowTrimmedByGovernorKgS:F6})"
        };
    }

    public static SwirlEntrainmentGovernorSummary Build(
        SwirlEntranceCapacityDualResult dual,
        FlowMarchDetailedResult march,
        double primaryMdotKgS)
    {
        SwirlEntranceCapacityResult g = dual.GoverningResult;
        FlowStepState? last = march.StepPhysicsStates.Count > 0 ? march.StepPhysicsStates[^1] : null;
        double mSecFinal = last?.MdotSecondaryKgS ?? 0.0;
        double mTotFinal = last?.MdotTotalKgS ?? primaryMdotKgS;
        return new SwirlEntrainmentGovernorSummary
        {
            EntrainmentGovernorMachLimitUsed = march.EntrainmentGovernorMachMaxUsed,
            EntranceEffectiveAreaM2 = dual.EntrancePlane.EffectiveSwirlEntranceAreaM2,
            EndEffectiveAreaM2 = dual.ChamberEnd.EffectiveSwirlEntranceAreaM2,
            GoverningEffectiveAreaM2 = g.EffectiveSwirlEntranceAreaM2,
            GoverningStationLabel = dual.GoverningStationLabel,
            MdotPrimaryKgS = primaryMdotKgS,
            MdotSecondaryDemandSumKgS = march.SumCorrelationEntrainmentDemandKgS,
            MdotSecondaryFinalKgS = mSecFinal,
            MdotTotalFinalKgS = mTotFinal,
            RhoMixGoverningKgM3 = g.RhoMixKgM3,
            TMixGoverningK = g.TMixK,
            AMixGoverningMps = g.SpeedOfSoundMixMps,
            VRequiredGoverningMps = g.VRequiredMps,
            MachRequiredGoverning = g.MachRequired,
            CapacityClassificationGoverning = g.Classification,
            StepsWhereGovernorTrimmedEntrainment = march.EntrainmentStepsLimitedBySwirlPassageCapacity,
            SumMassFlowTrimmedByGovernorKgS = march.SumEntrainmentMassTrimmedByPassageGovernorKgS
        };
    }
}
