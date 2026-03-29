using System;
using System.Collections.Generic;
using System.Linq;
using PicoGK;
using PicoGK_Run.Core;
using PicoGK_Run.Geometry;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;
using PicoGK_Run.Physics.Continuous;
using PicoGK_Run.Physics.Reports;
using PicoGK_Run.Physics.Solvers;

namespace PicoGK_Run.Infrastructure.Services;

/// <summary>Compressible SI path from active design to driven design + diagnostics (solve-only; no voxels).</summary>
internal static class PhysicsSiPathService
{
    public const int DefaultMarchSteps = 24;

    public static SiPathSolveResult Solve(
            SourceInputs source,
            NozzleDesignInputs activeDesign,
            RunConfiguration? runConfiguration = null)
        {
            RunConfiguration run = runConfiguration ?? new RunConfiguration();
            double yawPhysicsDeg = run.LockInjectorYawTo90Degrees ? 90.0 : activeDesign.InjectorYawAngleDeg;
            var gas = new GasProperties();
            var ambient = new AmbientAir(
                gas,
                source.AmbientPressurePa,
                source.AmbientTemperatureK,
                velocityMps: 0.0);
    
            SourceDischargeConsistencyReport dischargeReport = SourceDischargePhase_Evaluate(source, gas, ambient.PressurePa);
            SourceDischargePhase_Log(dischargeReport, run.SiVerbosityLevel);
    
            double exitAreaM2 = source.SourceOutletAreaMm2 / 1e6;
            double coreMdot = source.MassFlowKgPerSec;
            double p0Implied = dischargeReport.P0ImpliedFromDerivedStatePa;
            double tTotalForDiagnostics = dischargeReport.DerivedTotalTemperatureK;
            double pStaticDerived = dischargeReport.DerivedStaticPressurePa;
    
            JetState rawInlet = new JetState(
                axialPositionM: 0.0,
                pressurePa: pStaticDerived,
                temperatureK: dischargeReport.DerivedStaticTemperatureK,
                densityKgM3: dischargeReport.DerivedDensityKgM3,
                velocityMps: dischargeReport.VelocityUsedMps,
                areaM2: exitAreaM2,
                primaryMassFlowKgS: coreMdot,
                entrainedMassFlowKgS: 0.0);
    
            double sourceAreaMm2 = source.SourceOutletAreaMm2;
            double injectorAreaMm2 = Math.Max(activeDesign.TotalInjectorAreaMm2, 1e-9);
            double injectorAreaM2 = injectorAreaMm2 / 1e6;
            double vCore = dischargeReport.VelocityUsedMps;
            double rhoCore = rawInlet.DensityKgM3;
            double areaDriverDiagnostic = vCore * (sourceAreaMm2 / injectorAreaMm2);
            double continuityAtInjectorMps = coreMdot / (rhoCore * Math.Max(injectorAreaM2, 1e-12));
    
            // Stage 1 — authoritative ṁ; ρ from derived source discharge; P₀ from derived stagnation (not blind PressureRatio).
            InjectorDischargeResult injectorDischarge = InjectorDischargeSolver.Solve(
                source,
                activeDesign,
                rhoCore,
                p0Implied,
                ambient.PressurePa,
                injectorYawAngleDegOverride: yawPhysicsDeg);
    
            double injectorJetVelocityRaw = injectorDischarge.VelocityMagnitudeFromContinuityMps;
            var (vtRaw, vaRaw) = SwirlMath.ResolveInjectorComponents(
                injectorJetVelocityRaw,
                yawPhysicsDeg,
                activeDesign.InjectorPitchAngleDeg);
    
            double va0 = injectorDischarge.AxialVelocityMps;
            double vt0 = injectorDischarge.TangentialVelocityMps;
            double injectorJetVelocityEffective = injectorDischarge.EffectiveVelocityMagnitudeMps;
    
            double rInjFluxM = 0.5e-3 * Math.Max(activeDesign.SwirlChamberDiameterMm, 1.0);
            double vMagInjPlane = Math.Sqrt(va0 * va0 + vt0 * vt0);
            double sFluxInjector = SwirlMath.SwirlCorrelationForEntrainment(
                coreMdot * rInjFluxM * vt0,
                coreMdot * va0,
                coreMdot,
                rInjFluxM,
                Math.Max(vMagInjPlane, 1e-9),
                vt0);
            double injectorSwirlDirective = SwirlMath.InjectorSwirlDirective(vt0, Math.Max(vMagInjPlane, 1e-9));
    
            // Pre-march radial picture on effective swirl — bounded entrainment boost (does not add free static head in march).
            double rWallM = 0.5e-3 * Math.Max(activeDesign.SwirlChamberDiameterMm, 1.0);
            var radialPreMarch = RadialVortexPressureModel.ComputeShapingRelativeToBulk(
                pStaticDerived,
                p0Implied,
                rhoCore,
                Math.Abs(vt0),
                rWallM,
                ChamberPhysicsCoefficients.RadialCoreRadiusFractionOfWall,
                ChamberPhysicsCoefficients.RadialPressureCapPa,
                source.AmbientPressurePa);
            double pCoreEstimatedPa = Math.Max(ambient.PressurePa - radialPreMarch.CorePressureDropPa, 1.0);
            double deltaPCoreUseful = Math.Clamp(
                radialPreMarch.CorePressureDropPa,
                0.0,
                ChamberPhysicsCoefficients.CouplingInletCorePressureUseCapPa);
            double vMagInj = Math.Sqrt(va0 * va0 + vt0 * vt0);
            double dynHeadPa = 0.5 * rhoCore * vMagInj * vMagInj;
            // Augments capture-boundary pressure deficit (Pa) when core suction is modeled — not a multiplicative ṁ boost.
            double captureStaticPressureDeficitAugmentationPa = 0.0;
            if (run.UseSwirlEntrainmentBoost)
            {
                double augFromCore = ChamberPhysicsCoefficients.CouplingVortexEntrainmentC * deltaPCoreUseful;
                double augDynCap = ChamberPhysicsCoefficients.CouplingVortexEntrainmentDynamicHeadGamma * dynHeadPa;
                captureStaticPressureDeficitAugmentationPa = Math.Min(
                    augFromCore,
                    Math.Min(augDynCap, ChamberPhysicsCoefficients.CouplingInletCorePressureUseCapPa));
                captureStaticPressureDeficitAugmentationPa = Math.Max(0.0, captureStaticPressureDeficitAugmentationPa);
            }
    
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
            double preBreak = SwirlDecayModel.PreMarchBreakdownRisk(
                sFluxInjector,
                ldRatio,
                activeDesign.InjectorAxialPositionRatio);
    
            double erHint = Math.Clamp(
                0.32 + 0.28 * Math.Tanh((activeDesign.ExitDiameterMm / Math.Max(activeDesign.SwirlChamberDiameterMm, 1.0) - 1.05) * 1.8),
                0.22,
                1.35);
    
            double kTotal = SwirlDecayModel.ComputeKTotal(
                activeDesign.SwirlChamberLengthMm,
                activeDesign.SwirlChamberDiameterMm,
                sFluxInjector,
                erHint,
                activeDesign.InjectorAxialPositionRatio,
                preBreak);
    
            double chamberDM = activeDesign.SwirlChamberDiameterMm * 1e-3;
            double swirlDecayPerStep = SwirlDecayModel.DecayPerStepFromK(kTotal, sectionLengthM, chamberDM, DefaultMarchSteps);
    
            double rSwirlMomM = 0.5e-3 * Math.Max(activeDesign.SwirlChamberDiameterMm, 1.0);
            SwirlChamberDischargePathSpec dischargeSpec = SwirlChamberDischargePathSpec.ForNozzleChamber(
                ambient.PressurePa,
                aCaptureM2,
                aFreeChamberMm2 * 1e-6,
                aChamberBoreMm2 * 1e-6,
                outletAreaM2,
                activeDesign.ExpanderHalfAngleDeg);
            FlowMarchDetailedResult detailed = MarchPhase_SolveSwirlChamber(
                marcher,
                inletState,
                sectionLengthM,
                DefaultMarchSteps,
                AreaAt,
                PerimeterAt,
                CaptureAreaAt,
                vt0,
                swirlDecayPerStep,
                captureStaticPressureDeficitAugmentationPa,
                ldRatio,
                activeDesign.SwirlChamberDiameterMm,
                run.UseReynoldsEntrainmentFactor,
                rSwirlMomM,
                run.ValidateMarchStepInvariants,
                aChamberBoreMm2 * 1e-6,
                aFreeChamberMm2 * 1e-6,
                dischargeSpec);
    
            IReadOnlyList<FlowMarchStepResult> steps = detailed.StepResults;
            JetState lastMarch = detailed.FlowStates[^1];
    
            SwirlChamberMarchDiagnostics chamberMarchDiag = CapacityPhase_BuildSwirlChamberMarchDiagnostics(
                activeDesign,
                entrainment,
                aChamberBoreMm2,
                aHubMm2,
                aFreeChamberMm2,
                aEffM2,
                perimeterMarchM,
                aCaptureM2,
                detailed,
                captureStaticPressureDeficitAugmentationPa,
                inletState,
                vt0,
                lastMarch,
                gas);
    
            CapacityPhase_LogSwirlEntranceCapacity(chamberMarchDiag, run.SiVerbosityLevel);
    
            // ThrustPhase: stator + expander + exit control-volume thrust (same physics as before; only orchestration split above).
            double etaStatorBase = Math.Min(0.42, activeDesign.StatorVaneAngleDeg / 95.0);
            double fracVt = Math.Clamp(1.0 - 0.38 * etaStatorBase, 0.22, 0.92);
    
            double impliedYawDeg = Math.Atan2(
                Math.Abs(detailed.FinalTangentialVelocityMps),
                Math.Max(Math.Abs(lastMarch.VelocityMps), 1e-6)) * (180.0 / Math.PI);
    
            double rStatorOuterMm = NozzleGeometryMetrics.BuiltRecoveryAnnulusInnerRadiusMm(activeDesign, run);
            HubStatorRecoveryContext hubCtx = HubStatorFirstOrderModel.ComputeContext(
                activeDesign,
                rStatorOuterMm,
                activeDesign.StatorVaneAngleDeg,
                impliedYawDeg);
    
            var stLossPre = StatorLossModel.Compute(
                Math.Max(lastMarch.DensityKgM3, 1e-6),
                lastMarch.VelocityMps,
                detailed.FinalTangentialVelocityMps,
                activeDesign.StatorVaneAngleDeg);
    
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
            var statorSiIn = new StatorRecoverySiInput(
                detailed.FinalTangentialVelocityMps,
                lastMarch.VelocityMps,
                lastMarch.DensityKgM3,
                lastMarch.TemperatureK);
            StatorRecoveryOutput statorOut = stator.Apply(in statorSiIn, etaStatorEff, fracVt);
    
            HubStatorFlowDiagnostics hubDiag = HubStatorFirstOrderModel.BuildDiagnostics(
                hubCtx,
                activeDesign,
                etaStatorEff,
                detailed.FinalTangentialVelocityMps,
                statorOut.RemainingTangentialVelocityMps,
                lastMarch.VelocityMps,
                statorOut.AxialVelocityGainMps);
    
            // Swirling diffuser / expander: correlation uses flux swirl from final march state when available.
            double swirlCorrExpander = detailed.StepPhysicsStates.Count > 0
                ? detailed.StepPhysicsStates[^1].SwirlNumberFlux
                : Math.Abs(vt0) / Math.Max(Math.Abs(va0), 1e-6);
            var diffuserCoupling = SwirlDiffuserRecoveryModel.Compute(
                activeDesign.ExpanderHalfAngleDeg,
                activeDesign.ExpanderLengthMm,
                activeDesign.SwirlChamberDiameterMm,
                activeDesign.ExitDiameterMm,
                Math.Max(lastMarch.DensityKgM3, 1e-6),
                lastMarch.VelocityMps,
                swirlCorrExpander,
                detailed.FinalTangentialVelocityMps);
    
            double diffuserRecoveryMult = Math.Clamp(
                diffuserCoupling.EffectivePressureRecoveryEfficiency
                / Math.Max(ChamberPhysicsCoefficients.DiffuserCouplingReferenceEfficiency, 0.05),
                ChamberPhysicsCoefficients.DiffuserCouplingScaleMin,
                ChamberPhysicsCoefficients.DiffuserCouplingScaleMax);
    
            double rhoExp = lastMarch.DensityKgM3;
            double vaExp = lastMarch.VelocityMps;
            double dynExp = 0.5 * rhoExp * vaExp * vaExp;
            double dPExpanderBase = diffuserCoupling.EstimatedPressureRecoveryCoefficient * dynExp;
            double dPExpanderEff = diffuserCoupling.ExpanderPressureRecoveryPa;
            double expanderForceN = diffuserCoupling.ExpanderWallAxialForceFromPressureN;
    
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
                CaptureStaticPressureDeficitAugmentationPa = captureStaticPressureDeficitAugmentationPa,
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
                    captureStaticPressureDeficitAugmentationPa,
                    swirlLedger,
                    etaStatorBase,
                    etaStatorEff)
            };
    
            double inletPressureForceN = steps.Sum(s => s.PressureForceN);
            double minInletP = steps.Count > 0 ? steps.Min(s => s.InletLocalPressurePa) : ambient.PressurePa;
            double maxInletMach = steps.Count > 0 ? steps.Max(s => s.InletMach) : 0.0;
            bool anyChoked = steps.Any(s => s.InletIsChoked)
                || (detailed.MarchClosure?.AnyEntrainmentChoked ?? false);
            double sumReq = steps.Sum(s => s.RequestedDeltaEntrainedMassFlowKgS);
            double sumAct = steps.Sum(s => s.DeltaEntrainedMassFlowKgS);
            double shortfall = Math.Max(0.0, sumReq - sumAct);
    
            double mdotExit = finalOutlet.TotalMassFlowKgS;

            PhysicsResidualSummary? marchRes = detailed.MarchResidualSummary;
            double exitMassFluxResidual = mdotExit > 1e-18
                ? Math.Abs(
                      mdotExit
                      - finalOutlet.DensityKgM3 * Math.Max(finalOutlet.AreaM2, 1e-18) * finalOutlet.VelocityMps)
                  / mdotExit
                : 0.0;
            PhysicsResidualSummary conservationResiduals = marchRes == null
                ? new PhysicsResidualSummary
                {
                    MaxChamberContinuityResidualRelative = 0.0,
                    MeanChamberContinuityResidualRelative = 0.0,
                    MaxChamberAxialMomentumBudgetResidualRelative = 0.0,
                    MaxChamberAngularMomentumFluxClosureResidualRelative = 0.0,
                    MaxChamberBulkPressureConsistencyResidualRelative = 0.0,
                    ExitControlVolumeMassFluxResidualRelative = exitMassFluxResidual
                }
                : new PhysicsResidualSummary
                {
                    MaxChamberContinuityResidualRelative = marchRes.MaxChamberContinuityResidualRelative,
                    MeanChamberContinuityResidualRelative = marchRes.MeanChamberContinuityResidualRelative,
                    MaxChamberAxialMomentumBudgetResidualRelative = marchRes.MaxChamberAxialMomentumBudgetResidualRelative,
                    MaxChamberAngularMomentumFluxClosureResidualRelative = marchRes
                        .MaxChamberAngularMomentumFluxClosureResidualRelative,
                    MaxChamberBulkPressureConsistencyResidualRelative = marchRes.MaxChamberBulkPressureConsistencyResidualRelative,
                    ExitControlVolumeMassFluxResidualRelative = exitMassFluxResidual
                };

            ThrustCalculator.ThrustControlVolumeResult cv = ThrustCalculator.ComputeControlVolumeThrustSanitized(
                mdotExit,
                finalOutlet.VelocityMps,
                ambient.VelocityMps,
                finalOutlet.PressurePa,
                ambient.PressurePa,
                finalOutlet.AreaM2);
    
            double fMom = cv.MomentumN;
            double fPressureExit = cv.ExitPlanePressureN;
            bool thrustCvValid = cv.IsValid;
            string? thrustInvalidReason = thrustCvValid ? null : cv.InvalidReason;
            double fNet = thrustCvValid ? cv.NetN : 0.0;
            double coreMomentumEstimateN = coreMdot * Math.Max(Math.Abs(va0), 1e-12);
    
            double solvedEr = coreMdot > 1e-12
                ? Math.Max(0.0, (lastMarch.TotalMassFlowKgS - coreMdot) / coreMdot)
                : 0.0;
    
            ChamberFirstOrderPhysics chamber = ChamberPhysicsPipeline.Build(
                activeDesign,
                source,
                injectorJetVelocityRaw,
                vt0,
                va0,
                sFluxInjector,
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
                vtAfterStator,
                Math.Max(1.0001, p0Implied / Math.Max(ambient.PressurePa, 1.0)),
                captureAreaM2: aCaptureM2,
                aFreeChamberMm2: aFreeChamberMm2,
                expanderWallAxialForceN: expanderForceN,
                expanderDeltaPEffectivePa: dPExpanderEff,
                injectorVelocityState: injectorDischarge.VelocityState);
    
            VortexFlowDiagnostics vortex = ChamberPhysicsPipeline.ToLegacyVortexDiagnostics(chamber, swirlDecayPerStep);
    
            InjectorPressureVelocityDiagnostics injectorPressureVelocity = InjectorPressureVelocityDiagnostics.Compute(
                source,
                gas,
                tTotalForDiagnostics,
                ambient.PressurePa,
                p0Implied,
                pStaticDerived,
                rawInlet.DensityKgM3,
                injectorJetVelocityRaw,
                injectorJetVelocityEffective,
                va0,
                vt0,
                yawPhysicsDeg,
                steps,
                detailed.StepPhysicsStates,
                sectionLengthM,
                activeDesign.InjectorAxialPositionRatio,
                chamber.RadialPressure,
                inletState.PressurePa);
    
            SiThrustSanity.LogCvAndApplyAssertions(
                run,
                cv,
                mdotExit,
                finalOutlet.VelocityMps,
                finalOutlet.PressurePa,
                ambient.PressurePa,
                finalOutlet.AreaM2,
                fMom,
                fPressureExit,
                ref fNet,
                ref thrustCvValid,
                ref thrustInvalidReason,
                inletPressureForceN,
                expanderForceN,
                injectorPressureVelocity.ChamberStaticPressureNearInjectorPa,
                injectorPressureVelocity.MarchInletAssignedStaticPressurePa,
                steps,
                out bool chamberPressureHardAssertionTripped);
    
            double statorEntrySwirlCorrelation = detailed.MarchClosure?.FinalFluxSwirlNumber
                ?? Math.Max(Math.Abs(sFluxInjector), 1e-12);
            SwirlChamberHealthReport swirlChamberHealth = SwirlChamberHealthReportBuilder.Build(
                activeDesign,
                ambient,
                injectorDischarge,
                sFluxInjector,
                aCaptureM2,
                aFreeChamberMm2,
                sumAct,
                lastMarch.TotalMassFlowKgS,
                lastMarch.VelocityMps,
                detailed.FinalTangentialVelocityMps,
                statorEntrySwirlCorrelation,
                vaAfterStator,
                fNet,
                etaStatorEff,
                statorOut.RecoveredPressureRisePa,
                pCoreEstimatedPa,
                physicsInjectorYawDegrees: yawPhysicsDeg);

            GeometryAssemblyPath continuousGeom = GeometryAssemblyPath.Compute(activeDesign, run);
            ContinuousNozzleSolution continuousPath = ContinuousNozzleSolver.Solve(
                continuousGeom,
                activeDesign,
                source,
                detailed,
                inletState,
                vt0,
                va0,
                minInletP,
                gas,
                closure: null,
                diffuserCoupling,
                statorOut,
                vaAfterStator,
                vtAfterStator,
                pAfterStator,
                rhoAfterStator,
                Math.Max(lastMarch.TemperatureK, 1.0),
                tTotalForDiagnostics,
                finalOutlet);
    
            var siDiag = new SiFlowDiagnostics
            {
                SwirlSegmentPhysics = chamber.SwirlSegmentReport,
                InjectorPlaneFluxSwirlNumber = sFluxInjector,
                InjectorPlaneSwirlDirective = injectorSwirlDirective,
                MarchSteps = steps,
                PhysicsStepStates = detailed.StepPhysicsStates,
                MarchPhysicsClosure = detailed.MarchClosure,
                ConservationResiduals = conservationResiduals,
                MinInletLocalStaticPressurePa = minInletP,
                MaxInletMach = maxInletMach,
                AnyEntrainmentStepChoked = anyChoked,
                EntrainmentStepsLimitedBySwirlPassageCapacity = detailed.EntrainmentStepsLimitedBySwirlPassageCapacity,
                SumRequestedEntrainmentIncrementsKgS = sumReq,
                SumActualEntrainmentIncrementsKgS = sumAct,
                EntrainmentShortfallSumKgS = shortfall,
                ExpanderAxialPressureForceN = expanderForceN,
                InletAxialPressureForceN = inletPressureForceN,
                StatorRecoveredPressureRisePa = statorOut.RecoveredPressureRisePa,
                FinalTangentialVelocityMps = vtAfterStator,
                FinalAxialVelocityMps = vaAfterStator,
                MomentumThrustN = fMom,
                PressureThrustN = fPressureExit,
                NetThrustN = fNet,
                CoreMomentumEstimateN = coreMomentumEstimateN,
                ThrustCvMdotExitKgS = mdotExit,
                ThrustCvVExitMps = finalOutlet.VelocityMps,
                ThrustCvPExitPa = finalOutlet.PressurePa,
                ThrustCvPAmbientPa = ambient.PressurePa,
                ThrustCvAExitM2 = finalOutlet.AreaM2,
                ThrustOtherForcesAddedToNetN = 0.0,
                ChamberPressureHardAssertionTripped = chamberPressureHardAssertionTripped,
                ThrustControlVolumeIsValid = thrustCvValid,
                ThrustControlVolumeInvalidReason = thrustInvalidReason,
                ThrustControlVolumeSoftWarning = thrustCvValid ? cv.SoftWarning : null,
                Vortex = vortex,
                Chamber = chamber,
                Coupling = couplingDiag,
                HubStator = hubDiag,
                InjectorPressureVelocity = injectorPressureVelocity,
                ChamberMarch = chamberMarchDiag,
                SwirlChamberHealth = swirlChamberHealth,
                SourceDischargeConsistency = dischargeReport,
                MarchInvariantWarnings = detailed.MarchInvariantWarnings,
                ContinuousPath = continuousPath
            };
    
            MarchInvariantPhase_LogIfVerbose(detailed.MarchInvariantWarnings, run);
    
            var designer = new NozzleDesigner();
            NozzleDesignResult designResult = designer.CreateDesignResult(
                inletState,
                flowStates,
                outletAreaM2,
                ambient.PressurePa,
                ambient.VelocityMps,
                siDiag);
    
            NozzleDesignInputs drivenDesign = FlowDrivenNozzleBuilder.BuildDesignInputs(designResult, activeDesign, run);
    
            NozzleSolvedState solved = NozzleSolvedStateFlowAdapter.FromSiFlow(
                designResult,
                inletState,
                finalOutlet,
                source,
                drivenDesign,
                areaDriverDiagnostic,
                continuityAtInjectorMps,
                injectorJetVelocityRaw,
                siDiag);
    
            NozzleCriticalRatiosSnapshot criticalRatios = NozzleCriticalRatios.Compute(
                drivenDesign,
                source,
                solved,
                siDiag,
                run);
    
            IReadOnlyList<string> healthBase = NozzleDesignHealthCheck.Validate(drivenDesign, criticalRatios, siDiag);
            var health = new List<string>(
                healthBase.Count
                + (chamberMarchDiag.ValidationWarnings?.Count ?? 0)
                + swirlChamberHealth.PlainLanguageWarnings.Count);
            health.AddRange(healthBase);
            health.AddRange(chamberMarchDiag.ValidationWarnings ?? Array.Empty<string>());
            health.AddRange(swirlChamberHealth.PlainLanguageWarnings);
    
            double mdotAmbPotential = SwirlAmbientEntrainmentSolver.ComputeSwirlDrivenAmbientPotentialKgS(
                ambient.PressurePa,
                pCoreEstimatedPa,
                aCaptureM2,
                ambient.DensityKgM3,
                sFluxInjector);
    
            var physicsStages = new NozzlePhysicsStageResult
            {
                Stage1Injector = injectorDischarge,
                Stage2InjectorFluxSwirlNumber = sFluxInjector,
                Stage2SwirlNumberAtInjector = sFluxInjector,
                Stage3CorePressureDropPa = radialPreMarch.CorePressureDropPa,
                Stage3WallPressureRisePa = radialPreMarch.WallPressureRisePa,
                Stage3EstimatedCoreStaticPressurePa = pCoreEstimatedPa,
                Stage4AmbientInflowPotentialKgS = mdotAmbPotential,
                Stage4AmbientInflowActualIntegratedKgS = sumAct,
                Stage5MixedMassFlowKgS = lastMarch.TotalMassFlowKgS,
                Stage5MixedAxialVelocityMps = lastMarch.VelocityMps,
                Stage5MixedTangentialVelocityMps = detailed.FinalTangentialVelocityMps,
                Stage6DiffuserPressureRiseEffectivePa = dPExpanderEff,
                Stage6DiffuserRecoveryMultiplier = diffuserRecoveryMult,
                Stage7StatorRecoveredPressureRisePa = statorOut.RecoveredPressureRisePa,
                Stage7StatorEtaEffective = etaStatorEff,
                Stage7AxialVelocityAfterMps = vaAfterStator,
                FinalExitAxialVelocityMps = vaAfterStator,
                FinalTotalMassFlowKgS = finalOutlet.TotalMassFlowKgS
            };
    
            return new SiPathSolveResult(
                drivenDesign,
                solved,
                siDiag,
                criticalRatios,
                health,
                designResult,
                inletState,
                physicsStages);
        }
        private static SourceDischargeConsistencyReport SourceDischargePhase_Evaluate(
            SourceInputs source,
            GasProperties gas,
            double ambientPressurePa) =>
            SourceDischargeConsistencyEvaluator.Evaluate(source, gas, ambientPressurePa);
    
        private static void SourceDischargePhase_Log(SourceDischargeConsistencyReport report, SiVerbosityLevel verbosity)
        {
            if (verbosity < SiVerbosityLevel.Normal)
                return;
            foreach (string line in report.FormatReportLines())
            {
                try
                {
                    Library.Log(line);
                }
                catch
                {
                    // validate / headless: Library may be unavailable
                }
    
                ConsoleReportColor.WriteClassifiedLine(line);
            }
        }
    
        private static FlowMarchDetailedResult MarchPhase_SolveSwirlChamber(
            FlowMarcher marcher,
            JetState inletState,
            double sectionLengthM,
            int stepCount,
            Func<double, double> areaFunction,
            Func<double, double> perimeterFunction,
            Func<double, double> captureAreaFunction,
            double primaryTangentialVelocityMps,
            double swirlDecayPerStepFactor,
            double captureStaticPressureDeficitAugmentationPa,
            double chamberLdRatio,
            double chamberDiameterMm,
            bool useReynoldsOnEntrainmentCe,
            double swirlMomentRadiusM,
            bool validateMarchStepInvariants,
            double chamberFullBoreAreaM2,
            double freeAnnulusAreaM2,
            SwirlChamberDischargePathSpec? dischargePathSpec) =>
            marcher.SolveDetailed(
                inletState,
                sectionLengthM,
                stepCount,
                areaFunction,
                perimeterFunction,
                captureAreaFunction,
                primaryTangentialVelocityMps,
                swirlDecayPerStepFactor,
                captureStaticPressureDeficitAugmentationPa,
                chamberLdRatio,
                chamberDiameterMm,
                useReynoldsOnEntrainmentCe,
                swirlMomentRadiusM,
                validateMarchStepInvariants,
                chamberFullBoreAreaM2,
                freeAnnulusAreaM2,
                capEntrainmentToSwirlPassageMach: true,
                swirlPassageMachLimitsForEntrainmentCap: null,
                dischargePathSpec);
    
        private static void CapacityPhase_LogSwirlEntranceCapacity(
            SwirlChamberMarchDiagnostics chamberMarchDiag,
            SiVerbosityLevel verbosity)
        {
            if (verbosity < SiVerbosityLevel.Normal)
                return;
            foreach (string line in chamberMarchDiag.SwirlEntranceCapacityStations?.FormatReportLines() ?? Array.Empty<string>())
            {
                try
                {
                    Library.Log(line);
                }
                catch
                {
                }
    
                ConsoleReportColor.WriteClassifiedLine(line);
            }
        }
    
        private static void MarchInvariantPhase_LogIfVerbose(IReadOnlyList<string> warnings, RunConfiguration run)
        {
            if (!run.ValidateMarchStepInvariants || run.SiVerbosityLevel < SiVerbosityLevel.High || warnings.Count == 0)
                return;
            ConsoleStatusWriter.WriteLine("--- SI march invariant checks (validation mode) ---", StatusLevel.Normal);
            try
            {
                Library.Log("--- SI march invariant checks (validation mode) ---");
            }
            catch
            {
            }
    
            foreach (string w in warnings)
            {
                try
                {
                    Library.Log(w);
                }
                catch
                {
                }
    
                ConsoleReportColor.WriteError(w);
            }
        }
    
        private static SwirlChamberMarchDiagnostics CapacityPhase_BuildSwirlChamberMarchDiagnostics(
            NozzleDesignInputs d,
            EntrainmentModel em,
            double aChamberBoreMm2,
            double aHubMm2,
            double aFreeChamberMm2,
            double aEffM2,
            double perimeterMarchM,
            double aCaptureM2,
            FlowMarchDetailedResult detailed,
            double captureStaticPressureDeficitAugmentationPa,
            JetState inletJet,
            double primaryTangentialVelocityMps,
            JetState lastJet,
            GasProperties gas)
        {
            double aInletMm2 = SwirlChamberMarchGeometry.InletCaptureAreaMm2(d.InletDiameterMm);
            double aInjMm2 = Math.Max(d.TotalInjectorAreaMm2, 1e-9);
            double aExitMm2 = SwirlChamberMarchGeometry.ExitInnerAreaMm2(d.ExitDiameterMm);
            double aCh = Math.Max(aChamberBoreMm2, 1e-9);
            double ceFirst = detailed.StepResults.Count > 0
                ? detailed.StepResults[0].EntrainmentMixingEffectivenessUsed
                : em.Coefficient;
            var warnings = new List<string>();
            if (aInjMm2 / aCh > 0.9)
                warnings.Add("WARNING (SI march): A_inj/A_chamber > 0.9 — port blockage may suppress entrainment.");
            double aInletM2 = aInletMm2 * 1e-6;
            if (aCaptureM2 < 0.5 * aInletM2)
                warnings.Add("WARNING (SI march): A_capture < 0.5×A_inlet — intake model may starve entrainment.");
            if (aExitMm2 / aCh > 3.0)
                warnings.Add("WARNING (SI march): A_exit/A_chamber > 3 — large expansion vs bore may limit useful coupling.");
    
            FlowStepState? firstPhysics = detailed.StepPhysicsStates.Count > 0 ? detailed.StepPhysicsStates[0] : null;
            FlowStepState? lastPhysics = detailed.StepPhysicsStates.Count > 0 ? detailed.StepPhysicsStates[^1] : null;
            SwirlEntranceCapacityDualResult dual = SwirlEntranceCapacityEvaluator.EvaluateDual(
                gas,
                firstPhysics,
                lastPhysics,
                inletJet,
                lastJet,
                primaryTangentialVelocityMps,
                aCaptureM2,
                aChamberBoreMm2 * 1e-6,
                aEffM2);
            foreach (string h in dual.EnumerateHealthMessages())
                warnings.Add(h);
    
            SwirlEntrainmentGovernorSummary gov = SwirlEntrainmentGovernorSummary.Build(dual, detailed, inletJet.MassFlowKgS);
            IReadOnlyList<string> radialLines = FormatRadialVortexShapingReportLines(lastPhysics);
    
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
                CaptureStaticPressureDeficitAugmentationPa = captureStaticPressureDeficitAugmentationPa,
                SwirlEntranceCapacityStations = dual,
                EntrainmentGovernor = gov,
                RadialShapingReportLines = radialLines,
                ValidationWarnings = warnings,
                ChamberDischargeSplit = detailed.FinalChamberDischargeSplit
            };
        }
    
        private static IReadOnlyList<string> FormatRadialVortexShapingReportLines(FlowStepState? lastPhysics)
        {
            if (lastPhysics == null)
                return Array.Empty<string>();
            double dCore = Math.Max(0.0, lastPhysics.PStaticPa - lastPhysics.CorePressurePa);
            double dWall = Math.Max(0.0, lastPhysics.WallPressurePa - lastPhysics.PStaticPa);
            return new List<string>
            {
                "REDUCED-ORDER RADIAL PRESSURE BALANCE (last march station — secondary to bulk P_static)",
                $"  P_bulk [Pa]:               {lastPhysics.PStaticPa:F1}",
                $"  P_core [Pa]:               {lastPhysics.CorePressurePa:F1}",
                $"  P_wall [Pa]:               {lastPhysics.WallPressurePa:F1}",
                $"  DeltaP_core [Pa]:          {dCore:F1}",
                $"  DeltaP_wall [Pa]:          {dWall:F1}",
                $"  core radius used [m]:      {lastPhysics.RadialCoreRadiusUsedM:E4}",
                $"  shaping invariants OK:     {lastPhysics.RadialShapingInvariantsOk}  {lastPhysics.RadialShapingInvariantNote}"
            };
        }
    
        private static IReadOnlyList<string> BuildCouplingSummaryLines(
            double vInRaw,
            double vInEff,
            SwirlDiffuserRecoveryResult diffuser,
            double diffuserRecoveryMult,
            double captureStaticPressureDeficitAugmentationPa,
            SwirlEnergyCouplingLedger e,
            double etaStatorBase,
            double etaStatorEff)
        {
            var lines = new List<string>(4);
            if (vInEff < 0.97 * vInRaw)
                lines.Add("Coupled vortex physics reduced injector energy vs raw blend (Cd / turning loss).");
            if (diffuser.SeparationRiskScore > 0.48 && diffuserRecoveryMult < 0.92)
                lines.Add("Diffuser recovery limited by separation risk (ΔP_exp and axial factor scaled).");
            if (captureStaticPressureDeficitAugmentationPa > 250.0)
                lines.Add(
                    "Core suction augments capture-boundary pressure deficit for entrainment (reduced-order radial balance; bounded Pa).");
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

}
