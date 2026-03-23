using System;
using System.Collections.Generic;
using PicoGK;
using PicoGK_Run.Core;
using PicoGK_Run.Geometry;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Infrastructure;

internal static class ResultReporter
{
    public static void Report(PipelineRunResult result)
    {
        NozzleInput input = result.Input;
        NozzleSolvedState s = result.Solved;
        double sourceDiameterMm = AreaMath.CircleDiameterFromAreaMm2(input.Source.SourceOutletAreaMm2);

        Library.Log("=== Nozzle / ejector estimate (SI flow drives geometry) ===");
        if (result.Autotune != null)
        {
            AutotuneRunSummary at = result.Autotune;
            Library.Log("--- Autotune (SI search — pre-CFD) ---");
            Library.Log($"Autotune enabled:            yes");
            Library.Log($"Trials (SI-only evals):      {at.Trials}");
            Library.Log($"Best composite score [-]:    {at.BestScore:F4}");
            Library.Log("Original hand/template design (pre-autotune baseline):");
            LogDesignBlock(at.BaselineTemplateDesign);
            Library.Log("Winning seed design (used for final SI + voxels) [mm / deg]:");
            LogDesignBlock(at.WinningSeedDesign);
        }
        else if (input.Run.UsePhysicsInformedGeometry)
            Library.Log("Design: PHYSICS-INFORMED pre-size (NozzleGeometrySynthesis) — diameters/lengths/expander/stator from source + heuristics; template yaw/pitch/count/wall kept.");
        Library.Log("Flow: lumped isentropic jet + compressible entrainment march (NozzleFlowCompositionRoot). Not CFD.");

        Library.Log("--- Source + ambient (boundary only, no engine geometry) ---");
        Library.Log($"SourceOutletAreaMm2 [mm2]:    {input.Source.SourceOutletAreaMm2:F2} (authoritative)");
        Library.Log($"Equiv. source diameter [mm]:  {sourceDiameterMm:F2} (derived helper)");
        Library.Log($"CoreMassFlow [kg/s]:          {input.Source.MassFlowKgPerSec:F4}");
        Library.Log($"SourceVelocityMps [m/s]:      {input.Source.SourceVelocityMps:F2}");
        Library.Log($"PressureRatio [-]:            {input.Source.PressureRatio:F2}");
        Library.Log($"ExhaustTemperatureK [K]:      {(input.Source.ExhaustTemperatureK.HasValue ? input.Source.ExhaustTemperatureK.Value.ToString("F2") : "n/a (ρ_core blend uses ambient density)")}");
        Library.Log($"AmbientPressurePa [Pa]:       {input.Source.AmbientPressurePa:F0}");
        Library.Log($"AmbientTemperatureK [K]:      {input.Source.AmbientTemperatureK:F2} (reporting; not used in current solver equations)");
        Library.Log($"AmbientDensityKgPerM3:        {input.Source.AmbientDensityKgPerM3:F4}");

        Library.Log(result.Autotune != null
            ? "--- Design inputs (final pipeline — tuned seed after SI merge; primary dims preserved from seed) ---"
            : "--- Design inputs (final pipeline / driven geometry) ---");
        LogDesignBlock(input.Design);

        if (result.CriticalRatios != null)
        {
            NozzleCriticalRatiosSnapshot cr = result.CriticalRatios;
            Library.Log("--- Four critical ratios (design envelope — heuristic, not CFD) ---");
            Library.Log("R1 σ = A_inlet/A_chamber (capture openness vs bore):");
            Library.Log($"    CaptureToChamberAreaRatio [-]: {cr.CaptureToChamberAreaRatio:F3}");
            Library.Log("R2 S = |Vt|/|Va| at injector (swirl injection intensity):");
            Library.Log($"    InjectorSwirlNumber [-]:       {cr.InjectorSwirlNumber:F3}");
            Library.Log("R3 Λ = L_chamber / D_chamber (mixing slenderness):");
            Library.Log($"    ChamberSlendernessLD [-]:      {cr.ChamberSlendernessLD:F3}");
            Library.Log($"    A_inj / A_chamber [-]:         {cr.InjectorPortToChamberAreaRatio:F3}");
            Library.Log("R4 Expander + exit / stator hints:");
            Library.Log($"    ExpanderHalfAngleDeg [deg]:    {cr.ExpanderHalfAngleDeg:F2}");
            Library.Log($"    R_expander_end [mm]:           {cr.ExpanderEndInnerRadiusMm:F2}  R_exit_target [mm]: {cr.ExitTargetInnerRadiusMm:F2}");
            Library.Log($"    |R_exp−R_exit|/R_chamber [-]:  {cr.ExpanderExitToTargetRadiusMismatchRatio:F3}");
            Library.Log($"    |stator−injector yaw| [deg]:   {cr.StatorToInjectorYawMismatchDeg:F1}");
            if (cr.SolvedEntrainmentRatio.HasValue)
                Library.Log($"    Solved ṁ_amb/ṁ_core [-]:       {cr.SolvedEntrainmentRatio.Value:F3}");
            Library.Log("Tune σ, S, Λ, and expander angle together; see health warnings below.");
        }

        Library.Log("--- Geometry meaning (viewer) ---");
        Library.Log("Injector geometry = REFERENCE MARKERS ONLY (beams): not flow passages, not meshed holes.");
        Library.Log("Future: replace with real injector port solids when modeling passages.");

        Library.Log("--- Source → injector momentum assumption (HEURISTIC) ---");
        Library.Log($"V_jet ≈ {NozzlePhysicsSolver.InjectorJetVelocityDriverBlend:F2}×[V_core×(A_source/A_inj)] + {1.0 - NozzlePhysicsSolver.InjectorJetVelocityDriverBlend:F2}×[mdot/(ρ_core×A_inj)] — source drives when A_inj≈A_source.");
        Library.Log($"V_core×(A_source/A_inj):      {s.InjectorJetVelocityAreaDriverMps:F2} m/s");
        Library.Log($"Continuity mdot/(ρ_core×A_inj): {s.InjectorJetVelocityContinuityCheckMps:F2} m/s");
        Library.Log($"Blended InjectorJetVelocityMps: {s.InjectorJetVelocityMps:F2} m/s (used for yaw/pitch decomposition)");

        if (result.SiFlow?.Vortex != null)
        {
            VortexFlowDiagnostics vx = result.SiFlow.Vortex;
            Library.Log("--- Vortex structure and pressure field (heuristic — not CFD) ---");
            Library.Log("Radial pressure: first-order dp/dr ~ rho*Vtheta^2/r sense; wall rise vs core depression split for reporting only.");
            Library.Log($"Vortex class:                  {vx.StructureClassLabel}");
            Library.Log($"Core pressure depression [Pa]: {vx.CorePressureDepressionPa:F1}");
            Library.Log($"Wall pressure rise [Pa]:      {vx.WallPressureRisePa:F1}");
            Library.Log($"Swirl decay fraction [-]:      {vx.SwirlDecayFractionAlongChamber:F3} (primary Vtheta along chamber)");
            Library.Log($"Remaining swirl at stator [-]: {vx.RemainingSwirlFractionAtStator:F3} (|Vt| scale vs injector |Vt|)");
            Library.Log($"Estimated recovery fraction [-]: {vx.EstimatedRecoveryFraction:F3} (stator / axial recovery bucket)");
            Library.Log($"Vortex quality metric [-]:     {vx.VortexQualityMetric:F3} (moderate S, stable regime, ER, recovery — tuning hook)");
            Library.Log("Swirl budget split (four buckets, normalized — bookkeeping):");
            Library.Log($"  for entrainment:             {vx.FractionSwirlForEntrainment:F3}");
            Library.Log($"  remaining at stator plane:   {vx.FractionSwirlRemainingAtStator:F3}");
            Library.Log($"  to axial recovery:           {vx.FractionSwirlToAxialRecovery:F3}");
            Library.Log($"  dissipated / lost:           {vx.FractionSwirlDissipated:F3}");
            Library.Log($"March swirl decay factor/step: {vx.SwirlDecayPerStepFactorUsed:F4} (audit)");
        }

        Library.Log("--- Solved physics ---");
        if (result.SiFlow != null)
        {
            SiFlowDiagnostics sf = result.SiFlow;
            Library.Log("--- SI compressible march (first-order estimate; not CFD-calibrated) ---");
            Library.Log($"Min inlet static P (entrainment solve) [Pa]: {sf.MinInletLocalStaticPressurePa:F1}");
            Library.Log($"Max inlet Mach (entrained stream) [-]: {sf.MaxInletMach:F4}");
            Library.Log($"Any entrainment step choked: {sf.AnyEntrainmentStepChoked}");
            Library.Log($"Σ requested Δṁ_ent [kg/s]:     {sf.SumRequestedEntrainmentIncrementsKgS:F6}");
            Library.Log($"Σ actual Δṁ_ent [kg/s]:       {sf.SumActualEntrainmentIncrementsKgS:F6}");
            Library.Log($"Entrainment shortfall Σ [kg/s]: {sf.EntrainmentShortfallSumKgS:F6} (requested − actual, per-step sum)");
            Library.Log($"Inlet axial pressure force [N]: {sf.InletAxialPressureForceN:F3} (Σ ΔP·A_capture per step, first-order)");
            Library.Log($"Expander axial pressure force [N]: {sf.ExpanderAxialPressureForceN:F3} (ΔP_exp·A_proj, heuristic ΔP)");
            Library.Log($"Stator recovered pressure rise [Pa]: {sf.StatorRecoveredPressureRisePa:F2} (η·Δ tangential KE → p, bounded)");
            Library.Log($"Momentum thrust [N]:          {sf.MomentumThrustN:F3} (ṁ·V_axial CV)");
            Library.Log($"Pressure thrust [N]:          {sf.PressureThrustN:F3} (exit plane + inlet + expander terms)");
            Library.Log($"Net thrust [N]:               {sf.NetThrustN:F3}");
            Library.Log($"March steps recorded:         {sf.MarchSteps.Count}");
        }

        Library.Log($"CoreGasDensity ρ_core [kg/m3]: {s.CoreGasDensityKgPerM3:F4} (heuristic ideal gas when T_exhaust set; blend only)");
        Library.Log($"Vt / Va at injector [m/s]:    {s.TangentialVelocityComponentMps:F2} / {s.AxialVelocityComponentMps:F2}");
        Library.Log($"InjectorSwirlNumber [-]:      {s.InjectorSwirlNumber:F3} (|Vt|/|Va|, not CFD swirl)");
        Library.Log($"ChamberSwirlForStator [-]:    {s.ChamberSwirlNumberForStator:F3} (after inlet + expander pressure budget + tangential debit; stator heuristic)");
        Library.Log("--- Inlet suction / low-pressure (HEURISTIC — not CFD; not free energy) ---");
        Library.Log("Low-pressure inlet/core region: modeled as suction/capture recovery from same swirl/pressure budget as entrainment + expander.");
        Library.Log($"InletSuctionDeltaP_Pa:        {s.InletSuctionDeltaPPa:F1} (ρ, Vt, inlet vs chamber dia; bounded)");
        Library.Log($"InletCaptureEfficiency [-]:   {s.InletCaptureEfficiency:F4} (entrainment mult vs baseline; capped ~1.09)");
        Library.Log($"InletPressureThrustComponentN: {s.InletPressureThrustComponentN:F2} (annulus × Δp, tiny axial term, budget-debited)");
        Library.Log($"PressureRecoveryBudgetAfterInlet [-]: {s.PressureRecoveryBudgetAfterInlet:F3} (remaining before expander wall recovery)");
        Library.Log($"RemainingPressureRecoveryBudget [-]: {s.RemainingPressureRecoveryBudget:F3} (after inlet + expander tap vs Vt ref)");
        Library.Log("--- Swirl-pressure recovery (expander) — HEURISTIC, not CFD; not centrifugal thrust ---");
        Library.Log($"SwirlPressureRisePa:          {s.SwirlPressureRisePa:F1} (order rho*v_theta^2/r wall rise, bounded)");
        Library.Log($"ExpanderWallAxialForceN:      {s.ExpanderWallAxialForceN:F2} (axial wall component only, capped)");
        Library.Log($"SwirlPressureRecoveryEff. [-]: {s.SwirlPressureRecoveryEfficiency:F3} (tangential KE fraction tapped, capped)");
        Library.Log($"Rem.TangentialAfterPR [m/s]:  {s.RemainingTangentialVelocityAfterPressureRecovery:F2} (before stator)");
        Library.Log($"Pressure loss fractions [-]:  area {s.PressureLoss.FractionFromInjectorSourceAreaMismatch:F3}, swirl {s.PressureLoss.FractionFromSwirlDissipation:F3}, short L/D {s.PressureLoss.FractionFromShortMixingLength:F3} → total {s.PressureLoss.FractionTotal:F3}");
        Library.Log($"Dominant loss contribution:   {s.DominantPressureLossContribution}");
        Library.Log($"AmbientAirMassFlow [kg/s]:    {s.AmbientAirMassFlowKgPerSec:F4}");
        Library.Log($"EntrainmentRatio [-]:         {s.EntrainmentRatio:F3}");
        Library.Log($"MixedMassFlow [kg/s]:         {s.MixedMassFlowKgPerSec:F4} (= core + entrained)");
        Library.Log($"MixedVelocityMps:             {s.MixedVelocityMps:F2} (momentum + loss + floor)");
        Library.Log($"ExpansionEfficiency [-]:      {s.ExpansionEfficiency:F3} (geometry + expansion ratio, heuristic)");
        Library.Log($"AxialRecoveryEfficiency [-]:  {s.AxialRecoveryEfficiency:F3} (stator vs swirl angle match, heuristic, capped)");
        Library.Log($"ExitVelocityMps:              {s.ExitVelocityMps:F2}");
        Library.Log($"SourceOnlyThrustN:            {s.SourceOnlyThrustN:F2} (baseline: mdot_core * V_core)");
        Library.Log($"MomentumThrustComponentN:     {s.MomentumThrustComponentN:F2} (mdot_mix * V_exit)");
        Library.Log($"PressureThrustComponentN:     {s.PressureThrustComponentN:F2} (expander wall {s.ExpanderWallAxialForceN:F2} + inlet {s.InletPressureThrustComponentN:F2})");
        Library.Log($"FinalThrustN:                 {s.FinalThrustN:F2} (momentum + pressure components; CV-style)");
        Library.Log($"ExtraThrustN:                 {s.ExtraThrustN:F2}");
        Library.Log($"ThrustGainRatio [-]:          {s.ThrustGainRatio:F3}");

        Library.Log("--- Reference geometry (secondary) ---");
        Library.Log($"Injector reference markers:   {result.Geometry.InjectorCountPlaced}");
        Library.Log($"TotalLengthMm:                {result.Geometry.TotalLengthMm:F2}");

        Library.Log("--- Viewer segment colors (when ShowInViewer) ---");
        Library.Log($"Roughness={NozzleViewerSegmentColors.Roughness:F2}, Metallic={NozzleViewerSegmentColors.Metallic:F2} (all segments)");
        int gi = 1;
        foreach ((string name, string hex) in NozzleViewerSegmentColors.Segments)
        {
            Library.Log($"  Group {gi}: {name,-22} {hex}");
            gi++;
        }

        LogHeuristicAssumptions();
        LogWarnings(result.SolverWarnings);
    }

    private static void LogDesignBlock(NozzleDesignInputs d)
    {
        Library.Log($"InletDiameterMm:              {d.InletDiameterMm:F2}");
        Library.Log($"SwirlChamber D x L [mm]:      {d.SwirlChamberDiameterMm:F2} x {d.SwirlChamberLengthMm:F2}");
        Library.Log($"InjectorAxialPositionRatio:   {d.InjectorAxialPositionRatio:F3} (0=upstream chamber, 1=downstream / near expander)");
        Library.Log($"TotalInjectorAreaMm2:         {d.TotalInjectorAreaMm2:F2}");
        Library.Log($"InjectorCount:                {d.InjectorCount}");
        Library.Log($"Injector W x H [mm]:          {d.InjectorWidthMm:F2} x {d.InjectorHeightMm:F2}");
        Library.Log($"Injector Yaw/Pitch/Roll [deg]: {d.InjectorYawAngleDeg:F2} / {d.InjectorPitchAngleDeg:F2} / {d.InjectorRollAngleDeg:F2}");
        Library.Log($"ExpanderLengthMm / HalfAngle: {d.ExpanderLengthMm:F2} / {d.ExpanderHalfAngleDeg:F2} deg");
        Library.Log($"ExitDiameterMm:               {d.ExitDiameterMm:F2}");
        Library.Log($"Stator vane angle / count:    {d.StatorVaneAngleDeg:F2} deg / {d.StatorVaneCount}");
        Library.Log($"WallThicknessMm:              {d.WallThicknessMm:F2}");
    }

    private static void LogHeuristicAssumptions()
    {
        Library.Log("--- Documented heuristic assumptions (full list) ---");
        Library.Log("- Steady, lumped control-volume style; no Navier–Stokes, no chemistry, no shock fitting — not CFD-calibrated.");
        Library.Log("- Core thrust baseline: F0 ≈ mdot_core * V_core (no pressure-thrust / area term).");
        Library.Log("- Injector jet speed: HEURISTIC blend of V_core×(A_source/A_inj) and mdot/(ρ_core A_inj); source speed drives when areas match.");
        Library.Log("- SI default path: entrainment correlation (Ce·ρ·V·P) per step + compressible intake solve (sonic/choked cap); mixed static P mass-weighted vs ambient P — first-order, not CFD.");
        Library.Log("- HEURISTIC (legacy solver): entrainment — bounded formula using V_core scale, inlet/chamber areas, L/D, InjectorAxialPositionRatio, swirl number; optional small inlet-suction capture bump from same budget.");
        Library.Log("- HEURISTIC: pressure-loss — named fractions; swirl term uses saturating S/(1+S) form, not S²-dominated.");
        Library.Log("- Mixed velocity: axial momentum dilution × (1 - loss_total), then optional numeric floor.");
        Library.Log("- HEURISTIC: expansion efficiency — angle + length + area ratio + PR; not a characteristic nozzle solution.");
        Library.Log("- HEURISTIC: stator recovery — vane angle vs implied swirl turning angle, capped η (no full energy recovery).");
        Library.Log("- HEURISTIC: inlet low-pressure / suction (post-mix) — capture + tiny annulus thrust only; debited from shared pressure-recovery budget before expander; not CFD.");
        Library.Log("- HEURISTIC: swirl-pressure recovery on angled expander walls (post-mix, pre-stator) — NOT CFD, NOT centrifugal thrust; tangential budget reduced before stator.");
        Library.Log("- Chamber swirl decay before stator: exponential in L/D — heuristic; then inlet + expander debits on same swirl/pressure budget (no stacked full recovery).");
        Library.Log("- Yaw/pitch/roll: see SwirlMath XML; roll ignored for axisymmetric physics.");
        Library.Log("- AmbientTemperatureK not used in equations (P_amb, rho_amb, T_exhaust for ρ_core blend).");
        Library.Log("- Vortex diagnostics: radial pressure / core depression / swirl budget split are 1-D heuristics for controlled-vortex interpretation — not CFD vortex identification or stability.");
        Library.Log("- Chamber swirl decay factor: L/D, diameter scale, entrainment-loading hint — tunable later against experiment or CFD.");
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
