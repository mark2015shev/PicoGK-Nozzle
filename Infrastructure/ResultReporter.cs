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
            Library.Log($"Autotune strategy:          {at.Strategy}");
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

        if (result.SiFlow?.InjectorPressureVelocity != null)
            LogInjectorPressureVelocitySection(result.SiFlow.InjectorPressureVelocity);

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

        NozzleGeometryDebugReport geometryAudit = NozzleGeometryDebugReportBuilder.Build(input.Design, result.Geometry);
        NozzleGeometryDebugReportBuilder.WriteReport(geometryAudit, line =>
        {
            Library.Log(line);
            Console.WriteLine(line);
        });

        Library.Log("--- Source → injector momentum assumption (HEURISTIC) ---");
        Library.Log($"V_jet ≈ {NozzlePhysicsSolver.InjectorJetVelocityDriverBlend:F2}×[V_core×(A_source/A_inj)] + {1.0 - NozzlePhysicsSolver.InjectorJetVelocityDriverBlend:F2}×[mdot/(ρ_core×A_inj)] — source drives when A_inj≈A_source.");
        Library.Log($"V_core×(A_source/A_inj):      {s.InjectorJetVelocityAreaDriverMps:F2} m/s");
        Library.Log($"Continuity mdot/(ρ_core×A_inj): {s.InjectorJetVelocityContinuityCheckMps:F2} m/s");
        Library.Log($"Blended InjectorJetVelocityMps (raw driver): {s.InjectorJetVelocityMps:F2} m/s");
        if (result.SiFlow?.Coupling != null)
        {
            SiVortexCouplingDiagnostics c = result.SiFlow.Coupling;
            Library.Log(
                $"Effective injector |V| (Cd·√(1−K_turn)) [m/s]: {c.InjectorJetVelocityEffectiveMps:F2} — used for SI march inlet decomposition.");
        }
        else
            Library.Log("(No SI coupling audit — yaw/pitch uses raw blend only.)");

        if (result.SiFlow?.Chamber != null)
        {
            LogChamberPhysicsSection(result.SiFlow);
            if (result.SiFlow.Coupling != null)
                LogSiCouplingAudit(result.SiFlow);
        }

        if (result.SiFlow?.HubStator != null)
            LogHubStatorSection(result.SiFlow);
        else if (result.SiFlow?.Vortex != null)
            LogLegacyVortexOnly(result.SiFlow.Vortex);

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
            if (sf.ChamberMarch != null)
                LogSwirlChamberMarchSection(sf);
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
        Library.Log($"Stator hub D / L_ax / chord:  {d.StatorHubDiameterMm:F2} / {d.StatorAxialLengthMm:F2} / {d.StatorBladeChordMm:F2} mm (centerbody + blades)");
        Library.Log($"WallThicknessMm:              {d.WallThicknessMm:F2}");
    }

    private static void LogChamberPhysicsSection(SiFlowDiagnostics sf)
    {
        ChamberFirstOrderPhysics ch = sf.Chamber!;
        RadialVortexPressureResult rp = ch.RadialPressure;
        VortexStructureDiagnosticsResult vs = ch.VortexStructure;
        SwirlBudgetResult bu = ch.SwirlBudget;
        SwirlDiffuserRecoveryResult df = ch.DiffuserRecovery;
        InjectorLossResult inj = ch.InjectorLoss;
        StatorLossResult st = ch.StatorLoss;
        EjectorRegimeResult ej = ch.EjectorRegime;

        Library.Log("--- Vortex structure and pressure field (heuristic, not CFD) ---");
        Library.Log("Interpretation: " + ch.InterpretationSummary);
        Library.Log($"Swirl number |Vt|/|Va| [-]:     {vs.InjectorSwirlNumberSimple:F3}");
        Library.Log($"Flux-style swirl S_flux ≈ K·S [-]: {vs.SwirlNumberFluxStyle:F3} (K={vs.FluxGeometryFactorKUsed:F2}, uniform profile assumption)");
        Library.Log($"Vortex classification:         {vs.ClassificationLabel}");
        Library.Log($"Breakdown risk score [-]:      {vs.BreakdownRiskScore:F3}");
        Library.Log($"Composite vortex quality [-]:  {vs.CompositeVortexQuality:F3} (structure model)");
        Library.Log($"Tuning composite quality [-]:   {ch.TuningCompositeQuality:F3} (autotune scalar)");
        Library.Log("--- Radial vortex pressure (mixed forced core + free outer) ---");
        Library.Log($"Core radius estimate [m]:      {rp.CoreRadiusM:F5}  Chamber R [m]: {rp.ChamberRadiusM:F5}");
        Library.Log($"Wall pressure rise [Pa]:        {rp.WallPressureRisePa:F1}");
        Library.Log($"Core pressure drop [Pa]:       {rp.CorePressureDropPa:F1}");
        Library.Log($"Radial |Δp| scale [Pa]:        {rp.EstimatedRadialPressureDeltaPa:F1}  Model: {rp.VortexType}");
        Library.Log($"Radial model notes:            {rp.Notes}");
        Library.Log("--- Swirl budget (velocity scales, first-order) ---");
        Library.Log($"Swirl injected |Vt| [m/s]:      {bu.SwirlInjectedVtMps:F2}");
        Library.Log($"Vt primary after decay [m/s]:  {bu.SwirlAfterChamberDecayVtPrimaryMps:F2}");
        Library.Log($"Vt mixed at chamber end [m/s]: {bu.SwirlMixedAtChamberEndVtMps:F2}");
        Library.Log($"Swirl→entrainment metric [m/s]:{bu.SwirlUsedForEntrainmentMetric:F2}");
        Library.Log($"Swirl into expander metric:    {bu.SwirlRemainingIntoExpanderMetric:F2}");
        Library.Log($"Vt at stator (post-row) [m/s]: {bu.SwirlAtStatorVtMps:F2}");
        Library.Log($"Dissipated metric [m/s]:       {bu.SwirlDissipatedOverallMetric:F2}");
        Library.Log($"k_total decay (audit) [-]:     {bu.KTotalUsed:F4}  {bu.Notes}");
        Library.Log("Swirl fraction buckets [-]:");
        Library.Log($"  dissipated: {ch.FracSwirlDissipated:F3}  entrainment: {ch.FracSwirlForEntrainment:F3}  recovery: {ch.FracSwirlToAxialRecovery:F3}  rem@stator: {ch.FracSwirlRemainingAtStator:F3}");
        Library.Log("--- Swirling diffuser / expander (heuristic) ---");
        Library.Log($"Recovery Cp [-]:               {df.EstimatedPressureRecoveryCoefficient:F3}");
        Library.Log($"Separation risk [-]:           {df.SeparationRiskScore:F3}");
        Library.Log($"Effective recovery eff. [-]:   {df.EffectivePressureRecoveryEfficiency:F3}");
        Library.Log($"Diffuser notes:                {df.Notes}");
        Library.Log("--- Injector / stator losses (same models; thrust path uses coupled η + effective |V|) ---");
        Library.Log($"Injector Δp_loss [Pa]:         {inj.EstimatedTotalPressureLossPa:F1}  Cd={inj.DischargeCoefficient:F3} K_turn={inj.TurningLossCoefficientK:F3}");
        Library.Log($"Injector notes:                {inj.Notes}");
        Library.Log($"Stator incidence mismatch [°]: {st.IncidenceMismatchDeg:F2}  Δp_loss [Pa]: {st.EstimatedTotalPressureLossPa:F1}  K_turn={st.TurningLossK:F3}");
        Library.Log($"Stator recovery η reduction [-]: {st.RecoveryEfficiencyReduction:F3}  {st.Notes}");
        Library.Log("--- Ejector operating regime (heuristic) ---");
        Library.Log($"Regime:                        {FormatEjectorRegime(ej.Regime)}");
        Library.Log($"Regime stress score [-]:       {ej.RegimeScore:F3}");
        Library.Log($"Regime notes:                  {ej.Notes}");
        if (sf.Vortex != null)
        {
            Library.Log($"March decay factor / step:     {sf.Vortex.SwirlDecayPerStepFactorUsed:F4} (legacy audit line)");
        }
    }

    private static void LogSwirlChamberMarchSection(SiFlowDiagnostics sf)
    {
        SwirlChamberMarchDiagnostics m = sf.ChamberMarch!;
        Library.Log("--- Swirl chamber SI march geometry (bore/hub annulus = CAD; no synthetic area ramp) ---");
        Library.Log(
            $"A_inlet {m.AInletMm2:F2} | A_chamber(bore) {m.AChamberBoreMm2:F2} | A_hub {m.AHubMm2:F2} | A_free_ch {m.AFreeChamberMm2:F2} | A_inj {m.AInjTotalMm2:F2} | A_exit {m.AExitMm2:F2} [mm2]");
        Library.Log(
            $"Ratios A_in/A_ch {m.RatioInletToChamber:F3} | A_inj/A_ch {m.RatioInjToChamber:F3} | A_free/A_ch {m.RatioFreeToChamber:F3} | A_exit/A_ch {m.RatioExitToChamber:F3}");
        Library.Log(
            $"Ce_base {m.EntrainmentCeBase:F4} | Ce@step1 {m.EntrainmentCeAtFirstStep:F4} | B_ent (mass demand) {m.EntrainmentMassDemandBoost:F3}");
        Library.Log(
            $"A_capture {m.CaptureAreaM2:E4} m2 | P_entrain {m.EntrainmentPerimeterM:F5} m | A_duct_eff {m.DuctEffectiveAreaM2:E4} m2 (uniform along chamber march)");
        if (sf.MarchSteps.Count > 0)
        {
            FlowMarchStepResult first = sf.MarchSteps[0];
            FlowMarchStepResult mid = sf.MarchSteps[sf.MarchSteps.Count / 2];
            FlowMarchStepResult last = sf.MarchSteps[^1];
            Library.Log(
                $"Per-step A_eff [m2] (first/mid/last): {first.DuctEffectiveAreaM2:E4} / {mid.DuctEffectiveAreaM2:E4} / {last.DuctEffectiveAreaM2:E4}");
            Library.Log(
                $"Per-step Ce (first/mid/last): {first.EntrainmentCeEffective:F4} / {mid.EntrainmentCeEffective:F4} / {last.EntrainmentCeEffective:F4}");
        }

        foreach (string w in m.ValidationWarnings)
            Library.Log(w);
    }

    private static string FormatEjectorRegime(EjectorOperatingRegime r) => r switch
    {
        EjectorOperatingRegime.SubcriticalEntrainment => "subcritical entrainment",
        EjectorOperatingRegime.CriticalEntrainment => "critical entrainment",
        EjectorOperatingRegime.SecondaryChokeLimited => "secondary stream choke-limited",
        EjectorOperatingRegime.CompoundChokingRisk => "compound / double-choking risk",
        EjectorOperatingRegime.OverexpandedPoorAdmittance => "overexpanded / poor admittance",
        _ => r.ToString()
    };

    private static void LogHubStatorSection(SiFlowDiagnostics sf)
    {
        HubStatorFlowDiagnostics h = sf.HubStator!;
        Library.Log("--- Hub-based stator (first-order SI — not CFD) ---");
        Library.Log($"Stator hub diameter [mm]:     {h.StatorHubDiameterMm:F2}  (solid centerbody OD)");
        Library.Log($"Casing inner R at stator [mm]: {h.StatorOuterInnerRadiusMm:F2}");
        Library.Log($"Span ratio (R_out−R_hub)/R_out: {h.SpanRatio:F3}");
        Library.Log($"Blockage A_hub/A_outer_disk:   {h.BlockageAreaRatio:F3}");
        Library.Log($"Geom recovery factor (span×(1−block pen)): {h.HubGeometryRecoveryFactor:F3}");
        Library.Log($"Alignment report factor [-]:    {h.AlignmentFactor:F3} (see incidence coupling in stator loss; informational)");
        Library.Log($"Effective stator η used [-]:    {h.EffectiveStatorEtaUsed:F3}");
        Library.Log($"|Vt| before / after stator [m/s]: {h.SwirlTangentialVelocityBeforeMps:F2} / {h.SwirlTangentialVelocityAfterMps:F2}");
        Library.Log($"Frac. swirl removed by row [-]: {h.FractionSwirlRemovedByStatorRow:F3}");
        Library.Log($"Frac. core/bypass (model) [-]: {h.FractionSwirlCoreBypassFirstOrder:F3}");
        Library.Log($"Frac. dissipated (model) [-]:  {h.FractionSwirlDissipatedFirstOrder:F3}");
        Library.Log($"Frac. to axial KE (model) [-]: {h.FractionSwirlToAxialMomentumFirstOrder:F3}");
    }

    private static void LogSiCouplingAudit(SiFlowDiagnostics sf)
    {
        SiVortexCouplingDiagnostics c = sf.Coupling!;
        SwirlEnergyCouplingLedger e = c.SwirlEnergy;
        Library.Log("--- SI coupled vortex physics (raw vs values used in thrust) — first-order, not CFD ---");
        foreach (string line in c.CouplingSummaryLines)
            Library.Log("Summary: " + line);
        Library.Log($"Injector |V| raw [m/s]:           {c.InjectorJetVelocityRawMps:F2}  effective: {c.InjectorJetVelocityEffectiveMps:F2}");
        Library.Log($"Injector Vt/Va raw [m/s]:        {c.InjectorVtRawMps:F2} / {c.InjectorVaRawMps:F2}");
        Library.Log($"Injector Vt/Va effective [m/s]:  {c.InjectorVtEffectiveMps:F2} / {c.InjectorVaEffectiveMps:F2}");
        Library.Log($"Entrainment demand boost B [-]: {c.EntrainmentDemandBoostFactor:F4}  Δp_core useful [Pa]: {c.DeltaPCoreUsefulForEntrainmentPa:F1}");
        Library.Log($"Stator η base / effective [-]:   {c.StatorEtaBase:F4} / {c.StatorEtaEffective:F4}  K_inc={c.StatorCouplingKIncidence:F3} K_turn={c.StatorCouplingKTurn:F3}");
        Library.Log($"Expander ΔP base / eff [Pa]:     {c.ExpanderDeltaPBasePa:F1} / {c.ExpanderDeltaPEffectivePa:F1}  mult={c.DiffuserRecoveryMultiplier:F3}");
        Library.Log($"Diffuser sep. axial factor [-]: {c.DiffuserSeparationAxialFactor:F3}  V_ax base/eff [m/s]: {c.FinalAxialVelocityBaseMps:F2} / {c.FinalAxialVelocityEffectiveMps:F2}");
        Library.Log("Swirl tangential KE rate audit E_θ=½ṁV_θ² [W]:");
        Library.Log($"  injected raw: {e.EThetaInjectedRaw_W:F1}  after injector loss: {e.EThetaAfterInjectorLoss_W:F1}");
        Library.Log($"  mixed @ chamber end: {e.EThetaAfterChamberDecay_W:F1}  → entrainment debit: {e.EThetaUsedForEntrainment_W:F1}");
        Library.Log($"  diffuser bookkeeping: {e.EThetaUsedForDiffuserRecovery_W:F1}  stator recovery debit: {e.EThetaUsedForStatorRecovery_W:F1}");
        Library.Log($"  exit residual: {e.EThetaExitResidual_W:F1}  dissipated (inj+decay): {e.EThetaDissipated_W:F1}");
        Library.Log($"Net thrust (coupled) [N]:       {sf.NetThrustN:F3}  (same path as autotune evaluation)");
    }

    private static void LogInjectorPressureVelocitySection(InjectorPressureVelocityDiagnostics ip)
    {
        Library.Log("--- Injector pressure and velocity state (first-order reporting — not CFD) ---");
        Library.Log("Do not equate combustor / upstream total pressure with post-injector or chamber mixed static P.");
        Library.Log($"InjectorUpstreamTotalPressurePa [Pa]:     {ip.InjectorUpstreamTotalPressurePa:F1}");
        Library.Log($"PressureRatio (meaning):                  {ip.PressureRatioDefinition}");
        Library.Log($"AmbientStaticPressurePa [Pa]:             {ip.AmbientStaticPressurePa:F1}");
        Library.Log($"JetSourceReferenceStaticPressurePa [Pa]:  {ip.JetSourceReferenceStaticPressurePa:F1} (static passed into JetSource — currently ambient)");
        Library.Log($"MarchInletAssignedStaticPressurePa [Pa]:  {ip.MarchInletAssignedStaticPressurePa:F1} (JetState.PressurePa at march start)");
        Library.Log("Velocities [m/s]:");
        Library.Log($"  Injector jet raw (blend driver):       {ip.InjectorJetVelocityRawMps:F2}");
        Library.Log($"  Injector jet effective (Cd·√(1−K)):    {ip.InjectorJetVelocityEffectiveMps:F2}");
        Library.Log($"  Va_effective / Vt_effective:            {ip.InjectorVaEffectiveMps:F2} / {ip.InjectorVtEffectiveMps:F2}");
        Library.Log($"  |V|_effective from components:         {ip.InjectorVelocityMagnitudeEffectiveMps:F2}");
        Library.Log("Dynamic pressure q = 0.5·ρ·V² [Pa]:");
        Library.Log($"  q from raw |V|:                          {ip.InjectorDynamicPressureRawPa:F1}");
        Library.Log($"  q from effective scalar |V|:            {ip.InjectorDynamicPressureEffectiveScalarPa:F1}");
        Library.Log($"  q from (Va²+Vt²) effective:            {ip.InjectorDynamicPressureFromComponentsPa:F1}");
        Library.Log($"InjectorTotalPressureLossModelPa [Pa]:  {ip.InjectorTotalPressureLossModelPa:F1} (InjectorLossModel vs raw V)");
        Library.Log($"InjectorExitStaticPressureFirstOrderPa:   {ip.InjectorExitStaticPressureFirstOrderPa:F1}");
        Library.Log($"  Assumptions: {ip.InjectorExitStaticPressureAssumptions}");
        Library.Log($"ChamberStaticPressureNearInjectorPa [Pa]: {ip.ChamberStaticPressureNearInjectorPa:F1}");
        Library.Log($"  Note: {ip.ChamberStaticPressureNearInjectorNote}");
        Library.Log("Vortex / core (radial model, first-order):");
        Library.Log($"  CorePressureDropPa [Pa]:                {ip.CorePressureDropPa:F1}");
        Library.Log($"  CoreStaticPressurePa [Pa]:              {ip.CoreStaticPressurePa:F1}  (≈ P_amb − core drop, floored)");
        Library.Log($"  AmbientMinusCorePressurePa [Pa]:        {ip.AmbientMinusCorePressurePa:F1}");
        Library.Log($"Formulas (summary): {ip.FormulasUsedSummary}");
    }

    private static void LogLegacyVortexOnly(VortexFlowDiagnostics vx)
    {
        Library.Log("--- Vortex structure and pressure field (heuristic — not CFD) [legacy compact] ---");
        Library.Log($"Vortex class:                  {vx.StructureClassLabel}");
        Library.Log($"Core pressure depression [Pa]: {vx.CorePressureDepressionPa:F1}");
        Library.Log($"Wall pressure rise [Pa]:      {vx.WallPressureRisePa:F1}");
        Library.Log($"Swirl decay fraction [-]:      {vx.SwirlDecayFractionAlongChamber:F3}");
        Library.Log($"Remaining swirl at stator [-]: {vx.RemainingSwirlFractionAtStator:F3}");
        Library.Log($"Vortex quality metric [-]:     {vx.VortexQualityMetric:F3}");
        Library.Log($"  buckets E/R/R/D: {vx.FractionSwirlForEntrainment:F3} / {vx.FractionSwirlRemainingAtStator:F3} / {vx.FractionSwirlToAxialRecovery:F3} / {vx.FractionSwirlDissipated:F3}");
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
        Library.Log("- Chamber swirl decay: k_total = k_wall + k_mix + k_entrain + k_instability; per-step factor = exp(-k_total·Δx/D). Coefficients in ChamberPhysicsCoefficients.");
        Library.Log("- Radial pressure: mixed forced (core) + free-vortex shell; Ω and Γ matched at r_core. Caps in ChamberPhysicsCoefficients.RadialPressureCapPa.");
        Library.Log("- Diffuser: Cp heuristic vs angle, L/D, area ratio; swirl can aid or hurt separation in SwirlDiffuserRecoveryModel.");
        Library.Log("- Injector/stator losses: Δp ~ K·0.5ρV² diagnostics; do not replace blade-row CFD.");
        Library.Log("- Ejector regime: Mach, choking flags, entrainment shortfall — classification only.");
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
