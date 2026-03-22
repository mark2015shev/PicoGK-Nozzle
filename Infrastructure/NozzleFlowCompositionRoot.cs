using System;
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
/// <b>Files added (physics + bridge):</b>
/// Physics/GasProperties.cs, AmbientAir.cs, JetState.cs, JetSource.cs, EntrainmentModel.cs,
/// MixingSectionSolver.cs, FlowMarcher.cs, ThrustCalculator.cs, NozzleDesignResult.cs, NozzleDesigner.cs,
/// Geometry/FlowDrivenNozzleBuilder.cs, Core/NozzleSolvedStateFlowAdapter.cs, Infrastructure/NozzleFlowCompositionRoot.cs.
/// <b>Modified:</b> AppPipeline.cs (uses this root + viewer helper), Program.cs (delegates to pipeline).
/// <b>Where physics meets geometry:</b> <see cref="FlowDrivenNozzleBuilder.BuildDesignInputs"/> merges
/// <see cref="NozzleDesignResult"/> with template <see cref="NozzleDesignInputs"/>; <see cref="NozzleGeometryBuilder"/> is unchanged.
/// <b>Still simplified (validate with CFD / test):</b> isentropic jet inlet, uniform ambient static P in mixing,
/// linear duct area, entrainment correlation Ce·ρ·V·P, no swirl / friction / detailed pressure recovery.
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

        JetState inletState = jetSource.CreateInitialState();

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

        IReadOnlyList<JetState> flowStates = marcher.Solve(
            inletState,
            sectionLengthM,
            DefaultMarchSteps,
            AreaAt,
            PerimeterAt);

        var designer = new NozzleDesigner();
        NozzleDesignResult designResult = designer.CreateDesignResult(
            inletState,
            flowStates,
            outletAreaM2,
            ambient.PressurePa,
            ambient.VelocityMps);

        NozzleDesignInputs drivenDesign = FlowDrivenNozzleBuilder.BuildDesignInputs(designResult, input.Design);

        NozzleSolvedState solved = NozzleSolvedStateFlowAdapter.FromSiFlow(
            designResult,
            inletState,
            flowStates[^1],
            input.Source,
            drivenDesign);

        var geometryBuilder = new NozzleGeometryBuilder();
        NozzleGeometryResult geometry = geometryBuilder.Build(drivenDesign, solved);

        if (showInViewer)
            AppPipeline.DisplayGeometryInViewer(geometry);

        PrintPhysicsSummary(inletState, designResult);

        IReadOnlyList<string> warnings = new[]
        {
            "Flow solve uses SI lumped model (NozzleFlowCompositionRoot); not legacy NozzlePhysicsSolver."
        };

        NozzleInput effectiveInput = new NozzleInput(input.Source, drivenDesign, input.Run);
        return new PipelineRunResult(effectiveInput, solved, geometry, warnings);
    }

    private static void PrintPhysicsSummary(JetState inlet, NozzleDesignResult d)
    {
        Console.WriteLine("--- SI flow-driven nozzle summary ---");
        Console.WriteLine($"Inlet jet velocity [m/s]:     {inlet.VelocityMps:F2}");
        Console.WriteLine($"Estimated exit velocity [m/s]: {d.EstimatedExitVelocityMps:F2}");
        Console.WriteLine($"Estimated total mass flow [kg/s]: {d.EstimatedTotalMassFlowKgS:F4}");
        Console.WriteLine($"Estimated thrust [N]:       {d.EstimatedThrustN:F2}");
        Console.WriteLine($"Suggested inlet radius [m]: {d.SuggestedInletRadiusM:F5}");
        Console.WriteLine($"Suggested outlet radius [m]: {d.SuggestedOutletRadiusM:F5}");
        Console.WriteLine($"Suggested mixing length [m]: {d.SuggestedMixingLengthM:F5}");
        Console.WriteLine("-------------------------------------");
    }
}
