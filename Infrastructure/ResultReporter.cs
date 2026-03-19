using System;
using PicoGK;
using PicoGK_Run.Core;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Infrastructure;

internal static class ResultReporter
{
    public static void Report(PipelineRunResult result)
    {
        NozzleInput input = result.Input;
        NozzleSolvedState s = result.Solved;
        double sourceDiameterMm = input.Source.SourceOutletDiameterMm ?? AreaMath.CircleDiameterFromAreaMm2(input.Source.SourceOutletAreaMm2);

        Library.Log("=== Parametric Nozzle Solver Report ===");
        Library.Log("--- Source Inputs ---");
        Library.Log($"Source Outlet Area [mm2]:     {input.Source.SourceOutletAreaMm2:F2}");
        Library.Log($"Source Outlet Diameter [mm]:  {sourceDiameterMm:F2} (helper)");
        Library.Log($"Mass Flow [kg/s]:             {input.Source.MassFlowKgPerSec:F4}");
        Library.Log($"Source Velocity [m/s]:        {input.Source.SourceVelocityMps:F2}");
        Library.Log($"Pressure Ratio [-]:           {input.Source.PressureRatio:F2}");
        Library.Log($"Exhaust Temperature [K]:      {input.Source.ExhaustTemperatureK:F2}");

        Library.Log("--- Ambient Inputs ---");
        Library.Log($"Pressure [Pa]:                {input.Ambient.PressurePa:F0}");
        Library.Log($"Temperature [K]:              {input.Ambient.TemperatureK:F2}");
        Library.Log($"Density [kg/m3]:              {input.Ambient.DensityKgPerM3:F4}");

        Library.Log("--- Nozzle Design Inputs ---");
        Library.Log($"Inlet Diameter [mm]:          {input.Design.InletDiameterMm:F2}");
        Library.Log($"Swirl Chamber D x L [mm]:     {input.Design.SwirlChamberDiameterMm:F2} x {input.Design.SwirlChamberLengthMm:F2}");
        Library.Log($"Total Injector Area [mm2]:    {input.Design.TotalInjectorAreaMm2:F2}");
        Library.Log($"Injector Count [-]:           {input.Design.InjectorCount}");
        Library.Log($"Injector Width x Height [mm]: {input.Design.InjectorWidthMm:F2} x {input.Design.InjectorHeightMm:F2}");
        Library.Log($"Injector Yaw/Pitch/Roll [deg]: {input.Design.InjectorYawAngleDeg:F2} / {input.Design.InjectorPitchAngleDeg:F2} / {input.Design.InjectorRollAngleDeg:F2}");
        Library.Log($"Expander Length [mm]:         {input.Design.ExpanderLengthMm:F2}");
        Library.Log($"Expander Half-Angle [deg]:    {input.Design.ExpanderHalfAngleDeg:F2}");
        Library.Log($"Exit Diameter [mm]:           {input.Design.ExitDiameterMm:F2}");
        Library.Log($"Stator Vane Angle [deg]:      {input.Design.StatorVaneAngleDeg:F2}");
        Library.Log($"Stator Vane Count [-]:        {input.Design.StatorVaneCount}");

        Library.Log("--- Derived Physics ---");
        Library.Log($"Source Area [mm2]:            {s.SourceAreaMm2:F2}");
        Library.Log($"Total Injector Area [mm2]:    {s.TotalInjectorAreaMm2:F2}");
        Library.Log($"Injector Jet Velocity [m/s]:  {s.InjectorJetVelocityMps:F2}");
        Library.Log($"Tangential Velocity [m/s]:    {s.TangentialVelocityComponentMps:F2}");
        Library.Log($"Axial Velocity [m/s]:         {s.AxialVelocityComponentMps:F2}");
        Library.Log($"Swirl Strength [-]:           {s.SwirlStrength:F3}");
        Library.Log($"Ambient Entrainment [kg/s]:   {s.AmbientAirMassFlowKgPerSec:F4}");
        Library.Log($"Entrainment Ratio [-]:        {s.EntrainmentRatio:F3}");
        Library.Log($"Mixed Mass Flow [kg/s]:       {s.MixedMassFlowKgPerSec:F4}");
        Library.Log($"Mixed Velocity [m/s]:         {s.MixedVelocityMps:F2}");
        Library.Log($"Expansion Efficiency [-]:     {s.ExpansionEfficiency:F3}");
        Library.Log($"Axial Recovery Efficiency [-]: {s.AxialRecoveryEfficiency:F3}");
        Library.Log($"Exit Velocity [m/s]:          {s.ExitVelocityMps:F2}");
        Library.Log($"Source-Only Thrust [N]:       {s.SourceOnlyThrustN:F2}");
        Library.Log($"Final Thrust [N]:             {s.FinalThrustN:F2}");
        Library.Log($"Extra Thrust [N]:             {s.ExtraThrustN:F2}");
        Library.Log($"Thrust Gain Ratio [-]:        {s.ThrustGainRatio:F3}");
        Library.Log("--- Heuristic Notes ---");
        Library.Log("Assumes thrust ~= mdot * V (pressure thrust term omitted in this first-order model).");
        Library.Log("Entrainment and recovery terms are geometry-informed heuristics, not CFD-calibrated.");

        Library.Log("--- Geometry ---");
        Library.Log($"Injector Placements [-]:      {result.Geometry.InjectorCountPlaced}");
        Library.Log($"Total Length [mm]:            {result.Geometry.TotalLengthMm:F2}");

        EmitWarnings(result);
    }

    private static void EmitWarnings(PipelineRunResult result)
    {
        NozzleInput i = result.Input;
        NozzleSolvedState s = result.Solved;

        if (i.Design.TotalInjectorAreaMm2 > i.Source.SourceOutletAreaMm2)
            Library.Log("WARNING: TotalInjectorAreaMm2 exceeds SourceOutletAreaMm2.");

        double injectorAreaFromSlots = i.Design.InjectorCount * i.Design.InjectorWidthMm * i.Design.InjectorHeightMm;
        double mismatch = Math.Abs(injectorAreaFromSlots - i.Design.TotalInjectorAreaMm2) / Math.Max(i.Design.TotalInjectorAreaMm2, 1e-9);
        if (mismatch > 0.20)
            Library.Log("WARNING: InjectorCount*Width*Height differs significantly from TotalInjectorAreaMm2.");

        if (s.EntrainmentRatio < 0.05 && i.Design.InletDiameterMm > 1.2 * (i.Source.SourceOutletDiameterMm ?? AreaMath.CircleDiameterFromAreaMm2(i.Source.SourceOutletAreaMm2)))
            Library.Log("WARNING: Large inlet with low entrainment; chamber or injector settings may be limiting mixing.");

        if (Math.Abs(i.Design.InjectorYawAngleDeg) > 90.0 ||
            Math.Abs(i.Design.InjectorPitchAngleDeg) > 45.0 ||
            Math.Abs(i.Design.InjectorRollAngleDeg) > 45.0)
            Library.Log("WARNING: Injector angles are outside typical practical bounds.");

        if (s.ExtraThrustN < 0.0)
            Library.Log("WARNING: Design currently predicts negative extra thrust versus source-only flow.");
    }
}

