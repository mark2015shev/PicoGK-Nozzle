using System;
using PicoGK_Run.Geometry;
using PicoGK_Run.Physics;
using PicoGK_Run.Physics.Solvers;

namespace PicoGK_Run.Infrastructure;

/// <summary>Single-place engineering narrative for console / Library.Log.</summary>
public static class RunReportBuilder
{
    public static void LogEngineeringReport(
        NozzlePhysicsStageResult? stages,
        GeometryContinuityReport? geometry,
        Action<string> log,
        SiFlowDiagnostics? siFlow = null)
    {
        log("=== Engineering SI report (staged swirl-vortex path — not CFD) ===");
        if (stages?.Stage1Injector != null)
        {
            InjectorDischargeResult i = stages.Stage1Injector;
            log($"[1] Injector ṁ [kg/s]:        {i.MassFlowKgS:F4}  (source-authoritative when set)");
            log($"[1] |V| continuity [m/s]:   {i.VelocityMagnitudeFromContinuityMps:F2}  effective |V| [m/s]: {i.EffectiveVelocityMagnitudeMps:F2}");
            log($"[1] V_t / V_a [m/s]:         {i.TangentialVelocityMps:F2} / {i.AxialVelocityMps:F2}");
            log($"[1] Swirl |V_t|/|V_a| [-]:   {i.SwirlNumberVtOverVa:F3}");
            log($"[1] ΔP drive / implied [Pa]: {i.DrivingPressureDropPa:F0} / {i.ImpliedDeltaPFromMassFlowPa:F0}");
            log($"[1] Note: {i.Notes}");
        }

        log($"[2] Swirl number (injector) [-]: {stages?.Stage2SwirlNumberAtInjector ?? 0:F3}");
        log($"[3] Core Δp vs wall [Pa]:      drop {stages?.Stage3CorePressureDropPa ?? 0:F1}  wall rise {stages?.Stage3WallPressureRisePa ?? 0:F1}");
        log($"[3] Est. core static [Pa]:     {stages?.Stage3EstimatedCoreStaticPressurePa ?? 0:F1}");
        log($"[4] Ambient inflow potential [kg/s]: {stages?.Stage4AmbientInflowPotentialKgS ?? 0:F4} (swirl-pressure drive scale)");
        log($"[4] Ambient inflow actual ΣΔṁ [kg/s]: {stages?.Stage4AmbientInflowActualIntegratedKgS ?? 0:F4} (compressible march)");
        log($"[5] Mixed @ chamber end ṁ [kg/s]: {stages?.Stage5MixedMassFlowKgS ?? 0:F4}");
        log($"[5] V_ax / V_t mixed [m/s]:    {stages?.Stage5MixedAxialVelocityMps ?? 0:F2} / {stages?.Stage5MixedTangentialVelocityMps ?? 0:F2}");
        log($"[6] Diffuser Δp_eff [Pa]:      {stages?.Stage6DiffuserPressureRiseEffectivePa ?? 0:F1}  mult [-]: {stages?.Stage6DiffuserRecoveryMultiplier ?? 0:F3}");
        log($"[7] Stator Δp_rec [Pa]:        {stages?.Stage7StatorRecoveredPressureRisePa ?? 0:F2}  η_eff [-]: {stages?.Stage7StatorEtaEffective ?? 0:F3}");
        log($"[7] V_ax after stator [m/s]:   {stages?.Stage7AxialVelocityAfterMps ?? 0:F2}");
        log($"Final exit V_ax [m/s]:       {stages?.FinalExitAxialVelocityMps ?? 0:F2}  ṁ_total [kg/s]: {stages?.FinalTotalMassFlowKgS ?? 0:F4}");

        if (geometry != null)
        {
            log(geometry.IsAcceptable
                ? "Geometry continuity: OK."
                : "Geometry continuity: ISSUES —");
            foreach (string iss in geometry.Issues)
                log("  " + iss);
        }

        if (siFlow?.ChamberMarch != null)
        {
            var m = siFlow.ChamberMarch;
            log("--- Areas (mm2) / continuity helpers ---");
            log(
                $"A_inlet {m.AInletMm2:F1}  A_ch_bore {m.AChamberBoreMm2:F1}  A_free_ch {m.AFreeChamberMm2:F1}  A_inj {m.AInjTotalMm2:F1}  A_exit {m.AExitMm2:F1}");
            log(
                $"D_expander_end / D_exit from design vs chamber: see critical ratios; P_ent {m.EntrainmentPerimeterM:F4} m  A_cap {m.CaptureAreaM2:E3} m2");
        }
    }
}
