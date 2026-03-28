using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// Capacity: V_req = ṁ_total/(ρ_mix A_eff), Mach_req = V_req/a(T_mix), a_mix = sqrt(γ R T_mix),
/// A_eff = min(A_capture, A_free_annulus). Uses <see cref="MixedFlowStationState"/> for a single evaluation plane.
/// </summary>
public static class SwirlEntranceCapacityEvaluator
{
    public static SwirlEntranceCapacityResult EvaluateAtStation(
        GasProperties gas,
        MixedFlowStationState mix,
        double captureAreaM2,
        double chamberBoreAreaM2,
        double freeAnnulusEffectiveAreaM2,
        string stationLabel,
        SwirlEntranceCapacityLimits? limits = null)
    {
        limits ??= SwirlEntranceCapacityLimits.Default;
        double aCap = Math.Max(captureAreaM2, 1e-18);
        double aFree = Math.Max(freeAnnulusEffectiveAreaM2, 1e-18);
        double aBore = Math.Max(chamberBoreAreaM2, 1e-18);

        double aEff = Math.Min(Math.Min(aCap, aFree), aBore);
        string gov =
            $"governing = min(A_capture={aCap:E4}, A_free_annulus={aFree:E4}, A_bore={aBore:E4}) m²";

        double denom = mix.DensityKgM3 * aEff;
        double vReq = mix.MdotTotalKgS / Math.Max(denom, 1e-24);
        double aSound = gas.SpeedOfSound(mix.TStaticK);
        double machReq = aSound > 1e-9 ? vReq / aSound : 0.0;

        SwirlEntranceCapacityClassification cls;
        if (machReq >= limits.MachChokingMin)
            cls = SwirlEntranceCapacityClassification.FailChoking;
        else if (machReq > limits.MachCautionMax)
            cls = SwirlEntranceCapacityClassification.FailRestrictive;
        else if (machReq > limits.MachGoodMax)
            cls = SwirlEntranceCapacityClassification.Warning;
        else
            cls = SwirlEntranceCapacityClassification.Pass;

        return new SwirlEntranceCapacityResult
        {
            MdotPrimaryKgS = mix.MdotPrimaryKgS,
            MdotSecondaryKgS = mix.MdotSecondaryKgS,
            MdotTotalKgS = mix.MdotTotalKgS,
            RhoMixKgM3 = mix.DensityKgM3,
            TMixK = mix.TStaticK,
            SpeedOfSoundMixMps = aSound,
            AInletCaptureM2 = aCap,
            AChamberBoreM2 = aBore,
            AFreeAnnulusM2 = aFree,
            EffectiveSwirlEntranceAreaM2 = aEff,
            GoverningAreaDescription = gov,
            VRequiredMps = vReq,
            MachRequired = machReq,
            VAxialFromMarchMps = mix.VAxialMps,
            Classification = cls,
            LimitsApplied = limits,
            StationLabel = stationLabel
        };
    }

    /// <summary>Entrance (first march station or inlet jet) and chamber end; combined classification is the worse of the two.</summary>
    public static SwirlEntranceCapacityDualResult EvaluateDual(
        GasProperties gas,
        FlowStepState? entranceMarchStep,
        FlowStepState? endMarchStep,
        JetState inletJet,
        JetState lastJet,
        double primaryTangentialVelocityMps,
        double captureAreaM2,
        double chamberBoreAreaM2,
        double freeAnnulusEffectiveAreaM2,
        SwirlEntranceCapacityLimits? limits = null)
    {
        limits ??= SwirlEntranceCapacityLimits.Default;

        MixedFlowStationState mixEntrance;
        if (entranceMarchStep != null)
            mixEntrance = MixedFlowStationState.FromFlowStepState(entranceMarchStep, "swirl entrance (first march station)");
        else
        {
            var compIn = CompressibleState.FromMixedStatic(
                gas,
                inletJet.PressurePa,
                inletJet.TemperatureK,
                inletJet.VelocityMps,
                primaryTangentialVelocityMps);
            mixEntrance = MixedFlowStationState.FromJetState(
                inletJet,
                primaryTangentialVelocityMps,
                compIn.MachNumber,
                compIn.TotalPressurePa,
                compIn.TotalTemperatureK,
                "swirl entrance (inlet jet, pre-march)");
        }

        MixedFlowStationState mixEnd;
        if (endMarchStep != null)
            mixEnd = MixedFlowStationState.FromFlowStepState(endMarchStep, "chamber end (last march station)");
        else
        {
            double vtEnd = 0.0;
            var compEnd = CompressibleState.FromMixedStatic(
                gas,
                lastJet.PressurePa,
                lastJet.TemperatureK,
                lastJet.VelocityMps,
                vtEnd);
            mixEnd = MixedFlowStationState.FromJetState(
                lastJet,
                vtEnd,
                compEnd.MachNumber,
                compEnd.TotalPressurePa,
                compEnd.TotalTemperatureK,
                "chamber end (jet fallback)");
        }

        SwirlEntranceCapacityResult atEnt = EvaluateAtStation(
            gas,
            mixEntrance,
            captureAreaM2,
            chamberBoreAreaM2,
            freeAnnulusEffectiveAreaM2,
            "swirl entrance plane",
            limits);
        SwirlEntranceCapacityResult atEnd = EvaluateAtStation(
            gas,
            mixEnd,
            captureAreaM2,
            chamberBoreAreaM2,
            freeAnnulusEffectiveAreaM2,
            "end of swirl chamber",
            limits);

        double dMach = Math.Abs(atEnt.MachRequired - atEnd.MachRequired);
        var combined = SwirlEntranceCapacityDualResult.Worst(atEnt.Classification, atEnd.Classification);
        string govLabel = atEnt.MachRequired >= atEnd.MachRequired
            ? "swirl entrance plane"
            : "end of swirl chamber";
        bool diverge = dMach >= SwirlEntranceCapacityDualResult.MachDivergenceWarningThreshold
                       || atEnt.Classification != atEnd.Classification;

        return new SwirlEntranceCapacityDualResult
        {
            EntrancePlane = atEnt,
            ChamberEnd = atEnd,
            CombinedClassification = combined,
            GoverningStationLabel = govLabel,
            MachAbsoluteDelta = dMach,
            StationsDivergeSignificantly = diverge
        };
    }
}
