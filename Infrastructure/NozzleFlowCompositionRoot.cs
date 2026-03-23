using System;
using System.Collections.Generic;
using System.Linq;
using PicoGK;
using PicoGK_Run.Core;
using PicoGK_Run.Geometry;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Infrastructure;

/// <summary>Outputs from <see cref="NozzleFlowCompositionRoot.EvaluateSiPathForValidation"/> (SI only).</summary>
internal sealed record SiPathValidationPack(
    NozzleSolvedState Solved,
    SiFlowDiagnostics SiDiag,
    NozzleCriticalRatiosSnapshot CriticalRatios,
    IReadOnlyList<string> HealthMessages);

/// <summary>
/// Wires SI physics → design result → mm geometry → <see cref="PipelineRunResult"/>.
/// </summary>
public static class NozzleFlowCompositionRoot
{
    private const int DefaultMarchSteps = 24;

    private sealed record SiPathSolveResult(
        NozzleDesignInputs DrivenDesign,
        NozzleSolvedState Solved,
        SiFlowDiagnostics SiDiag,
        NozzleCriticalRatiosSnapshot CriticalRatios,
        IReadOnlyList<string> HealthMessages,
        NozzleDesignResult DesignResult,
        JetState InletState);

    /// <summary>
    /// Same SI path as <see cref="Run"/> up to health check — no voxels, no viewer, no console summary.
    /// <paramref name="run"/> reserved for future tuning hooks (march resolution, etc.); currently unused.
    /// </summary>
    internal static FlowTuneEvaluation EvaluateDesignForTuning(
        SourceInputs source,
        NozzleDesignInputs candidateDesign,
        RunConfiguration run)
    {
        SiPathSolveResult r = SolveSiPath(source, candidateDesign, run);
        int hard = r.HealthMessages.Count(m => m.StartsWith("DESIGN ERROR", StringComparison.Ordinal));
        return new FlowTuneEvaluation
        {
            CandidateDesign = candidateDesign,
            DrivenDesign = r.DrivenDesign,
            EntrainmentRatio = r.Solved.EntrainmentRatio,
            NetThrustN = r.SiDiag.NetThrustN,
            SourceOnlyThrustN = r.Solved.SourceOnlyThrustN,
            VortexQualityMetric = r.SiDiag.Chamber?.TuningCompositeQuality ?? r.SiDiag.Vortex?.VortexQualityMetric ?? 0.0,
            PhysicsMetrics = FlowTunePhysicsMetrics.FromChamber(r.SiDiag.Chamber, r.SiDiag.FinalAxialVelocityMps),
            AmbientAirMassFlowKgS = r.Solved.AmbientAirMassFlowKgPerSec,
            CoreMassFlowKgS = r.Solved.CoreMassFlowKgPerSec,
            HealthCount = r.HealthMessages.Count,
            HasDesignError = hard > 0,
            HealthMessages = r.HealthMessages is List<string> list ? list : new List<string>(r.HealthMessages),
            Score = 0.0
        };
    }

    public static PipelineRunResult Run(NozzleInput input, bool showInViewer)
    {
        NozzleDesignInputs activeDesign = input.Run.UsePhysicsInformedGeometry
            ? NozzleGeometrySynthesis.Synthesize(input.Source, input.Design)
            : input.Design;

        SiPathSolveResult path = SolveSiPath(input.Source, activeDesign, input.Run);

        var geometryBuilder = new NozzleGeometryBuilder();
        NozzleGeometryResult geometry = geometryBuilder.Build(path.DrivenDesign, path.Solved);

        if (showInViewer)
            AppPipeline.DisplayGeometryInViewer(geometry);

        PrintPhysicsSummary(path.InletState, path.DesignResult, path.SiDiag);

        var warnings = new List<string>
        {
            "SI path: compressible entrainment march + first-order stator/expander bookkeeping (not CFD)."
        };
        if (input.Run.UsePhysicsInformedGeometry)
            warnings.Add("Geometry pre-sized by NozzleGeometrySynthesis (first-order rules from K320-class source + swirl/ER heuristics; not a numerical optimizer).");
        warnings.AddRange(path.HealthMessages);

        NozzleInput effectiveInput = new NozzleInput(input.Source, path.DrivenDesign, input.Run);
        return new PipelineRunResult(effectiveInput, path.Solved, geometry, warnings, path.SiDiag, path.CriticalRatios);
    }

    private static SiPathSolveResult SolveSiPath(
        SourceInputs source,
        NozzleDesignInputs activeDesign,
        RunConfiguration? runConfiguration = null)
    {
        RunConfiguration run = runConfiguration ?? new RunConfiguration();
        var gas = new GasProperties();
        var ambient = new AmbientAir(
            gas,
            source.AmbientPressurePa,
            source.AmbientTemperatureK,
            velocityMps: 0.0);

        double pTotal = Math.Max(source.AmbientPressurePa * source.PressureRatio, ambient.PressurePa + 1.0);
        double pStaticJet = source.AmbientPressurePa;
        double tTotal = source.ExhaustTemperatureK ?? source.AmbientTemperatureK;
        double exitAreaM2 = source.SourceOutletAreaMm2 / 1e6;

        var jetSource = new JetSource(
            gas,
            totalPressurePa: pTotal,
            staticPressurePa: pStaticJet,
            totalTemperatureK: tTotal,
            exitAreaM2: exitAreaM2,
            primaryMassFlowKgS: source.MassFlowKgPerSec);

        JetState rawInlet = jetSource.CreateInitialState();

        double sourceAreaMm2 = source.SourceOutletAreaMm2;
        double injectorAreaMm2 = Math.Max(activeDesign.TotalInjectorAreaMm2, 1e-9);
        double sourceAreaM2 = sourceAreaMm2 / 1e6;
        double injectorAreaM2 = injectorAreaMm2 / 1e6;
        double coreMdot = source.MassFlowKgPerSec;
        double vCore = source.SourceVelocityMps > 0.0
            ? source.SourceVelocityMps
            : VelocityMath.FromMassFlow(coreMdot, source.AmbientDensityKgPerM3, sourceAreaM2);
        double rhoCore = rawInlet.DensityKgM3;
        double areaDriver = vCore * (sourceAreaMm2 / injectorAreaMm2);
        double continuityCheck = coreMdot / (rhoCore * Math.Max(injectorAreaM2, 1e-12));
        double injectorJetVelocityRaw = NozzlePhysicsSolver.InjectorJetVelocityDriverBlend * areaDriver
            + (1.0 - NozzlePhysicsSolver.InjectorJetVelocityDriverBlend) * continuityCheck;

        var (vtRaw, vaRaw) = SwirlMath.ResolveInjectorComponents(
            injectorJetVelocityRaw,
            activeDesign.InjectorYawAngleDeg,
            activeDesign.InjectorPitchAngleDeg);

        // Coupled injector: V_eff = Cd·V·√max(0,1−K_turn); then same yaw/pitch decomposition (linear in |V|).
        double injectorJetVelocityEffective = InjectorLossModel.EffectiveJetVelocityMps(
            injectorJetVelocityRaw,
            rhoCore,
            activeDesign.InjectorYawAngleDeg);

        var (vt0, va0) = SwirlMath.ResolveInjectorComponents(
            injectorJetVelocityEffective,
            activeDesign.InjectorYawAngleDeg,
            activeDesign.InjectorPitchAngleDeg);

        // Pre-march radial picture on effective swirl — bounded entrainment boost (does not add free static head in march).
        double rWallM = 0.5e-3 * Math.Max(activeDesign.SwirlChamberDiameterMm, 1.0);
        var radialPreMarch = RadialVortexPressureModel.Compute(
            rhoCore,
            Math.Abs(vt0),
            rWallM,
            ChamberPhysicsCoefficients.RadialCoreRadiusFractionOfWall,
            ChamberPhysicsCoefficients.RadialPressureCapPa);
        double deltaPCoreUseful = Math.Clamp(
            radialPreMarch.CorePressureDropPa,
            0.0,
            ChamberPhysicsCoefficients.CouplingInletCorePressureUseCapPa);
        double entrainmentBoostRaw = 1.0
            + ChamberPhysicsCoefficients.CouplingVortexEntrainmentC * deltaPCoreUseful / Math.Max(ambient.PressurePa, 1.0);
        double vMagInj = Math.Sqrt(va0 * va0 + vt0 * vt0);
        double dynHeadRatio = 0.5 * rhoCore * vMagInj * vMagInj / Math.Max(ambient.PressurePa, 1.0);
        double boostCapDynamic = 1.0 + ChamberPhysicsCoefficients.CouplingVortexEntrainmentDynamicHeadGamma * dynHeadRatio;
        double boostCapAbsolute = ChamberPhysicsCoefficients.CouplingVortexEntrainmentBoostMax;
        double entrainmentBoost = Math.Min(entrainmentBoostRaw, Math.Min(boostCapDynamic, boostCapAbsolute));
        entrainmentBoost = Math.Max(1.0, entrainmentBoost);
        if (!run.UseSwirlEntrainmentBoost)
            entrainmentBoost = 1.0;

        double aChamberBoreMm2 = SwirlChamberMarchGeometry.ChamberBoreAreaMm2(activeDesign.SwirlChamberDiameterMm);
        double aHubMm2 = SwirlChamberMarchGeometry.HubDiskAreaMm2(activeDesign.StatorHubDiameterMm);
        double aFreeChamberMm2 = SwirlChamberMarchGeometry.EffectiveGasAreaMm2(
            activeDesign.SwirlChamberDiameterMm,
            activeDesign.StatorHubDiameterMm,
            run.ChamberVaneBlockageFractionOfAnnulus);
        double aEffM2 = Math.Max(aFreeChamberMm2 * 1e-6, 1e-10);
        double perimeterMarchM = SwirlChamberMarchGeometry.EntrainmentPerimeterM(
            activeDesign.SwirlChamberDiameterMm,
            activeDesign.StatorHubDiameterMm);
        double aCaptureM2 = SwirlChamberMarchGeometry.CaptureAreaM2(activeDesign, run, aFreeChamberMm2);

        JetState inletState = new JetState(
            axialPositionM: 0.0,
            pressurePa: rawInlet.PressurePa,
            temperatureK: rawInlet.TemperatureK,
            densityKgM3: rawInlet.DensityKgM3,
            velocityMps: va0,
            areaM2: aEffM2,
            primaryMassFlowKgS: rawInlet.MassFlowKgS,
            entrainedMassFlowKgS: 0.0);

        var entrainment = new EntrainmentModel();
        var mixing = new MixingSectionSolver();
        var marcher = new FlowMarcher(ambient, entrainment, mixing, gas);

        double sectionLengthM = activeDesign.SwirlChamberLengthMm / 1000.0;
        double outletAreaM2 = Math.PI * Math.Pow(activeDesign.ExitDiameterMm / 2000.0, 2);

        // Chamber march: constant effective annulus (CAD bore − hub − vane blockage) and real wetted perimeter.
        double AreaAt(double x) => aEffM2;
        double PerimeterAt(double x) => perimeterMarchM;
        double CaptureAreaAt(double x) => aCaptureM2;

        double ldRatio = activeDesign.SwirlChamberLengthMm / Math.Max(activeDesign.SwirlChamberDiameterMm, 1e-6);
        double sInjPre = Math.Abs(vt0) / Math.Max(Math.Abs(va0), 1e-6); // effective injector swirl
        double preBreak = SwirlDecayModel.PreMarchBreakdownRisk(
            sInjPre,
            ldRatio,
            activeDesign.InjectorAxialPositionRatio);

        double erHint = Math.Clamp(
            0.32 + 0.28 * Math.Tanh((activeDesign.ExitDiameterMm / Math.Max(activeDesign.SwirlChamberDiameterMm, 1.0) - 1.05) * 1.8),
            0.22,
            1.35);

        double kTotal = SwirlDecayModel.ComputeKTotal(
            activeDesign.SwirlChamberLengthMm,
            activeDesign.SwirlChamberDiameterMm,
            vt0,
            va0,
            erHint,
            activeDesign.InjectorAxialPositionRatio,
            preBreak);

        double chamberDM = activeDesign.SwirlChamberDiameterMm * 1e-3;
        double swirlDecayPerStep = SwirlDecayModel.DecayPerStepFromK(kTotal, sectionLengthM, chamberDM, DefaultMarchSteps);

        FlowMarchDetailedResult detailed = marcher.SolveDetailed(
            inletState,
            sectionLengthM,
            DefaultMarchSteps,
            AreaAt,
            PerimeterAt,
            CaptureAreaAt,
            primaryTangentialVelocityMps: vt0,
            swirlDecayPerStepFactor: swirlDecayPerStep,
            entrainmentMassDemandMultiplier: entrainmentBoost,
            chamberLdRatio: ldRatio,
            chamberDiameterMm: activeDesign.SwirlChamberDiameterMm,
            useReynoldsOnEntrainmentCe: run.UseReynoldsEntrainmentFactor);

        SwirlChamberMarchDiagnostics chamberMarchDiag = BuildSwirlChamberMarchDiagnostics(
            activeDesign,
            entrainment,
            aChamberBoreMm2,
            aHubMm2,
            aFreeChamberMm2,
            aEffM2,
            perimeterMarchM,
            aCaptureM2,
            detailed,
            entrainmentBoost);

        IReadOnlyList<FlowMarchStepResult> steps = detailed.StepResults;
        JetState lastMarch = detailed.FlowStates[^1];

        double etaStatorBase = Math.Min(0.42, activeDesign.StatorVaneAngleDeg / 95.0);
        double fracVt = Math.Clamp(1.0 - 0.38 * etaStatorBase, 0.22, 0.92);

        double impliedYawDeg = Math.Atan2(
            Math.Abs(detailed.FinalTangentialVelocityMps),
            Math.Max(Math.Abs(lastMarch.VelocityMps), 1e-6)) * (180.0 / Math.PI);

        double rStatorOuterMm = NozzleGeometryMetrics.ExpanderEndInnerRadiusMm(activeDesign);
        HubStatorRecoveryContext hubCtx = HubStatorFirstOrderModel.ComputeContext(
            activeDesign,
            rStatorOuterMm,
            activeDesign.StatorVaneAngleDeg,
            impliedYawDeg);

        var stLossPre = StatorLossModel.Compute(
            Math.Max(lastMarch.DensityKgM3, 1e-6),
            lastMarch.VelocityMps,
            detailed.FinalTangentialVelocityMps,
            activeDesign.StatorVaneAngleDeg,
            impliedYawDeg);

        // η_stator,eff = η_base · clamp(1 − w_i·K_inc − w_t·K_turn, η_floor, 1); K_inc = tanh(mismatch/ref), K_turn from model.
        double kInc = Math.Tanh(
            stLossPre.IncidenceMismatchDeg / Math.Max(ChamberPhysicsCoefficients.StatorIncidenceRefDeg, 1.0));
        double kTurn = Math.Clamp(stLossPre.TurningLossK, 0.0, 0.55);
        double etaFactor = Math.Clamp(
            1.0
            - ChamberPhysicsCoefficients.StatorCouplingKIncidenceWeight * kInc
            - ChamberPhysicsCoefficients.StatorCouplingKTurnWeight * kTurn,
            ChamberPhysicsCoefficients.StatorCouplingEtaFactorFloor,
            1.0);
        double etaStatorEff = Math.Min(
            etaStatorBase * etaFactor * hubCtx.HubGeometryRecoveryFactor,
            ChamberPhysicsCoefficients.HubStatorMaxEtaCap);

        var stator = new StatorRecoveryModel();
        StatorRecoveryOutput statorOut = stator.Apply(
            detailed.FinalTangentialVelocityMps,
            lastMarch.DensityKgM3,
            etaStatorEff,
            fracVt);

        HubStatorFlowDiagnostics hubDiag = HubStatorFirstOrderModel.BuildDiagnostics(
            hubCtx,
            activeDesign,
            etaStatorEff,
            detailed.FinalTangentialVelocityMps,
            statorOut.RemainingTangentialVelocityMps,
            lastMarch.VelocityMps,
            statorOut.AxialVelocityGainMps);

        // Swirling diffuser / expander: scale wall ΔP by recovery model (same inputs as chamber pipeline).
        double injectorSwirlSimple = Math.Abs(vt0) / Math.Max(Math.Abs(va0), 1e-6);
        var diffuserCoupling = SwirlDiffuserRecoveryModel.Compute(
            activeDesign.ExpanderHalfAngleDeg,
            activeDesign.ExpanderLengthMm,
            activeDesign.SwirlChamberDiameterMm,
            activeDesign.ExitDiameterMm,
            Math.Max(lastMarch.DensityKgM3, 1e-6),
            lastMarch.VelocityMps,
            injectorSwirlSimple,
            detailed.FinalTangentialVelocityMps);

        double diffuserRecoveryMult = Math.Clamp(
            diffuserCoupling.EffectivePressureRecoveryEfficiency
            / Math.Max(ChamberPhysicsCoefficients.DiffuserCouplingReferenceEfficiency, 0.05),
            ChamberPhysicsCoefficients.DiffuserCouplingScaleMin,
            ChamberPhysicsCoefficients.DiffuserCouplingScaleMax);

        double halfRad = activeDesign.ExpanderHalfAngleDeg * (Math.PI / 180.0);
        double rhoExp = lastMarch.DensityKgM3;
        double vaExp = lastMarch.VelocityMps;
        double dPExpanderBase = 0.22 * rhoExp * vaExp * vaExp * Math.Sin(Math.Max(halfRad, 0.02));
        dPExpanderBase = Math.Min(dPExpanderBase, 0.48 * rhoExp * vaExp * vaExp);
        double dPExpanderEff = dPExpanderBase * diffuserRecoveryMult;
        double expanderProjectedAreaM2 = ExpanderAxialProjectedAreaM2(activeDesign);
        double expanderForceN = PressureForceMath.ExpanderOverPressureAxialForce(dPExpanderEff, expanderProjectedAreaM2);

        double pAfterStator = Math.Max(lastMarch.PressurePa + statorOut.RecoveredPressureRisePa, 1.0);
        double vaAfterStatorBase = lastMarch.VelocityMps + statorOut.AxialVelocityGainMps;
        double sepAxialFactor = Math.Clamp(
            1.0 - ChamberPhysicsCoefficients.CouplingDiffuserSeparationAxialPenaltyK * diffuserCoupling.SeparationRiskScore,
            ChamberPhysicsCoefficients.CouplingDiffuserSeparationAxialFloor,
            1.0);
        double vaAfterStator = Math.Max(vaAfterStatorBase * sepAxialFactor, 1e-6);
        double vtAfterStator = statorOut.RemainingTangentialVelocityMps;
        double rhoAfterStator = gas.Density(pAfterStator, Math.Max(lastMarch.TemperatureK, 1.0));

        var finalOutlet = new JetState(
            lastMarch.AxialPositionM,
            pAfterStator,
            lastMarch.TemperatureK,
            rhoAfterStator,
            vaAfterStator,
            lastMarch.AreaM2,
            lastMarch.MassFlowKgS,
            lastMarch.EntrainedMassFlowKgS);

        var flowStates = new List<JetState>(detailed.FlowStates);
        flowStates[^1] = finalOutlet;

        SwirlEnergyCouplingLedger swirlLedger = SwirlEnergyCouplingLedger.Build(
            coreMdot,
            vtRaw,
            vt0,
            detailed.FinalPrimaryTangentialVelocityMps,
            lastMarch.TotalMassFlowKgS,
            detailed.FinalTangentialVelocityMps,
            vtAfterStator,
            diffuserRecoveryMult,
            ChamberPhysicsCoefficients.SwirlLedgerDiffuserBookkeepingK);

        var couplingDiag = new SiVortexCouplingDiagnostics
        {
            InjectorJetVelocityRawMps = injectorJetVelocityRaw,
            InjectorJetVelocityEffectiveMps = injectorJetVelocityEffective,
            InjectorVtRawMps = vtRaw,
            InjectorVaRawMps = vaRaw,
            InjectorVtEffectiveMps = vt0,
            InjectorVaEffectiveMps = va0,
            EntrainmentDemandBoostFactor = entrainmentBoost,
            DeltaPCoreUsefulForEntrainmentPa = deltaPCoreUseful,
            StatorEtaBase = etaStatorBase,
            StatorEtaEffective = etaStatorEff,
            StatorCouplingKIncidence = kInc,
            StatorCouplingKTurn = kTurn,
            ExpanderDeltaPBasePa = dPExpanderBase,
            ExpanderDeltaPEffectivePa = dPExpanderEff,
            DiffuserRecoveryMultiplier = diffuserRecoveryMult,
            DiffuserSeparationAxialFactor = sepAxialFactor,
            FinalAxialVelocityBaseMps = vaAfterStatorBase,
            FinalAxialVelocityEffectiveMps = vaAfterStator,
            SwirlEnergy = swirlLedger,
            CouplingSummaryLines = BuildCouplingSummaryLines(
                injectorJetVelocityRaw,
                injectorJetVelocityEffective,
                diffuserCoupling,
                diffuserRecoveryMult,
                entrainmentBoost,
                swirlLedger,
                etaStatorBase,
                etaStatorEff)
        };

        double inletPressureForceN = steps.Sum(s => s.PressureForceN);
        double minInletP = steps.Count > 0 ? steps.Min(s => s.InletLocalPressurePa) : ambient.PressurePa;
        double maxInletMach = steps.Count > 0 ? steps.Max(s => s.InletMach) : 0.0;
        bool anyChoked = steps.Any(s => s.InletIsChoked);
        double sumReq = steps.Sum(s => s.RequestedDeltaEntrainedMassFlowKgS);
        double sumAct = steps.Sum(s => s.DeltaEntrainedMassFlowKgS);
        double shortfall = Math.Max(0.0, sumReq - sumAct);

        double mdotExit = finalOutlet.TotalMassFlowKgS;
        double fMom = mdotExit * (finalOutlet.VelocityMps - ambient.VelocityMps);
        double fExitPlane = (finalOutlet.PressurePa - ambient.PressurePa) * finalOutlet.AreaM2;
        double fPressureTotal = fExitPlane + inletPressureForceN + expanderForceN;
        double fNet = fMom + fPressureTotal;

        double solvedEr = coreMdot > 1e-12
            ? Math.Max(0.0, (lastMarch.TotalMassFlowKgS - coreMdot) / coreMdot)
            : 0.0;

        ChamberFirstOrderPhysics chamber = ChamberPhysicsPipeline.Build(
            activeDesign,
            source,
            injectorJetVelocityRaw,
            vt0,
            va0,
            swirlDecayPerStep,
            DefaultMarchSteps,
            kTotal,
            detailed,
            lastMarch,
            statorOut,
            etaStatorEff,
            fracVt,
            steps,
            minInletP,
            sumReq,
            sumAct,
            shortfall,
            solvedEr,
            vtAfterStator);

        VortexFlowDiagnostics vortex = ChamberPhysicsPipeline.ToLegacyVortexDiagnostics(chamber, swirlDecayPerStep);

        InjectorPressureVelocityDiagnostics injectorPressureVelocity = InjectorPressureVelocityDiagnostics.Compute(
            source,
            ambient.PressurePa,
            pTotal,
            pStaticJet,
            rawInlet.DensityKgM3,
            injectorJetVelocityRaw,
            injectorJetVelocityEffective,
            va0,
            vt0,
            activeDesign.InjectorYawAngleDeg,
            steps,
            sectionLengthM,
            activeDesign.InjectorAxialPositionRatio,
            chamber.RadialPressure,
            inletState.PressurePa);

        var siDiag = new SiFlowDiagnostics
        {
            MarchSteps = steps,
            MinInletLocalStaticPressurePa = minInletP,
            MaxInletMach = maxInletMach,
            AnyEntrainmentStepChoked = anyChoked,
            SumRequestedEntrainmentIncrementsKgS = sumReq,
            SumActualEntrainmentIncrementsKgS = sumAct,
            EntrainmentShortfallSumKgS = shortfall,
            ExpanderAxialPressureForceN = expanderForceN,
            InletAxialPressureForceN = inletPressureForceN,
            StatorRecoveredPressureRisePa = statorOut.RecoveredPressureRisePa,
            FinalTangentialVelocityMps = vtAfterStator,
            FinalAxialVelocityMps = vaAfterStator,
            MomentumThrustN = fMom,
            PressureThrustN = fPressureTotal,
            NetThrustN = fNet,
            Vortex = vortex,
            Chamber = chamber,
            Coupling = couplingDiag,
            HubStator = hubDiag,
            InjectorPressureVelocity = injectorPressureVelocity,
            ChamberMarch = chamberMarchDiag
        };

        var designer = new NozzleDesigner();
        NozzleDesignResult designResult = designer.CreateDesignResult(
            inletState,
            flowStates,
            outletAreaM2,
            ambient.PressurePa,
            ambient.VelocityMps,
            siDiag);

        NozzleDesignInputs drivenDesign = FlowDrivenNozzleBuilder.BuildDesignInputs(designResult, activeDesign);

        NozzleSolvedState solved = NozzleSolvedStateFlowAdapter.FromSiFlow(
            designResult,
            inletState,
            finalOutlet,
            source,
            drivenDesign,
            areaDriver,
            continuityCheck,
            injectorJetVelocityRaw,
            siDiag);

        NozzleCriticalRatiosSnapshot criticalRatios = NozzleCriticalRatios.Compute(
            drivenDesign,
            source,
            solved,
            siDiag);

        IReadOnlyList<string> healthBase = NozzleDesignHealthCheck.Validate(drivenDesign, criticalRatios, siDiag);
        var health = new List<string>(healthBase.Count + (chamberMarchDiag.ValidationWarnings?.Count ?? 0));
        health.AddRange(healthBase);
        health.AddRange(chamberMarchDiag.ValidationWarnings ?? Array.Empty<string>());

        return new SiPathSolveResult(
            drivenDesign,
            solved,
            siDiag,
            criticalRatios,
            health,
            designResult,
            inletState);
    }

    /// <summary>
    /// Coupled SI solve only (no voxels/viewer) — for validation sweeps and tooling; same path as tuning eval.
    /// </summary>
    internal static SiPathValidationPack EvaluateSiPathForValidation(
        SourceInputs source,
        NozzleDesignInputs design,
        RunConfiguration? run = null)
    {
        SiPathSolveResult r = SolveSiPath(source, design, run);
        return new SiPathValidationPack(r.Solved, r.SiDiag, r.CriticalRatios, r.HealthMessages);
    }

    private static SwirlChamberMarchDiagnostics BuildSwirlChamberMarchDiagnostics(
        NozzleDesignInputs d,
        EntrainmentModel em,
        double aChamberBoreMm2,
        double aHubMm2,
        double aFreeChamberMm2,
        double aEffM2,
        double perimeterMarchM,
        double aCaptureM2,
        FlowMarchDetailedResult detailed,
        double entrainmentMassDemandBoost)
    {
        double aInletMm2 = SwirlChamberMarchGeometry.InletCaptureAreaMm2(d.InletDiameterMm);
        double aInjMm2 = Math.Max(d.TotalInjectorAreaMm2, 1e-9);
        double aExitMm2 = SwirlChamberMarchGeometry.ExitInnerAreaMm2(d.ExitDiameterMm);
        double aCh = Math.Max(aChamberBoreMm2, 1e-9);
        double ceFirst = detailed.StepResults.Count > 0
            ? detailed.StepResults[0].EntrainmentCeEffective
            : em.Coefficient;
        var warnings = new List<string>();
        if (aInjMm2 / aCh > 0.9)
            warnings.Add("WARNING (SI march): A_inj/A_chamber > 0.9 — port blockage may suppress entrainment.");
        double aInletM2 = aInletMm2 * 1e-6;
        if (aCaptureM2 < 0.5 * aInletM2)
            warnings.Add("WARNING (SI march): A_capture < 0.5×A_inlet — intake model may starve entrainment.");
        if (aExitMm2 / aCh > 3.0)
            warnings.Add("WARNING (SI march): A_exit/A_chamber > 3 — large expansion vs bore may limit useful coupling.");

        return new SwirlChamberMarchDiagnostics
        {
            AInletMm2 = aInletMm2,
            AChamberBoreMm2 = aChamberBoreMm2,
            AHubMm2 = aHubMm2,
            AFreeChamberMm2 = aFreeChamberMm2,
            AInjTotalMm2 = aInjMm2,
            AExitMm2 = aExitMm2,
            RatioInletToChamber = aInletMm2 / aCh,
            RatioInjToChamber = aInjMm2 / aCh,
            RatioFreeToChamber = aFreeChamberMm2 / aCh,
            RatioExitToChamber = aExitMm2 / aCh,
            DuctEffectiveAreaM2 = aEffM2,
            EntrainmentPerimeterM = perimeterMarchM,
            CaptureAreaM2 = aCaptureM2,
            EntrainmentCeBase = em.Coefficient,
            EntrainmentCeAtFirstStep = ceFirst,
            EntrainmentMassDemandBoost = entrainmentMassDemandBoost,
            ValidationWarnings = warnings
        };
    }

    private static IReadOnlyList<string> BuildCouplingSummaryLines(
        double vInRaw,
        double vInEff,
        SwirlDiffuserRecoveryResult diffuser,
        double diffuserRecoveryMult,
        double entrainmentBoost,
        SwirlEnergyCouplingLedger e,
        double etaStatorBase,
        double etaStatorEff)
    {
        var lines = new List<string>(4);
        if (vInEff < 0.97 * vInRaw)
            lines.Add("Coupled vortex physics reduced injector energy vs raw blend (Cd / turning loss).");
        if (diffuser.SeparationRiskScore > 0.48 && diffuserRecoveryMult < 0.92)
            lines.Add("Diffuser recovery limited by separation risk (ΔP_exp and axial factor scaled).");
        if (entrainmentBoost > 1.04)
            lines.Add("Core suction model moderately increased entrainment demand (bounded boost).");
        double statorDebit = e.EThetaUsedForStatorRecovery_W;
        double residual = e.EThetaExitResidual_W;
        if (statorDebit > 1e-6 && residual < 0.22 * (statorDebit + residual))
            lines.Add("Most tangential energy removed or dissipated before/at stator exit (first-order ledger).");
        if (etaStatorEff < 0.9 * etaStatorBase)
            lines.Add("Stator incidence/turning losses reduced effective recovery efficiency.");
        if (lines.Count == 0)
            lines.Add("Coupled physics: mild adjustments vs uncoupled baseline (see raw vs effective audit).");
        return lines;
    }

    private static double ExpanderAxialProjectedAreaM2(NozzleDesignInputs d)
    {
        double rCh = d.SwirlChamberDiameterMm * 0.5e-3;
        double rEx = d.ExitDiameterMm * 0.5e-3;
        double halfRad = d.ExpanderHalfAngleDeg * (Math.PI / 180.0);
        double ring = Math.PI * Math.Max(rEx * rEx - rCh * rCh, 0.0);
        return Math.Max(ring * Math.Sin(Math.Max(halfRad, 0.03)), 1e-9);
    }

    private static void PrintPhysicsSummary(JetState inlet, NozzleDesignResult d, SiFlowDiagnostics si)
    {
        Console.WriteLine("--- SI flow-driven nozzle summary (compressible entrainment path) ---");
        Console.WriteLine($"Inlet jet axial velocity [m/s]: {inlet.VelocityMps:F2}");
        Console.WriteLine($"Estimated exit velocity [m/s]: {d.EstimatedExitVelocityMps:F2}");
        Console.WriteLine($"Estimated total mass flow [kg/s]: {d.EstimatedTotalMassFlowKgS:F4}");
        Console.WriteLine($"Estimated thrust [N]:       {d.EstimatedThrustN:F2} (momentum + pressure CV terms, first-order)");
        Console.WriteLine($"Min inlet static (entrain.) [Pa]: {si.MinInletLocalStaticPressurePa:F1}");
        Console.WriteLine($"Max entrainment Mach [-]:   {si.MaxInletMach:F3}  Choked step: {si.AnyEntrainmentStepChoked}");
        Console.WriteLine($"Suggested inlet radius [m]: {d.SuggestedInletRadiusM:F5}");
        Console.WriteLine($"Suggested outlet radius [m]: {d.SuggestedOutletRadiusM:F5}");
        Console.WriteLine($"Suggested mixing length [m]: {d.SuggestedMixingLengthM:F5}");
        if (si.ChamberMarch != null)
        {
            SwirlChamberMarchDiagnostics m = si.ChamberMarch;
            Console.WriteLine("--- Swirl chamber SI march geometry (aligned with CAD bore/hub annulus) ---");
            Console.WriteLine(
                $"A_inlet {m.AInletMm2:F2} | A_chamber(bore) {m.AChamberBoreMm2:F2} | A_free_ch {m.AFreeChamberMm2:F2} | A_inj {m.AInjTotalMm2:F2} | A_exit {m.AExitMm2:F2} [mm2]");
            Console.WriteLine(
                $"Ratios A_in/A_ch {m.RatioInletToChamber:F3} | A_inj/A_ch {m.RatioInjToChamber:F3} | A_free/A_ch {m.RatioFreeToChamber:F3} | A_exit/A_ch {m.RatioExitToChamber:F3}");
            Console.WriteLine(
                $"Ce_base {m.EntrainmentCeBase:F4} | Ce@step1 {m.EntrainmentCeAtFirstStep:F4} | B_ent {m.EntrainmentMassDemandBoost:F3}");
            Console.WriteLine(
                $"A_capture {m.CaptureAreaM2:E4} m2 | P_ent {m.EntrainmentPerimeterM:F5} m | A_duct_eff {m.DuctEffectiveAreaM2:E4} m2 (constant along chamber march)");
            foreach (string w in m.ValidationWarnings)
                Console.WriteLine(w);
        }

        Console.WriteLine("---------------------------------------------------------------------");
    }
}
