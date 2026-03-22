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
/// <remarks>
/// Default path uses <see cref="FlowMarcher.SolveDetailed"/> (compressible entrainment, inlet suction, swirl decay)
/// plus first-order stator recovery and expander pressure-force bookkeeping — not CFD.
/// Legacy <see cref="NozzlePhysicsSolver"/> remains in the repo but is not on the default path.
/// </remarks>
public static class NozzleFlowCompositionRoot
{
    private const int DefaultMarchSteps = 24;

    public static PipelineRunResult Run(NozzleInput input, bool showInViewer)
    {
        var gas = new GasProperties();
        var ambient = new AmbientAir(
            gas,
            input.Source.AmbientPressurePa,
            input.Source.AmbientTemperatureK,
            velocityMps: 0.0);

        double pTotal = Math.Max(input.Source.AmbientPressurePa * input.Source.PressureRatio, ambient.PressurePa + 1.0);
        double pStaticJet = input.Source.AmbientPressurePa;
        double tTotal = input.Source.ExhaustTemperatureK ?? input.Source.AmbientTemperatureK;
        double exitAreaM2 = input.Source.SourceOutletAreaMm2 / 1e6;

        var jetSource = new JetSource(
            gas,
            totalPressurePa: pTotal,
            staticPressurePa: pStaticJet,
            totalTemperatureK: tTotal,
            exitAreaM2: exitAreaM2,
            primaryMassFlowKgS: input.Source.MassFlowKgPerSec);

        JetState rawInlet = jetSource.CreateInitialState();

        double sourceAreaMm2 = input.Source.SourceOutletAreaMm2;
        double injectorAreaMm2 = Math.Max(input.Design.TotalInjectorAreaMm2, 1e-9);
        double sourceAreaM2 = sourceAreaMm2 / 1e6;
        double injectorAreaM2 = injectorAreaMm2 / 1e6;
        double coreMdot = input.Source.MassFlowKgPerSec;
        double vCore = input.Source.SourceVelocityMps > 0.0
            ? input.Source.SourceVelocityMps
            : VelocityMath.FromMassFlow(coreMdot, input.Source.AmbientDensityKgPerM3, sourceAreaM2);
        double rhoCore = rawInlet.DensityKgM3;
        double areaDriver = vCore * (sourceAreaMm2 / injectorAreaMm2);
        double continuityCheck = coreMdot / (rhoCore * Math.Max(injectorAreaM2, 1e-12));
        double injectorJetVelocity = NozzlePhysicsSolver.InjectorJetVelocityDriverBlend * areaDriver
            + (1.0 - NozzlePhysicsSolver.InjectorJetVelocityDriverBlend) * continuityCheck;

        var (vt0, va0) = SwirlMath.ResolveInjectorComponents(
            injectorJetVelocity,
            input.Design.InjectorYawAngleDeg,
            input.Design.InjectorPitchAngleDeg);

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

        double sectionLengthM = input.Design.SwirlChamberLengthMm / 1000.0;
        double outletAreaM2 = Math.PI * Math.Pow(input.Design.ExitDiameterMm / 2000.0, 2);
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

        double swirlDecayPerStep = Math.Pow(0.72, 1.0 / Math.Max(DefaultMarchSteps, 1));

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

        double etaStator = Math.Min(0.42, input.Design.StatorVaneAngleDeg / 95.0);
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

        double halfRad = input.Design.ExpanderHalfAngleDeg * (Math.PI / 180.0);
        double rhoExp = lastMarch.DensityKgM3;
        double vaExp = lastMarch.VelocityMps;
        double dPExpander = 0.22 * rhoExp * vaExp * vaExp * Math.Sin(Math.Max(halfRad, 0.02));
        dPExpander = Math.Min(dPExpander, 0.48 * rhoExp * vaExp * vaExp);
        double expanderProjectedAreaM2 = ExpanderAxialProjectedAreaM2(input.Design);
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
            NetThrustN = fNet
        };

        var designer = new NozzleDesigner();
        NozzleDesignResult designResult = designer.CreateDesignResult(
            inletState,
            flowStates,
            outletAreaM2,
            ambient.PressurePa,
            ambient.VelocityMps,
            siDiag);

        NozzleDesignInputs drivenDesign = FlowDrivenNozzleBuilder.BuildDesignInputs(designResult, input.Design);

        NozzleSolvedState solved = NozzleSolvedStateFlowAdapter.FromSiFlow(
            designResult,
            inletState,
            finalOutlet,
            input.Source,
            drivenDesign,
            areaDriver,
            continuityCheck,
            injectorJetVelocity,
            siDiag);

        var geometryBuilder = new NozzleGeometryBuilder();
        NozzleGeometryResult geometry = geometryBuilder.Build(drivenDesign, solved);

        if (showInViewer)
            AppPipeline.DisplayGeometryInViewer(geometry);

        PrintPhysicsSummary(inletState, designResult, siDiag);

        NozzleCriticalRatiosSnapshot criticalRatios = NozzleCriticalRatios.Compute(
            drivenDesign,
            input.Source,
            solved,
            siDiag);

        var warnings = new List<string>
        {
            "SI path: compressible entrainment march + first-order stator/expander bookkeeping (not CFD)."
        };
        warnings.AddRange(NozzleDesignHealthCheck.Validate(drivenDesign, criticalRatios, siDiag));

        NozzleInput effectiveInput = new NozzleInput(input.Source, drivenDesign, input.Run);
        return new PipelineRunResult(effectiveInput, solved, geometry, warnings, siDiag, criticalRatios);
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
