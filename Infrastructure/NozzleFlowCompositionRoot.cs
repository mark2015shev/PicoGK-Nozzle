using System;
using System.Collections.Generic;
using System.Linq;
using PicoGK;
using PicoGK_Run.Core;
using PicoGK_Run.Geometry;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Infrastructure;

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
        _ = run;
        SiPathSolveResult r = SolveSiPath(source, candidateDesign);
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

        SiPathSolveResult path = SolveSiPath(input.Source, activeDesign);

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

    private static SiPathSolveResult SolveSiPath(SourceInputs source, NozzleDesignInputs activeDesign)
    {
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
        double injectorJetVelocity = NozzlePhysicsSolver.InjectorJetVelocityDriverBlend * areaDriver
            + (1.0 - NozzlePhysicsSolver.InjectorJetVelocityDriverBlend) * continuityCheck;

        var (vt0, va0) = SwirlMath.ResolveInjectorComponents(
            injectorJetVelocity,
            activeDesign.InjectorYawAngleDeg,
            activeDesign.InjectorPitchAngleDeg);

        JetState inletState = new JetState(
            axialPositionM: 0.0,
            pressurePa: rawInlet.PressurePa,
            temperatureK: rawInlet.TemperatureK,
            densityKgM3: rawInlet.DensityKgM3,
            velocityMps: va0,
            areaM2: rawInlet.AreaM2,
            primaryMassFlowKgS: rawInlet.MassFlowKgS,
            entrainedMassFlowKgS: 0.0);

        var entrainment = new EntrainmentModel();
        var mixing = new MixingSectionSolver();
        var marcher = new FlowMarcher(ambient, entrainment, mixing, gas);

        double sectionLengthM = activeDesign.SwirlChamberLengthMm / 1000.0;
        double outletAreaM2 = Math.PI * Math.Pow(activeDesign.ExitDiameterMm / 2000.0, 2);
        double areaIn = inletState.AreaM2;

        double AreaAt(double x)
        {
            double t = sectionLengthM > 1e-12 ? x / sectionLengthM : 0.0;
            t = Math.Clamp(t, 0.0, 1.0);
            return areaIn + (outletAreaM2 - areaIn) * t;
        }

        double PerimeterAt(double x)
        {
            double a = AreaAt(x);
            return 2.0 * Math.Sqrt(Math.PI * Math.Max(a, 1e-15));
        }

        double CaptureAreaAt(double x)
        {
            double a = AreaAt(x);
            return Math.Max(0.18 * a, 1e-9);
        }

        double ldRatio = activeDesign.SwirlChamberLengthMm / Math.Max(activeDesign.SwirlChamberDiameterMm, 1e-6);
        double sInjPre = Math.Abs(vt0) / Math.Max(Math.Abs(va0), 1e-6);
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
            swirlDecayPerStepFactor: swirlDecayPerStep);

        IReadOnlyList<FlowMarchStepResult> steps = detailed.StepResults;
        JetState lastMarch = detailed.FlowStates[^1];

        double etaStator = Math.Min(0.42, activeDesign.StatorVaneAngleDeg / 95.0);
        double fracVt = Math.Clamp(1.0 - 0.38 * etaStator, 0.22, 0.92);
        var stator = new StatorRecoveryModel();
        StatorRecoveryOutput statorOut = stator.Apply(
            detailed.FinalTangentialVelocityMps,
            lastMarch.DensityKgM3,
            etaStator,
            fracVt);

        double pAfterStator = Math.Max(lastMarch.PressurePa + statorOut.RecoveredPressureRisePa, 1.0);
        double vaAfterStator = lastMarch.VelocityMps + statorOut.AxialVelocityGainMps;
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

        double halfRad = activeDesign.ExpanderHalfAngleDeg * (Math.PI / 180.0);
        double rhoExp = lastMarch.DensityKgM3;
        double vaExp = lastMarch.VelocityMps;
        double dPExpander = 0.22 * rhoExp * vaExp * vaExp * Math.Sin(Math.Max(halfRad, 0.02));
        dPExpander = Math.Min(dPExpander, 0.48 * rhoExp * vaExp * vaExp);
        double expanderProjectedAreaM2 = ExpanderAxialProjectedAreaM2(activeDesign);
        double expanderForceN = PressureForceMath.ExpanderOverPressureAxialForce(dPExpander, expanderProjectedAreaM2);

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
            injectorJetVelocity,
            vt0,
            va0,
            swirlDecayPerStep,
            DefaultMarchSteps,
            kTotal,
            detailed,
            lastMarch,
            statorOut,
            etaStator,
            fracVt,
            steps,
            minInletP,
            sumReq,
            sumAct,
            shortfall,
            solvedEr,
            vtAfterStator);

        VortexFlowDiagnostics vortex = ChamberPhysicsPipeline.ToLegacyVortexDiagnostics(chamber, swirlDecayPerStep);

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
            Chamber = chamber
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
            injectorJetVelocity,
            siDiag);

        NozzleCriticalRatiosSnapshot criticalRatios = NozzleCriticalRatios.Compute(
            drivenDesign,
            source,
            solved,
            siDiag);

        IReadOnlyList<string> health = NozzleDesignHealthCheck.Validate(drivenDesign, criticalRatios, siDiag);

        return new SiPathSolveResult(
            drivenDesign,
            solved,
            siDiag,
            criticalRatios,
            health,
            designResult,
            inletState);
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
        Console.WriteLine("---------------------------------------------------------------------");
    }
}
