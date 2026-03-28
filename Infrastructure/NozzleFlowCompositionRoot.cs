using System;
using System.Collections.Generic;
using System.Linq;
using PicoGK;
using PicoGK_Run.Core;
using PicoGK_Run.Geometry;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;
using PicoGK_Run.Physics.Reports;
using PicoGK_Run.Physics.Solvers;
using PicoGK_Run.Infrastructure.Pipeline;

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
        JetState InletState,
        NozzlePhysicsStageResult PhysicsStages);

    /// <summary>
    /// Same prepare → <c>SolveSiPath</c> (+ optional continuity) as <see cref="Run"/> — no voxels, no viewer, no console summary.
    /// </summary>
    internal static FlowTuneEvaluation EvaluateDesignForTuning(
        SourceInputs source,
        NozzleDesignInputs candidateDesign,
        RunConfiguration run)
    {
        UnifiedEvaluationResult u = EvaluateCandidateUnified(source, candidateDesign, run, NozzleEvaluationMode.TuningFast);
        return ToFlowTuneEvaluation(u);
    }

    /// <summary>Authoritative handoff: synthesis / derived-bore reference / template — identical for tuning and final.</summary>
    public static PreparedNozzleDesignHandoff PrepareActiveDesignForSolve(
        SourceInputs source,
        NozzleDesignInputs seedDesign,
        RunConfiguration run)
    {
        NozzleDesignInputs activeDesign;
        SwirlChamberSizingModel.SizingDiagnostics? chamberSizing;
        if (run.UsePhysicsInformedGeometry)
        {
            NozzleGeometrySynthesis.GeometrySynthesisResult syn = NozzleGeometrySynthesis.SynthesizeWithDiagnostics(
                source,
                seedDesign,
                run.GeometrySynthesisTargetEntrainmentRatio,
                run);
            activeDesign = syn.Design;
            chamberSizing = syn.ChamberSizing;
        }
        else if (run.UseDerivedSwirlChamberDiameter)
        {
            activeDesign = seedDesign;
            chamberSizing = SwirlChamberSizingModel.ComputeDerived(
                source,
                seedDesign,
                run.GeometrySynthesisTargetEntrainmentRatio,
                run,
                SwirlChamberSizingModel.DiameterMode.ReferenceDerivedAtConfiguredTargetEr);
        }
        else
        {
            activeDesign = seedDesign;
            chamberSizing = SwirlChamberSizingModel.ForUserTemplate(seedDesign, source, run);
        }

        return new PreparedNozzleDesignHandoff(seedDesign, activeDesign, chamberSizing);
    }

    /// <summary>Single evaluation path shared by autotune and final run (voxels excluded).</summary>
    public static UnifiedEvaluationResult EvaluateCandidateUnified(
        SourceInputs source,
        NozzleDesignInputs seedDesign,
        RunConfiguration run,
        NozzleEvaluationMode mode)
    {
        PreparedNozzleDesignHandoff prep = PrepareActiveDesignForSolve(source, seedDesign, run);
        SiPathSolveResult path = SolveSiPath(source, prep.ActiveDesignAfterSynthesis, run);
        bool checkContinuity = mode == NozzleEvaluationMode.FinalDetailed
            ? run.RunGeometryContinuityCheck
            : run.EvaluateGeometryContinuityDuringAutotune;
        GeometryContinuityReport? continuity = checkContinuity
            ? GeometryContinuityValidator.Check(path.DrivenDesign, run)
            : null;
        return BuildUnifiedEvaluation(prep, path, continuity, run);
    }

    /// <summary>Build unified result after <c>SolveSiPath</c> (used by <see cref="Run"/> to preserve profiler stages).</summary>
    private static UnifiedEvaluationResult BuildUnifiedAfterSolve(
        PreparedNozzleDesignHandoff prep,
        SiPathSolveResult path,
        GeometryContinuityReport? continuity,
        RunConfiguration run) =>
        BuildUnifiedEvaluation(prep, path, continuity, run);

    private static UnifiedEvaluationResult BuildUnifiedEvaluation(
        PreparedNozzleDesignHandoff prep,
        SiPathSolveResult path,
        GeometryContinuityReport? continuity,
        RunConfiguration run)
    {
        SiFlowDiagnostics si = path.SiDiag;
        FlowTunePhysicsMetrics metrics = FlowTunePhysicsMetrics.FromChamber(si.Chamber, si.FinalAxialVelocityMps);
        int designErr = path.HealthMessages.Count(m => m.StartsWith("DESIGN ERROR", StringComparison.Ordinal));
        double mdotTot = path.PhysicsStages.FinalTotalMassFlowKgS;
        PhysicsPenaltyBreakdown phys = PenaltyBreakdownBuilder.BuildPhysics(
            si,
            metrics,
            path.HealthMessages,
            designErr,
            mdotTot);
        DownstreamGeometryTargets downstream = DownstreamGeometryResolver.Resolve(path.DrivenDesign, run);
        GeometryPenaltyBreakdown geom = PenaltyBreakdownBuilder.BuildGeometry(continuity, downstream, run);
        ConstraintViolationBreakdown cv = PenaltyBreakdownBuilder.BuildConstraints(
            si,
            continuity,
            designErr > 0,
            path.HealthMessages,
            mdotTot,
            downstream,
            run);
        bool hard = designErr > 0 || cv.Reject;
        string? hr = null;
        if (hard)
        {
            var parts = new List<string>();
            if (designErr > 0)
                parts.Add("DESIGN_ERROR");
            foreach (string r in cv.Reasons)
            {
                if (!parts.Contains(r))
                    parts.Add(r);
            }

            hr = string.Join("; ", parts);
        }

        return new UnifiedEvaluationResult(
            prep,
            path.DrivenDesign,
            path.Solved,
            si,
            path.CriticalRatios,
            path.HealthMessages is List<string> hl ? hl : new List<string>(path.HealthMessages),
            path.PhysicsStages,
            path.DesignResult,
            path.InletState,
            continuity,
            phys,
            geom,
            cv,
            hard,
            hr);
    }

    private static FlowTuneEvaluation ToFlowTuneEvaluation(UnifiedEvaluationResult u)
    {
        int hard = u.HealthMessages.Count(m => m.StartsWith("DESIGN ERROR", StringComparison.Ordinal));
        bool hasErr = hard > 0 || u.HardReject;
        return new FlowTuneEvaluation
        {
            CandidateDesign = u.Preparation.ActiveDesignAfterSynthesis,
            DrivenDesign = u.DrivenDesign,
            EntrainmentRatio = u.Solved.EntrainmentRatio,
            NetThrustN = u.SiDiagnostics.NetThrustN,
            SourceOnlyThrustN = u.Solved.SourceOnlyThrustN,
            VortexQualityMetric = u.SiDiagnostics.Chamber?.TuningCompositeQuality
                ?? u.SiDiagnostics.Vortex?.VortexQualityMetric
                ?? 0.0,
            PhysicsMetrics = FlowTunePhysicsMetrics.FromChamber(u.SiDiagnostics.Chamber, u.SiDiagnostics.FinalAxialVelocityMps),
            AmbientAirMassFlowKgS = u.Solved.AmbientAirMassFlowKgPerSec,
            CoreMassFlowKgS = u.Solved.CoreMassFlowKgPerSec,
            HealthCount = u.HealthMessages.Count,
            HasDesignError = hasErr,
            HealthMessages = u.HealthMessages is List<string> list ? list : new List<string>(u.HealthMessages),
            SiDiagnostics = u.SiDiagnostics,
            Score = 0.0,
            UnifiedEvaluation = u,
            PhysicsPenalties = u.PhysicsPenalties,
            GeometryPenalties = u.GeometryPenalties,
            ConstraintBreakdown = u.Constraints,
            TopPenaltySource = u.HardReject ? (u.HardRejectReason ?? "HardReject") : u.PhysicsPenalties.TopSource
        };
    }

    public static PipelineRunResult Run(NozzleInput input, bool showInViewer)
    {
        PipelineProfiler.ResetSession(input.Run.EnablePipelineProfiling);

        double chamberDiameterInputMm = input.Design.SwirlChamberDiameterMm;

        PreparedNozzleDesignHandoff prep;
        using (PipelineProfiler.Stage("pipeline.synthesis"))
            prep = PrepareActiveDesignForSolve(input.Source, input.Design, input.Run);
        SwirlChamberSizingModel.SizingDiagnostics? chamberSizing = prep.ChamberSizing;

        SiPathSolveResult path;
        using (PipelineProfiler.Stage("physics.solveSiPath"))
            path = SolveSiPath(input.Source, prep.ActiveDesignAfterSynthesis, input.Run);

        GeometryContinuityReport? geomContinuity;
        using (PipelineProfiler.Stage("geometry.continuity"))
        {
            geomContinuity = input.Run.RunGeometryContinuityCheck
                ? GeometryContinuityValidator.Check(path.DrivenDesign, input.Run)
                : null;
        }

        UnifiedEvaluationResult unified = BuildUnifiedAfterSolve(prep, path, geomContinuity, input.Run);

        // Per-segment timings live in NozzleGeometryBuilder (sum ≈ full voxel assembly; no outer wrapper — avoids double-count in TOTAL).
        var geometryBuilder = new NozzleGeometryBuilder();
        NozzleGeometryResult geometry = geometryBuilder.Build(unified.DrivenDesign, unified.Solved, input.Run);

        if (showInViewer)
        {
            using (PipelineProfiler.Stage("viewer.display"))
                AppPipeline.DisplayGeometryInViewer(geometry);
        }

        using (PipelineProfiler.Stage("reporting.consoleSummary"))
            PrintPhysicsSummary(unified.InletState, unified.DesignResult, unified.SiDiagnostics, input.Run.SiVerbosityLevel);

        var warnings = new List<string>
        {
            "SI path: compressible entrainment march + first-order stator/expander bookkeeping (not CFD)."
        };
        if (input.Run.UsePhysicsInformedGeometry)
        {
            warnings.Add(
                input.Run.UseDerivedSwirlChamberDiameter
                    ? "Geometry pre-sized by NozzleGeometrySynthesis with entrainment-derived swirl chamber bore (continuity sizing — not CFD)."
                    : "Geometry pre-sized by NozzleGeometrySynthesis (jet×swirl/ER heuristics for chamber bore; not CFD).");
        }

        if (chamberSizing != null)
        {
            foreach (string cw in chamberSizing.Warnings)
                warnings.Add("Chamber sizing: " + cw);
        }

        if (!input.Run.UsePhysicsInformedGeometry
            && input.Run.UseDerivedSwirlChamberDiameter
            && chamberSizing != null
            && chamberSizing.Mode == SwirlChamberSizingModel.DiameterMode.ReferenceDerivedAtConfiguredTargetEr)
        {
            double dAct = chamberDiameterInputMm;
            double dRef = chamberSizing.ChamberDiameterTargetMm;
            if (Math.Abs(dAct - dRef) > 1.5)
            {
                warnings.Add(
                    $"Chamber bore trace: actual input/seed D={dAct:F2} mm vs reference derived at GeometrySynthesisTargetEntrainmentRatio ({input.Run.GeometrySynthesisTargetEntrainmentRatio:F3}) D≈{dRef:F2} mm — autotune trials use per-trial ER; this is not CFD.");
            }
        }

        if (geomContinuity is { IsAcceptable: false })
            warnings.AddRange(geomContinuity.Issues);
        warnings.AddRange(unified.HealthMessages);

        NozzleInput effectiveInput = new NozzleInput(input.Source, unified.DrivenDesign, input.Run);
        PipelineProfileReport? profile = PipelineProfiler.TryBuildReport();

        string declaredSource = !input.Run.UsePhysicsInformedGeometry
            ? (input.Run.UseDerivedSwirlChamberDiameter
                ? "input_seed (no synthesis this run; see reference derived at configured target ER in sizing section)"
                : "template_or_hand_seed")
            : (input.Run.UseDerivedSwirlChamberDiameter
                ? "entrainment-derived via NozzleGeometrySynthesis (this run)"
                : "synthesis-heuristic (jet×swirl×ER bore)");

        double? refDerivedMm = input.Run.UseDerivedSwirlChamberDiameter ? chamberSizing?.ChamberDiameterTargetMm : null;

        var chamberAudit = new ChamberDiameterAudit
        {
            InputDesignMm = chamberDiameterInputMm,
            AfterSynthesisMm = prep.ActiveDesignAfterSynthesis.SwirlChamberDiameterMm,
            PreSiSolveMm = prep.ActiveDesignAfterSynthesis.SwirlChamberDiameterMm,
            PostFlowDrivenMm = unified.DrivenDesign.SwirlChamberDiameterMm,
            UsedForVoxelBuildMm = unified.DrivenDesign.SwirlChamberDiameterMm,
            DeclaredPrimarySource = declaredSource,
            AutotuneSearchUsedDerivedBoreSizing = false,
            AutotuneAppliedDirectChamberScale = false,
            ReferenceDerivedBoreAtConfiguredTargetErMm = refDerivedMm,
            Footnote = !input.Run.UsePhysicsInformedGeometry
                ? "UsePhysicsInformedGeometry was false — voxel bore equals input design (e.g. autotune winning seed). Enable synthesis on the final pass only if you intend to re-run sizing (may overwrite tuned seed)."
                : null
        };

        return new PipelineRunResult(
            effectiveInput,
            unified.Solved,
            geometry,
            warnings,
            unified.SiDiagnostics,
            unified.CriticalRatios,
            autotune: null,
            physicsStages: unified.PhysicsStages,
            geometryContinuity: geomContinuity,
            performanceProfile: profile,
            chamberSizing: chamberSizing,
            chamberDiameterAudit: chamberAudit);
    }

    private static SiPathSolveResult SolveSiPath(
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
            entrainmentBoost,
            ldRatio,
            activeDesign.SwirlChamberDiameterMm,
            run.UseReynoldsEntrainmentFactor,
            rSwirlMomM,
            run.ValidateMarchStepInvariants,
            aChamberBoreMm2 * 1e-6);

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
            entrainmentBoost,
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
        bool anyChoked = steps.Any(s => s.InletIsChoked)
            || (detailed.MarchClosure?.AnyEntrainmentChoked ?? false);
        double sumReq = steps.Sum(s => s.RequestedDeltaEntrainedMassFlowKgS);
        double sumAct = steps.Sum(s => s.DeltaEntrainedMassFlowKgS);
        double shortfall = Math.Max(0.0, sumReq - sumAct);

        double mdotExit = finalOutlet.TotalMassFlowKgS;
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
            Math.Max(1.0001, p0Implied / Math.Max(ambient.PressurePa, 1.0)));

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

        var siDiag = new SiFlowDiagnostics
        {
            InjectorPlaneFluxSwirlNumber = sFluxInjector,
            InjectorPlaneSwirlDirective = injectorSwirlDirective,
            MarchSteps = steps,
            PhysicsStepStates = detailed.StepPhysicsStates,
            MarchPhysicsClosure = detailed.MarchClosure,
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
            MarchInvariantWarnings = detailed.MarchInvariantWarnings
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

    /// <summary>Derived source discharge (stagnation-based live path); logging is separate.</summary>
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
        double entrainmentMassDemandMultiplier,
        double chamberLdRatio,
        double chamberDiameterMm,
        bool useReynoldsOnEntrainmentCe,
        double swirlMomentRadiusM,
        bool validateMarchStepInvariants,
        double chamberFullBoreAreaM2) =>
        marcher.SolveDetailed(
            inletState,
            sectionLengthM,
            stepCount,
            areaFunction,
            perimeterFunction,
            captureAreaFunction,
            primaryTangentialVelocityMps,
            swirlDecayPerStepFactor,
            entrainmentMassDemandMultiplier,
            chamberLdRatio,
            chamberDiameterMm,
            useReynoldsOnEntrainmentCe,
            swirlMomentRadiusM,
            validateMarchStepInvariants,
            chamberFullBoreAreaM2,
            capEntrainmentToSwirlPassageMach: true,
            swirlPassageMachLimitsForEntrainmentCap: null);

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
        Console.WriteLine("--- SI march invariant checks (validation mode) ---");
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
        double entrainmentMassDemandBoost,
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
            EntrainmentMassDemandBoost = entrainmentMassDemandBoost,
            SwirlEntranceCapacityStations = dual,
            EntrainmentGovernor = gov,
            RadialShapingReportLines = radialLines,
            ValidationWarnings = warnings
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
            "RADIAL VORTEX SHAPING (last march station — secondary to bulk P_static)",
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

    private static void PrintPhysicsSummary(
        JetState inlet,
        NozzleDesignResult d,
        SiFlowDiagnostics si,
        SiVerbosityLevel verbosity)
    {
        Console.WriteLine("--- SI flow-driven nozzle summary (compressible entrainment path) ---");
        Console.WriteLine($"Inlet jet axial velocity [m/s]: {inlet.VelocityMps:F2}");
        Console.WriteLine($"Estimated exit velocity [m/s]: {d.EstimatedExitVelocityMps:F2}");
        Console.WriteLine($"Estimated total mass flow [kg/s]: {d.EstimatedTotalMassFlowKgS:F4}");
        Console.WriteLine($"Estimated thrust [N]:       {d.EstimatedThrustN:F2} (momentum + pressure CV terms, first-order)");
        Console.WriteLine(
            $"Min inlet static (entrain.) [Pa]: {si.MinInletLocalStaticPressurePa:F1} ({SiPressureGuards.PaToBar(si.MinInletLocalStaticPressurePa):F4} bar)");
        ConsoleReportColor.WriteClassifiedLine(
            $"Max entrainment Mach [-]:   {si.MaxInletMach:F3}  Choked step: {si.AnyEntrainmentStepChoked}");
        Console.WriteLine($"Suggested inlet radius [m]: {d.SuggestedInletRadiusM:F5}");
        Console.WriteLine($"Suggested outlet radius [m]: {d.SuggestedOutletRadiusM:F5}");
        Console.WriteLine($"Suggested mixing length [m]: {d.SuggestedMixingLengthM:F5}");
        if (si.ChamberMarch != null && verbosity >= SiVerbosityLevel.Normal)
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
                ConsoleReportColor.WriteClassifiedLine(w);
            if (m.SwirlEntranceCapacityStations != null)
            {
                foreach (string line in m.SwirlEntranceCapacityStations.FormatReportLines())
                    ConsoleReportColor.WriteClassifiedLine(line);
            }
        }

        Console.WriteLine("---------------------------------------------------------------------");
    }
}
