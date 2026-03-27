using System;
using System.Collections.Generic;
using PicoGK_Run.Core;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Physics;
/// <summary>
/// Runs extended chamber physics after the compressible march — single place for full SI diagnostics.
/// </summary>
public static class ChamberPhysicsPipeline
{
    public static ChamberFirstOrderPhysics Build(
        NozzleDesignInputs design,
        SourceInputs source,
        double injectorJetVelocityMps,
        double vtInjector,
        double vaInjector,
        double injectorPlaneFluxSwirlNumber,
        double swirlDecayPerStep,
        int marchSteps,
        double kTotalUsed,
        FlowMarchDetailedResult detailed,
        JetState lastMarch,
        StatorRecoveryOutput statorOut,
        double etaStator,
        double statorFracVt,
        IReadOnlyList<FlowMarchStepResult> steps,
        double minInletStaticPa,
        double sumReq,
        double sumAct,
        double shortfall,
        double solvedEr,
        double vtAfterStator)
    {
        double rho = Math.Max(lastMarch.DensityKgM3, 1e-6);
        double coreMdot = detailed.FlowStates[0].MassFlowKgS;
        double rWallM = 0.5e-3 * Math.Max(design.SwirlChamberDiameterMm, 1.0);

        var inj = InjectorLossModel.Compute(rho, injectorJetVelocityMps, design.InjectorYawAngleDeg);

        var radial = RadialVortexPressureModel.Compute(
            rho,
            detailed.FinalTangentialVelocityMps,
            rWallM,
            ChamberPhysicsCoefficients.RadialCoreRadiusFractionOfWall,
            ChamberPhysicsCoefficients.RadialPressureCapPa);

        var budget = SwirlDecayModel.BuildBudget(
            vtInjector,
            swirlDecayPerStep,
            marchSteps,
            coreMdot,
            lastMarch.TotalMassFlowKgS,
            detailed.FinalTangentialVelocityMps,
            vtAfterStator,
            solvedEr,
            kTotalUsed);

        double vtPriEnd = Math.Abs(vtInjector) * Math.Pow(swirlDecayPerStep, marchSteps);
        double decayFrac = Math.Abs(vtInjector) > 1e-9
            ? Math.Clamp(1.0 - (vtPriEnd * vtPriEnd) / (vtInjector * vtInjector), 0.0, 1.0)
            : 0.0;

        double ke0 = vtInjector * vtInjector;
        double kePre = detailed.FinalTangentialVelocityMps * detailed.FinalTangentialVelocityMps;
        double remRatio = ke0 > 1e-12 ? Math.Clamp(kePre / ke0, 0.0, 2.5) : 0.0;
        double remSwirlFrac = Math.Sqrt(Math.Min(remRatio, 1.0));

        double ld = design.SwirlChamberLengthMm / Math.Max(design.SwirlChamberDiameterMm, 1e-6);

        var structure = VortexStructureModel.Compute(
            vtInjector,
            vaInjector,
            injectorPlaneFluxSwirlNumber,
            ld,
            design.InjectorAxialPositionRatio,
            design.ExpanderHalfAngleDeg,
            radial.CorePressureDropPa,
            radial.EstimatedRadialPressureDeltaPa,
            remSwirlFrac,
            solvedEr,
            lastMarch.VelocityMps,
            decayFrac);

        double swirlCorr = detailed.StepPhysicsStates.Count > 0
            ? detailed.StepPhysicsStates[^1].SwirlNumberFlux
            : Math.Max(Math.Abs(injectorPlaneFluxSwirlNumber), 1e-12);
        var diffuser = SwirlDiffuserRecoveryModel.Compute(
            design.ExpanderHalfAngleDeg,
            design.ExpanderLengthMm,
            design.SwirlChamberDiameterMm,
            design.ExitDiameterMm,
            rho,
            lastMarch.VelocityMps,
            swirlCorr,
            detailed.FinalTangentialVelocityMps);

        double impliedYawDeg = Math.Atan2(
            Math.Abs(detailed.FinalTangentialVelocityMps),
            Math.Max(Math.Abs(lastMarch.VelocityMps), 1e-6)) * (180.0 / Math.PI);

        var stLoss = StatorLossModel.Compute(
            rho,
            lastMarch.VelocityMps,
            detailed.FinalTangentialVelocityMps,
            design.StatorVaneAngleDeg,
            impliedYawDeg);

        var ej = EjectorRegimeModel.Compute(
            steps,
            source.PressureRatio,
            source.AmbientPressurePa,
            minInletStaticPa,
            shortfall,
            sumReq,
            sumAct);

        // Four-bucket swirl split (normalized) — aligned with legacy reporting
        double fDiss = decayFrac;
        double fEnt = Math.Clamp(0.10 + 0.28 * Math.Tanh(solvedEr / 1.35) * (0.55 + 0.45 * (1.0 - design.InjectorAxialPositionRatio)), 0.05, 0.48);
        double axialGain = Math.Max(0.0, statorOut.AxialVelocityGainMps);
        double fRec = Math.Clamp(
            0.12 * etaStator
            + 0.55 * (axialGain / Math.Max(Math.Abs(vtInjector), 1e-6)) * statorFracVt
            + 0.08 * Math.Tanh(statorOut.RecoveredPressureRisePa / Math.Max(rho * vtInjector * vtInjector, 1e3)),
            0.02,
            0.55);
        double fRem = Math.Max(0.0, 1.0 - fDiss - fEnt - fRec);
        double s4 = fDiss + fEnt + fRec + fRem;
        if (s4 > 1e-12)
        {
            fDiss /= s4;
            fEnt /= s4;
            fRec /= s4;
            fRem /= s4;
        }

        double dynRef = 0.5 * rho * Math.Max(injectorJetVelocityMps * injectorJetVelocityMps, 1.0);
        double lossNorm = Math.Clamp((inj.EstimatedTotalPressureLossPa + stLoss.EstimatedTotalPressureLossPa) / Math.Max(dynRef, 1.0), 0.0, 6.0) / 6.0;

        double radialUsefulNorm = Math.Clamp(
            (radial.WallPressureRisePa + radial.CorePressureDropPa) / 70_000.0,
            0.0,
            1.4);

        double remSwirl01 = remSwirlFrac;

        double tuningQ = Math.Clamp(
            0.42 * structure.CompositeVortexQuality
            + 0.28 * radialUsefulNorm
            + 0.18 * remSwirlFrac
            - 0.22 * structure.BreakdownRiskScore
            - 0.18 * diffuser.SeparationRiskScore
            - 0.14 * lossNorm
            - 0.12 * ej.RegimeScore,
            0.0,
            1.0);

        string interp = BuildInterpretation(structure, diffuser, ej, solvedEr, tuningQ);

        return new ChamberFirstOrderPhysics
        {
            RadialPressure = radial,
            VortexStructure = structure,
            SwirlBudget = budget,
            DiffuserRecovery = diffuser,
            InjectorLoss = inj,
            StatorLoss = stLoss,
            EjectorRegime = ej,
            InterpretationSummary = interp,
            TuningCompositeQuality = tuningQ,
            FracSwirlDissipated = fDiss,
            FracSwirlForEntrainment = fEnt,
            FracSwirlToAxialRecovery = fRec,
            FracSwirlRemainingAtStator = fRem,
            NormalizedTotalPressureLoss01 = lossNorm,
            RadialPressureUsefulNorm = radialUsefulNorm,
            RecoverableSwirlFraction01 = remSwirl01
        };
    }

    public static VortexFlowDiagnostics ToLegacyVortexDiagnostics(
        ChamberFirstOrderPhysics ch,
        double swirlDecayPerStep)
    {
        var vs = ch.VortexStructure;
        var rp = ch.RadialPressure;
        var bu = ch.SwirlBudget;

        double decayFrac = Math.Abs(bu.SwirlInjectedVtMps) > 1e-9
            ? Math.Clamp(
                1.0 - (bu.SwirlAfterChamberDecayVtPrimaryMps * bu.SwirlAfterChamberDecayVtPrimaryMps)
                     / (bu.SwirlInjectedVtMps * bu.SwirlInjectedVtMps),
                0.0,
                1.0)
            : 0.0;

        return new VortexFlowDiagnostics
        {
            CorePressureDepressionPa = rp.CorePressureDropPa,
            WallPressureRisePa = rp.WallPressureRisePa,
            SwirlDecayFractionAlongChamber = decayFrac,
            RemainingSwirlFractionAtStator = Math.Sqrt(Math.Clamp(
                (bu.SwirlMixedAtChamberEndVtMps * bu.SwirlMixedAtChamberEndVtMps)
                / Math.Max(bu.SwirlInjectedVtMps * bu.SwirlInjectedVtMps, 1e-12),
                0.0,
                1.0)),
            FractionSwirlForEntrainment = ch.FracSwirlForEntrainment,
            FractionSwirlRemainingAtStator = ch.FracSwirlRemainingAtStator,
            FractionSwirlToAxialRecovery = ch.FracSwirlToAxialRecovery,
            FractionSwirlDissipated = ch.FracSwirlDissipated,
            StructureClass = MapStabilityToLegacy(vs.Classification),
            StructureClassLabel = vs.ClassificationLabel,
            VortexQualityMetric = ch.TuningCompositeQuality,
            SwirlDecayPerStepFactorUsed = swirlDecayPerStep
        };
    }

    private static VortexStructureClass MapStabilityToLegacy(VortexStabilityClassification c) => c switch
    {
        VortexStabilityClassification.StableLow => VortexStructureClass.FreeVortexDominated,
        VortexStabilityClassification.StableUseful => VortexStructureClass.MixedVortex,
        VortexStabilityClassification.StrongSwirl => VortexStructureClass.ForcedVortexDominated,
        VortexStabilityClassification.BreakdownRisk => VortexStructureClass.PossibleBreakdownOrUnstable,
        VortexStabilityClassification.LikelyUnstable => VortexStructureClass.PossibleBreakdownOrUnstable,
        _ => VortexStructureClass.MixedVortex
    };

    private static string BuildInterpretation(
        VortexStructureDiagnosticsResult s,
        SwirlDiffuserRecoveryResult d,
        EjectorRegimeResult e,
        double er,
        double tuningQ)
    {
        string a = s.Classification switch
        {
            VortexStabilityClassification.StrongSwirl when er > 0.45 => "Strong but recoverable vortex; check diffuser separation.",
            VortexStabilityClassification.BreakdownRisk => "Over-swirled or compact L/D — breakdown risk rising (model).",
            VortexStabilityClassification.LikelyUnstable => "Likely unstable / recirculation risk in this 1-D view.",
            VortexStabilityClassification.StableUseful when d.SeparationRiskScore < 0.42 => "Healthy pre-CFD candidate for controlled vortex + entrainment.",
            VortexStabilityClassification.StableUseful => "Useful vortex; watch diffuser separation.",
            VortexStabilityClassification.StableLow when er < 0.25 => "Weak swirl; entrainment may be limited.",
            _ => "Mixed regime — review metrics and move to experiment/CFD."
        };

        string b = d.SeparationRiskScore > 0.55 ? " Likely diffuser separation risk." : "";
        string c = e.Regime == EjectorOperatingRegime.CompoundChokingRisk || e.Regime == EjectorOperatingRegime.SecondaryChokeLimited
            ? " Entrainment may be choke-limited."
            : "";

        return a + b + c + $" [tuning composite≈{tuningQ:F2}]";
    }
}
