using System.Collections.Generic;
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
        double sourceDiameterMm = AreaMath.CircleDiameterFromAreaMm2(input.Source.SourceOutletAreaMm2);

        Library.Log("=== Physics-first parametric nozzle / ejector estimate ===");
        Library.Log("(Heuristic / first-order — not CFD-calibrated, not test-stand validated.)");

        Library.Log("--- Source + ambient (boundary only, no engine geometry) ---");
        Library.Log($"SourceOutletAreaMm2 [mm2]:    {input.Source.SourceOutletAreaMm2:F2} (authoritative)");
        Library.Log($"Equiv. source diameter [mm]:  {sourceDiameterMm:F2} (derived helper)");
        Library.Log($"CoreMassFlow [kg/s]:          {input.Source.MassFlowKgPerSec:F4}");
        Library.Log($"SourceVelocityMps [m/s]:      {input.Source.SourceVelocityMps:F2}");
        Library.Log($"PressureRatio [-]:            {input.Source.PressureRatio:F2}");
        Library.Log($"ExhaustTemperatureK [K]:      {(input.Source.ExhaustTemperatureK.HasValue ? input.Source.ExhaustTemperatureK.Value.ToString("F2") : "n/a (continuity falls back to ambient density)")}");
        Library.Log($"AmbientPressurePa [Pa]:       {input.Source.AmbientPressurePa:F0}");
        Library.Log($"AmbientTemperatureK [K]:      {input.Source.AmbientTemperatureK:F2} (reporting; not used in current solver equations)");
        Library.Log($"AmbientDensityKgPerM3:        {input.Source.AmbientDensityKgPerM3:F4}");

        Library.Log("--- Design inputs ---");
        Library.Log($"InletDiameterMm:              {input.Design.InletDiameterMm:F2}");
        Library.Log($"SwirlChamber D x L [mm]:      {input.Design.SwirlChamberDiameterMm:F2} x {input.Design.SwirlChamberLengthMm:F2}");
        Library.Log($"TotalInjectorAreaMm2:         {input.Design.TotalInjectorAreaMm2:F2} (continuity uses this)");
        Library.Log($"InjectorCount:                {input.Design.InjectorCount}");
        Library.Log($"Injector W x H [mm]:          {input.Design.InjectorWidthMm:F2} x {input.Design.InjectorHeightMm:F2} (geometry + slot check)");
        Library.Log($"Injector Yaw/Pitch/Roll [deg]: {input.Design.InjectorYawAngleDeg:F2} / {input.Design.InjectorPitchAngleDeg:F2} / {input.Design.InjectorRollAngleDeg:F2}");
        Library.Log($"ExpanderLengthMm / HalfAngle: {input.Design.ExpanderLengthMm:F2} / {input.Design.ExpanderHalfAngleDeg:F2} deg");
        Library.Log($"ExitDiameterMm:               {input.Design.ExitDiameterMm:F2}");
        Library.Log($"Stator vane angle / count:    {input.Design.StatorVaneAngleDeg:F2} deg / {input.Design.StatorVaneCount}");
        Library.Log($"WallThicknessMm:              {input.Design.WallThicknessMm:F2}");

        Library.Log("--- Solved physics ---");
        Library.Log($"CoreGasDensity (injector) [kg/m3]: {s.CoreGasDensityKgPerM3:F4} (heuristic ideal-gas if T_exhaust set)");
        Library.Log($"InjectorJetVelocityMps:       {s.InjectorJetVelocityMps:F2} (mdot = rho_core * A_inj * V)");
        Library.Log($"Vt / Va at injector [m/s]:    {s.TangentialVelocityComponentMps:F2} / {s.AxialVelocityComponentMps:F2}");
        Library.Log($"InjectorSwirlNumber [-]:      {s.InjectorSwirlNumber:F3} (|Vt|/|Va|, not CFD swirl)");
        Library.Log($"ChamberSwirlForStator [-]:    {s.ChamberSwirlNumberForStator:F3} (decayed for stator heuristic only)");
        Library.Log($"Pressure loss fractions [-]:  area {s.PressureLoss.FractionFromInjectorSourceAreaMismatch:F3}, swirl {s.PressureLoss.FractionFromSwirlDissipation:F3}, short L/D {s.PressureLoss.FractionFromShortMixingLength:F3} → total {s.PressureLoss.FractionTotal:F3}");
        Library.Log($"AmbientAirMassFlow [kg/s]:    {s.AmbientAirMassFlowKgPerSec:F4}");
        Library.Log($"EntrainmentRatio [-]:         {s.EntrainmentRatio:F3}");
        Library.Log($"MixedMassFlow [kg/s]:         {s.MixedMassFlowKgPerSec:F4} (= core + entrained)");
        Library.Log($"MixedVelocityMps:             {s.MixedVelocityMps:F2} (momentum + loss + floor)");
        Library.Log($"ExpansionEfficiency [-]:      {s.ExpansionEfficiency:F3} (geometry + expansion ratio, heuristic)");
        Library.Log($"AxialRecoveryEfficiency [-]:  {s.AxialRecoveryEfficiency:F3} (stator vs chamber swirl, applied once)");
        Library.Log($"ExitVelocityMps:              {s.ExitVelocityMps:F2}");
        Library.Log($"SourceOnlyThrustN:            {s.SourceOnlyThrustN:F2} (baseline: mdot_core * V_core)");
        Library.Log($"FinalThrustN:                 {s.FinalThrustN:F2} (mdot_mix * V_exit; pressure thrust omitted)");
        Library.Log($"ExtraThrustN:                 {s.ExtraThrustN:F2}");
        Library.Log($"ThrustGainRatio [-]:          {s.ThrustGainRatio:F3}");

        Library.Log("--- Reference geometry (secondary) ---");
        Library.Log($"Injector placements:          {result.Geometry.InjectorCountPlaced}");
        Library.Log($"TotalLengthMm:                {result.Geometry.TotalLengthMm:F2}");

        LogHeuristicAssumptions();
        LogWarnings(result.SolverWarnings);
    }

    private static void LogHeuristicAssumptions()
    {
        Library.Log("--- Documented heuristic assumptions (full list) ---");
        Library.Log("- Steady, lumped control-volume style; no Navier–Stokes, no chemistry, no shock fitting — not CFD-calibrated.");
        Library.Log("- Core thrust baseline: F0 ≈ mdot_core * V_core (simplification: no pressure-thrust / area term).");
        Library.Log("- Injector jet speed: continuity mdot = rho_core * A_inj * V; rho_core from ideal-gas estimate when ExhaustTemperatureK set.");
        Library.Log("- HEURISTIC: entrainment model — bounded algebraic ejector-like formula (inlet capture, chamber, L/D, swirl number).");
        Library.Log("- HEURISTIC: pressure-loss model — named fractional kinetic losses (area mismatch, swirl dissipation, short chamber); see PressureLossMath.");
        Library.Log("- Mixed velocity: momentum dilution (stagnant secondary air) × (1 - loss_total), then optional numeric floor.");
        Library.Log("- HEURISTIC: expansion efficiency — angle + length + area ratio + PR shaping; not a characteristic nozzle solution.");
        Library.Log("- HEURISTIC: axial recovery — stator maps vane angle/count + decayed swirl to one η (applied once on V after expansion).");
        Library.Log("- Chamber swirl decay before stator: exponential in L/D (placeholder coefficient) — heuristic.");
        Library.Log("- InjectorSwirlNumber = |Vt|/|Va| from yaw/pitch; roll ignored for axisymmetric direction (see SwirlMath).");
        Library.Log("- AmbientTemperatureK is not referenced by the current equations (only P_amb, rho_amb, T_exhaust for rho_core).");
    }

    private static void LogWarnings(IReadOnlyList<string> warnings)
    {
        Library.Log("--- Warnings ---");
        if (warnings.Count == 0)
        {
            Library.Log("(none)");
            return;
        }

        foreach (string w in warnings)
            Library.Log("WARNING: " + w);
    }
}
