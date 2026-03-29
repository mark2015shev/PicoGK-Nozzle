using System;
using System.Globalization;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

/// <summary>
/// Replays <see cref="NozzleGeometryBuilder"/> assembly math (same overlap + builder formulas) for console audit.
/// </summary>
public static class NozzleGeometryDebugReportBuilder
{
    public const double RadiusMatchToleranceMm = 0.51;
    public const double DiameterJumpWarnMm = 1.0;

    public static NozzleGeometryDebugReport Build(
        NozzleDesignInputs d,
        NozzleGeometryResult? builtGeometry = null,
        RunConfiguration? run = null)
    {
        GeometryAssemblyPath path = GeometryAssemblyPath.Compute(d, run);
        DownstreamGeometryTargets tgt = DownstreamGeometryResolver.Resolve(d, run);
        double overlap = path.OverlapMm;
        var segments = new List<GeometrySegmentDebugInfo>();
        var explanations = new List<string>();
        var warnings = new List<string>();

        double wall = path.WallMm;
        double chamberD = Math.Max(d.SwirlChamberDiameterMm, 1.0);
        SwirlChamberPlacement sp = path.SwirlPlacement;
        double chamberLen = sp.PhysicalChamberLengthBuiltMm;
        double chamberInnerR = path.ChamberInnerRadiusMm;
        double entranceInnerR = path.EntranceInnerRadiusMm;

        double x = path.XInletStart;
        double xLipEnd = path.XLipEnd;
        double xFlareEnd = path.XAfterInlet;
        double xAfterInlet = path.XAfterInlet;

        // --- Inlet lip ---
        double? flareHalfAngleDeg = entranceInnerR > chamberInnerR + 1e-9
            ? RadiansToDeg(Math.Atan((entranceInnerR - chamberInnerR) / Math.Max(path.FlareLengthMm, 1e-6)))
            : null;
        segments.Add(MkSeg(
            "Inlet lip",
            x, xLipEnd,
            entranceInnerR, entranceInnerR,
            wall,
            null,
            "Constant inner capture radius (≥ chamber ID); outer shell includes wall thickness."));
        if (entranceInnerR > chamberInnerR + 1e-6)
            explanations.Add("Inlet mouth inner radius is widened to match or exceed swirl chamber ID (no throat ahead of chamber).");

        // --- Inlet flare (same meridian as audit “Inlet” second sub-span) ---
        segments.Add(MkSeg(
            "Inlet flare (inner radius to chamber ID)",
            xLipEnd, xFlareEnd,
            entranceInnerR, chamberInnerR,
            wall,
            flareHalfAngleDeg,
            "Linear inner meridian from capture radius to chamber bore ID (when widened, entrance R ≥ chamber R — flare may be zero ΔR)."));

        // --- Swirl chamber voxel (main segment; guard unioned in builder when InjectorUpstreamGuardLengthMm > 0) ---
        double xSwirlStart = path.XSwirlStart;
        double xAfterSwirl = path.XAfterSwirl;
        double anchorMm = run?.SwirlChamberLengthDownstreamAnchorMm ?? 0.0;
        string swirlAssemblyNote =
            $"Physical L_req=L_built={d.SwirlChamberLengthMm:F3} mm (main bore). Junction X={sp.InletChamberJunctionXMm:F3} mm. "
            + (anchorMm > 0.0
                ? $"Downstream face at junction+anchor ({anchorMm:F2} mm)."
                : "Downstream face = upstream + physical L.")
            + (sp.UsesExplicitUpstreamRetentionSection
                ? $" Optional upstream guard {sp.UpstreamRetentionLengthMm:F2} mm (separate segment)."
                : "");
        segments.Add(MkSeg(
            "Swirl chamber",
            xSwirlStart, xAfterSwirl,
            chamberInnerR, chamberInnerR,
            wall,
            null,
            swirlAssemblyNote));

        double xInjectorPlane = path.XInjectorPlane;
        segments.Add(MkSeg(
            "Injector ring reference position (outer wall station)",
            xInjectorPlane, xInjectorPlane,
            chamberInnerR, chamberInnerR,
            wall,
            null,
            "Reference beams originate at outer casing R; not flow passages."));

        segments.Add(MkSeg(
            "Injector axial position plane (ratio-based)",
            xInjectorPlane, xInjectorPlane,
            chamberInnerR, chamberInnerR,
            wall,
            null,
            $"InjectorX = chamberStart + clamp(ratio)×L_phys: {sp.RequestedInjectorAxialRatio:F4} → {sp.ClampedInjectorAxialRatio:F4}, L={chamberLen:F3} mm."));

        if (sp.PlacementHealth == SwirlChamberPlacementHealth.Warn)
            warnings.Add(
                $"SWIRL PLACEMENT WARN: chamber upstream overshoot {sp.ChamberUpstreamOvershootMm:F3} mm (warn threshold {run?.SwirlChamberUpstreamOvershootWarnMm ?? 0.05:F2} mm).");
        if (sp.PlacementHealth == SwirlChamberPlacementHealth.Fail)
            warnings.Add(
                $"SWIRL PLACEMENT FAIL: overshoot {sp.ChamberUpstreamOvershootMm:F3} mm exceeds hard reject {run?.SwirlChamberUpstreamOvershootHardRejectMm ?? 2.0:F2} mm.");

        // --- Expander ---
        double xExpStart = path.XExpanderStart;
        double expLen = path.XAfterExpander - path.XExpanderStart;
        double expanderEndInnerR = path.ExpanderEndInnerRadiusMm;
        double xAfterExpander = path.XAfterExpander;
        double impliedExitD = 2.0 * expanderEndInnerR;
        segments.Add(MkSeg(
            "Expander (conical diffuser)",
            xExpStart, xAfterExpander,
            chamberInnerR, expanderEndInnerR,
            wall,
            d.ExpanderHalfAngleDeg,
            path.UsesPostStatorExitTaper
                ? "Nominal template length + angle to recovery R (post-stator taper mode)."
                : "Axial length solved so outlet inner R matches declared exit inner R (constant-area recovery); angle from design."));

        // --- Stator ---
        double xStatorStart = path.XStatorStart;
        StatorGeometryDebugInfo statorDiag = ComputeStatorDiagnostics(d, path);
        double xAfterStator = path.XAfterStator;
        segments.Add(MkSeg(
            "Stator section (casing + hub + reference vanes)",
            xStatorStart, xAfterStator,
            expanderEndInnerR, expanderEndInnerR,
            wall,
            null,
            "Annulus inner casing radius is held constant (matches expander exit R); hub + blades are solid add-ons."));

        explanations.Add(
            "Downstream gas path uses one recovery annulus inner radius: expander outlet = stator casing ID = exit section start (from DownstreamGeometryResolver).");
        explanations.Add(
            "Exit uses AddBeam(..., roundCap:false): flat annular ends; inner beam subtracted so the bore stays open.");

        // --- Exit (length + stations from GeometryAssemblyPath / ExitBuilder.ComputeExitSectionLengthMm) ---
        double xExitStart = path.XExitStart;
        double rExit0 = path.ExitInnerRadiusStartMm;
        double rExit1 = path.ExitInnerRadiusEndMm;
        double exitLen = path.ExitSectionLengthMm;
        double xAfterExit = path.XAfterExit;
        double exitSlopeHalfAngleDeg = RadiansToDeg(Math.Atan(Math.Abs(rExit1 - rExit0) / Math.Max(exitLen, 1e-6)));

        if (!path.UsesPostStatorExitTaper)
            explanations.Add(
                "Constant-area recovery exit (default): exit inner R start = end = recovery annulus R — no post-stator contraction in the bore.");
        else
        {
            explanations.Add(
                "Post-stator taper enabled: exit frustum runs from geometric expander outlet R to declared ExitDiameterMm/2 (explicit taper, not a hidden cleanup).");
            if (rExit1 < rExit0 - 0.25)
                explanations.Add("Exit section contracts in the bore (intentional when taper mode is on).");
            if (rExit1 > rExit0 + 0.25)
                explanations.Add("Exit section expands in the bore (intentional when taper mode is on).");
        }

        double nominalConeD = 2.0 * tgt.NominalConeOutletInnerRadiusMm;
        if (!path.UsesPostStatorExitTaper
            && tgt.NominalConeVersusDeclaredExitInnerRadiusMm > GeometryConsistencyTolerances.NominalVersusAuthoritativeNarrativeMinDeltaMm)
        {
            explanations.Add(
                $"Reference nominal expander (design L={tgt.NominalExpanderLengthMm:F2} mm × half-angle) would reach inner Ø {nominalConeD:F2} mm; authoritative built path uses inner Ø {impliedExitD:F2} mm at expander outlet (matches declared exit) with effective axial L={expLen:F3} mm — see GeometryAssemblyPath / DownstreamGeometryResolver.");
        }

        segments.Add(MkSeg(
            "Exit section",
            xExitStart, xAfterExit,
            rExit0, rExit1,
            wall,
            rExit0 != rExit1 ? exitSlopeHalfAngleDeg : null,
            path.UsesPostStatorExitTaper
                ? "Explicit post-stator taper frustum (EnablePostStatorExitTaper)."
                : "Constant-area annulus (inner R constant); minimum-length lip from ComputeExitSectionLengthMm."));

        segments.Add(MkSeg(
            "Final outlet lip / exit plane",
            xAfterExit, xAfterExit,
            rExit1, rExit1,
            wall,
            null,
            "End of built duct; inner radius = exit section end inner R."));

        if (2.0 * rExit1 > chamberD * 1.2 && rExit1 > rExit0 + 0.5)
            warnings.Add(
                $"Final exit inner Ø ({2 * rExit1:F2} mm) is much larger than swirl chamber Ø ({chamberD:F2} mm) — most post-chamber 'opening' is expander + exit, not stator row expansion.");

        if (Math.Abs(rExit1 - rExit0) > 1.0 && exitLen < 0.18 * Math.Max(2.0 * rExit0, 2.0 * rExit1))
            warnings.Add(
                $"Exit section length ({exitLen:F2} mm) is still short vs bore (L/D≈{exitLen / Math.Max(2.0 * Math.Max(rExit0, rExit1), 1e-6):F3}) — check viewer.");

        if (tgt.ConeCannotReachDeclaredExit)
            warnings.Add(
                "DOWNSTREAM: declared exit inner R could not be reached with a diverging cone from chamber ID — built recovery uses fallback cone (see unified penalties / HardRejectInfeasibleDownstreamCone).");

        // --- Mismatches ---
        var mismatches = new List<TransitionMismatchDebugInfo>();
        void AddM(string from, string to, double ru, double rd, string noteExtra = "")
        {
            double dr = rd - ru;
            double dd = 2.0 * dr;
            bool ok = Math.Abs(dr) <= RadiusMatchToleranceMm;
            string note = noteExtra;
            if (!ok)
                note = (string.IsNullOrEmpty(note) ? "" : note + " ") + $"|ΔR|={Math.Abs(dr):F3} mm exceeds tolerance {RadiusMatchToleranceMm:F2} mm.";
            mismatches.Add(new TransitionMismatchDebugInfo(from, to, ru, rd, dr, dd, ok, note.Trim()));
            if (!ok)
                warnings.Add($"GEOMETRY MISMATCH: {from} → {to}: ΔR={dr:F3} mm.");
        }

        AddM(
            "Swirl chamber end (inner)",
            "Expander start (inner @ voxel start)",
            chamberInnerR,
            chamberInnerR,
            "Same chamber ID; expander voxel starts with overlap upstream.");
        AddM(
            "Expander end (inner)",
            "Stator start (casing inner)",
            expanderEndInnerR,
            expanderEndInnerR,
            "Stator upstream inner R is forced to expander exit.");
        AddM(
            "Stator end (casing inner)",
            "Exit start (inner)",
            expanderEndInnerR,
            rExit0,
            "Single recovery R: exit start equals stator casing inner R.");
        double dExitR = rExit1 - rExit0;
        double tolR = GeometryContinuityValidator.DownstreamRadiusContinuityToleranceMm;
        bool constantExitOk = !path.UsesPostStatorExitTaper && Math.Abs(dExitR) <= tolR;
        mismatches.Add(new TransitionMismatchDebugInfo(
            "Exit start (inner)",
            "Final outlet (inner, exit frustum end)",
            rExit0,
            rExit1,
            dExitR,
            2.0 * dExitR,
            WithinTolerance: path.UsesPostStatorExitTaper || constantExitOk,
            Note: path.UsesPostStatorExitTaper
                ? "Explicit post-stator taper (EnablePostStatorExitTaper)."
                : Math.Abs(dExitR) <= tolR
                    ? "Constant-area exit lip (inner R start = end)."
                    : "Unexpected radial change in default constant-area exit mode."));

        explanations.Add(
            $"Assembly uses {overlap:F2} mm axial overlap between consecutive voxel segments for watertight BoolAdd — not an extra flow passage; diameters are continuity-checked at logical interfaces.");

        // Stator sits after expander: upstream flow path is diverging; casing ID here is constant (see explanations).
        warnings.Add(
            "Stator is placed immediately downstream of a diverging expander (diffuser tail). Casing inner radius is held constant in StatorSectionBuilder — not a diverging stator wall.");

        if (statorDiag.VaneSpanMm < Math.Max(3.0, 0.06 * chamberD))
            warnings.Add($"Vane span (hub-to-casing annulus ≈ {statorDiag.VaneSpanMm:F2} mm) may be short for meaningful swirl interception — check hub OD and casing R.");

        // Stator mount interpretation tags
        var mountTags = new List<string>();
        string primaryMount;
        if (path.UsesPostStatorExitTaper)
        {
            mountTags.Add("post-stator exit taper enabled (explicit bore change in exit section)");
            primaryMount = "diffuser-mounted + explicit exit taper";
        }
        else
        {
            mountTags.Add("constant-area recovery section (casing inner R constant through expander/stator/exit start)");
            primaryMount = "diffuser-mounted + constant-area annulus";
        }

        mountTags.Add("diffuser-mounted recovery section (stator after conical expander)");
        if (Math.Abs(rExit1 - rExit0) > 0.25)
            mountTags.Add("exit follows stator — bore change in exit section");

        statorDiag = statorDiag with
        {
            MountInterpretationPrimary = primaryMount,
            MountInterpretationTags = mountTags
        };

        NozzleGeometryAssemblyAudit assemblyAudit = NozzleGeometryAssemblyAuditBuilder.Build(d, builtGeometry);
        foreach (string cw in assemblyAudit.ConsistencyWarnings)
            warnings.Add(cw);

        double tolDown = GeometryContinuityValidator.DownstreamRadiusContinuityToleranceMm;
        double maxRadErr = Math.Max(
            Math.Abs(path.ExpanderEndInnerRadiusMm - path.RecoveryAnnulusInnerRadiusMm),
            Math.Abs(path.ExitInnerRadiusStartMm - path.RecoveryAnnulusInnerRadiusMm));
        if (!path.UsesPostStatorExitTaper)
            maxRadErr = Math.Max(maxRadErr, Math.Abs(path.ExitInnerRadiusEndMm - path.ExitInnerRadiusStartMm));

        string downLabel = maxRadErr <= tolDown ? "PASS" : "FAIL";
        if (tgt.ConeCannotReachDeclaredExit && maxRadErr <= tolDown)
            downLabel = "WARN";

        string modeLabel = path.UsesPostStatorExitTaper ? "Mode B: post-stator taper" : "Mode A: constant-area recovery exit";

        string placeLabel = sp.PlacementHealth switch
        {
            SwirlChamberPlacementHealth.Pass => "PASS",
            SwirlChamberPlacementHealth.Warn => "WARN",
            SwirlChamberPlacementHealth.Fail => "FAIL",
            _ => "—"
        };

        return new NozzleGeometryDebugReport
        {
            AssemblyOverlapMm = overlap,
            TotalBuiltLengthMm = xAfterExit,
            NominalChamberInletPlaneXMm = xAfterInlet,
            SwirlVoxelStartXMm = xSwirlStart,
            SwirlChamberEndXMm = xAfterSwirl,
            SwirlChamberPhysicalLengthRequestedMm = sp.PhysicalChamberLengthRequestedMm,
            SwirlChamberPhysicalLengthBuiltMm = sp.PhysicalChamberLengthBuiltMm,
            InjectorUpstreamGuardLengthMm = sp.UpstreamRetentionLengthMm,
            RequestedInjectorAxialRatio = sp.RequestedInjectorAxialRatio,
            ClampedInjectorAxialRatio = sp.ClampedInjectorAxialRatio,
            InjectorDistanceFromChamberUpstreamFaceMm = sp.InjectorDistanceFromChamberUpstreamFaceMm,
            InjectorDistanceFromChamberDownstreamFaceMm = sp.InjectorDistanceFromChamberDownstreamFaceMm,
            ChamberUpstreamOvershootMm = sp.ChamberUpstreamOvershootMm,
            SwirlChamberPlacementStatusLabel = placeLabel,
            InjectorReferencePlaneXMm = xInjectorPlane,
            ImpliedExpanderExitDiameterMm = impliedExitD,
            RequestedExitDiameterMm = d.ExitDiameterMm,
            SolvedDownstreamTargetInnerDiameterMm = 2.0 * path.RecoveryAnnulusInnerRadiusMm,
            ExpanderBuiltOutletInnerDiameterMm = 2.0 * expanderEndInnerR,
            StatorCasingInnerDiameterMm = 2.0 * expanderEndInnerR,
            ExitStartInnerDiameterMm = 2.0 * rExit0,
            ExitEndInnerDiameterMm = 2.0 * rExit1,
            DownstreamContinuityMaxRadialErrorMm = maxRadErr,
            DownstreamContinuityLabel = downLabel,
            DownstreamExitModeLabel = modeLabel,
            Segments = segments,
            Mismatches = mismatches,
            Stator = statorDiag,
            BuilderExplanations = explanations,
            Warnings = warnings,
            AssemblyAudit = assemblyAudit
        };
    }

    private static StatorGeometryDebugInfo ComputeStatorDiagnostics(NozzleDesignInputs d, GeometryAssemblyPath path)
    {
        double xStart = path.XStatorStart;
        double innerR = Math.Max(0.5, path.ExpanderEndInnerRadiusMm);
        double xEnd = path.XAfterStator;
        double length = xEnd - xStart;
        double wall = path.WallMm;

        float innerRf = (float)innerR;
        double rOuter = innerR + wall;

        float hubDmm = (float)(d.StatorHubDiameterMm > 0.5 ? d.StatorHubDiameterMm : 0.28 * d.SwirlChamberDiameterMm);
        float rHub = 0.5f * hubDmm;
        float maxHubR = innerRf * 0.82f - 0.8f;
        rHub = Math.Clamp(rHub, 3f, Math.Max(maxHubR, 4f));

        float bulletLen = Math.Clamp(0.42f * (float)d.SwirlChamberDiameterMm, 5f, 11f);
        float tipR = Math.Max(0.85f, 0.22f * rHub);

        int vaneCount = Math.Max(1, d.StatorVaneCount);
        float vaneAngleRad = (float)(d.StatorVaneAngleDeg * Math.PI / 180.0);
        float dPhi = (2f * MathF.PI) / vaneCount;
        float marginX = Math.Clamp(0.06f * (float)length, 0.8f, 2.2f);
        float vaneStartX = (float)xStart + marginX;
        float vaneEndX = (float)xStart + (float)length - marginX;

        double span = innerR - rHub;
        float chordMm = (float)(d.StatorBladeChordMm > 0.5 ? d.StatorBladeChordMm : Math.Max(3.5, 0.14 * span));
        float vaneR = Math.Clamp(0.20f * chordMm, 0.48f, 3.2f);
        float rStart = rHub + vaneR + 0.55f;
        float rEnd = innerRf - vaneR - 0.9f;
        if (rEnd <= rStart + 0.5f)
            rEnd = Math.Min(innerRf - 1f, rStart + 1.5f);

        string casingChange = "Constant (dR/dx = 0 for annulus inner wall)";
        string passage = rEnd > rStart + 0.2
            ? "Vane reference beams: root R < tip R at matched axial extent (slight diverging blade path in builder)"
            : "Vane reference beams: approximately constant mean radius along skewed beam";

        return new StatorGeometryDebugInfo(
            StatorStartXMm: xStart,
            StatorEndXMm: xEnd,
            StatorAxialLengthMm: length,
            CasingInnerRadiusStartMm: innerR,
            CasingInnerRadiusEndMm: innerR,
            CasingOuterRadiusStartMm: rOuter,
            CasingOuterRadiusEndMm: rOuter,
            VaneCount: vaneCount,
            VaneAngleDeg: d.StatorVaneAngleDeg,
            VaneSpanMm: span,
            VaneChordMm: chordMm,
            VaneBeamRadiusMm: vaneR,
            VaneRootRadiusMm: rStart,
            VaneTipRadiusMm: rEnd,
            VaneAxialRootStartMm: vaneStartX,
            VaneAxialRootEndMm: vaneEndX,
            HubRadiusMm: rHub,
            HubNoseTipRadiusMm: tipR,
            HubNoseStartXMm: xStart - bulletLen,
            CasingAxialChange: casingChange,
            VanePassageClassification: passage,
            MountInterpretationPrimary: "",
            MountInterpretationTags: Array.Empty<string>());
    }

    private static GeometrySegmentDebugInfo MkSeg(
        string name,
        double x0,
        double x1,
        double r0,
        double r1,
        double wall,
        double? halfAngleDeg,
        string notes)
    {
        double d0 = 2.0 * r0;
        double d1 = 2.0 * r1;
        double len = x1 - x0;
        return new GeometrySegmentDebugInfo(
            Name: name,
            XStartMm: x0,
            XEndMm: x1,
            LengthMm: len,
            RadiusStartMm: r0,
            RadiusEndMm: r1,
            DiameterStartMm: d0,
            DiameterEndMm: d1,
            HalfAngleDeg: halfAngleDeg,
            WallThicknessMm: wall,
            InnerFlowAreaStartMm2: Math.PI * r0 * r0,
            InnerFlowAreaEndMm2: Math.PI * r1 * r1,
            OuterDiameterStartMm: d0 + 2.0 * wall,
            OuterDiameterEndMm: d1 + 2.0 * wall,
            Notes: notes);
    }

    private static double RadiansToDeg(double rad) => rad * (180.0 / Math.PI);

    /// <summary>Writes the full report to <paramref name="log"/> (e.g. <c>Library.Log</c> or <c>Console.WriteLine</c>).</summary>
    public static void WriteReport(NozzleGeometryDebugReport r, Action<string> log)
    {
        static string F(double v, string fmt = "F3") => v.ToString(fmt, CultureInfo.InvariantCulture);
        static string Fa(double? v) => v.HasValue ? F(v.Value, "F3") : "—";

        log("");
        log("╔══════════════════════════════════════════════════════════════════════════════╗");
        log("║  GEOMETRY DEBUG REPORT (built nozzle, inlet → exit) — voxels audit only      ║");
        log("╚══════════════════════════════════════════════════════════════════════════════╝");
        log($"AssemblyOverlapMm: {F(r.AssemblyOverlapMm)}  |  Total built length (last X) [mm]: {F(r.TotalBuiltLengthMm)}");
        log($"Nominal chamber inlet plane X [mm]: {F(r.NominalChamberInletPlaneXMm)}  |  Swirl voxel start X [mm]: {F(r.SwirlVoxelStartXMm)}");
        log("");
        log("── Swirl chamber placement (authoritative physical L, injector inside span) ──");
        log($"  Physical L requested [mm]: {F(r.SwirlChamberPhysicalLengthRequestedMm)}  |  built main segment L [mm]: {F(r.SwirlChamberPhysicalLengthBuiltMm)}");
        log($"  Chamber start X [mm]: {F(r.SwirlVoxelStartXMm)}  |  chamber end X [mm]: {F(r.SwirlChamberEndXMm)}");
        log($"  Upstream guard L [mm]: {F(r.InjectorUpstreamGuardLengthMm)}  (separate segment if > 0)");
        log($"  Injector ratio requested → clamped: {F(r.RequestedInjectorAxialRatio, "F4")} → {F(r.ClampedInjectorAxialRatio, "F4")}  (run min/max)");
        log($"  Injector plane X [mm]: {F(r.InjectorReferencePlaneXMm)}");
        log($"  Injector Δ from chamber upstream / downstream face [mm]: {F(r.InjectorDistanceFromChamberUpstreamFaceMm)} / {F(r.InjectorDistanceFromChamberDownstreamFaceMm)}");
        log($"  Chamber upstream overshoot past inlet junction [mm]: {F(r.ChamberUpstreamOvershootMm)}");
        log($"  Placement status: {r.SwirlChamberPlacementStatusLabel}");
        log("");
        log($"Implied expander exit Ø [mm]: {F(r.ImpliedExpanderExitDiameterMm, "F2")}  |  Requested ExitDiameterMm: {F(r.RequestedExitDiameterMm, "F2")}");
        log("");
        log("── Downstream diameter audit (single recovery annulus) ──");
        log($"  Mode: {r.DownstreamExitModeLabel}");
        log($"  Solved downstream target inner Ø [mm]: {F(r.SolvedDownstreamTargetInnerDiameterMm, "F3")}  (PASS/WARN/FAIL label: {r.DownstreamContinuityLabel})");
        log($"  Expander built outlet inner Ø [mm]:    {F(r.ExpanderBuiltOutletInnerDiameterMm, "F3")}");
        log($"  Stator casing inner Ø [mm]:            {F(r.StatorCasingInnerDiameterMm, "F3")}");
        log($"  Exit start inner Ø [mm]:                {F(r.ExitStartInnerDiameterMm, "F3")}");
        log($"  Exit end inner Ø [mm]:                  {F(r.ExitEndInnerDiameterMm, "F3")}");
        log($"  Downstream continuity max |ΔR| [mm]:  {F(r.DownstreamContinuityMaxRadialErrorMm, "F4")}  (tol {GeometryContinuityValidator.DownstreamRadiusContinuityToleranceMm:F3} mm)");
        log("");
        log("── Segment detail (build order) ──");
        foreach (GeometrySegmentDebugInfo s in r.Segments)
        {
            log($"[{s.Name}]");
            log($"  X_start_mm={F(s.XStartMm)}  X_end_mm={F(s.XEndMm)}  Length_mm={F(s.LengthMm)}");
            log($"  R_start_mm={F(s.RadiusStartMm)}  R_end_mm={F(s.RadiusEndMm)}  |  D_start_mm={F(s.DiameterStartMm)}  D_end_mm={F(s.DiameterEndMm)}");
            log($"  HalfAngle_deg={Fa(s.HalfAngleDeg)}  WallThickness_mm={F(s.WallThicknessMm)}");
            log($"  A_inner_start_mm2={F(s.InnerFlowAreaStartMm2, "F2")}  A_inner_end_mm2={F(s.InnerFlowAreaEndMm2, "F2")}");
            log($"  OD_wall_outer_mm: D_start={F(s.OuterDiameterStartMm)}  D_end={F(s.OuterDiameterEndMm)}");
            log($"  Notes: {s.Notes}");
            log("");
        }

        if (r.Stator != null)
        {
            StatorGeometryDebugInfo st = r.Stator;
            log("── Stator-specific geometry ──");
            log($"  Stator X_start_mm={F(st.StatorStartXMm)}  X_end_mm={F(st.StatorEndXMm)}  Axial_length_mm={F(st.StatorAxialLengthMm)}");
            log($"  Casing inner R start/end_mm={F(st.CasingInnerRadiusStartMm)} / {F(st.CasingInnerRadiusEndMm)}");
            log($"  Casing outer R start/end_mm={F(st.CasingOuterRadiusStartMm)} / {F(st.CasingOuterRadiusEndMm)}");
            log($"  Hub R_mm={F(st.HubRadiusMm)}  Nose tip R_mm={F(st.HubNoseTipRadiusMm)}  Nose starts X_mm={F(st.HubNoseStartXMm)}");
            log($"  Vane count={st.VaneCount}  Vane angle [deg]={F(st.VaneAngleDeg, "F2")}  Span_mm={F(st.VaneSpanMm, "F2")}");
            log($"  Vane chord_mm={F(st.VaneChordMm, "F2")}  Vane beam R_mm={F(st.VaneBeamRadiusMm, "F2")}");
            log($"  Vane root R_mm={F(st.VaneRootRadiusMm, "F2")}  Vane tip R_mm={F(st.VaneTipRadiusMm, "F2")}");
            log($"  Vane axial extent (root stations) X_mm: {F(st.VaneAxialRootStartMm)} → {F(st.VaneAxialRootEndMm)}");
            log($"  Casing axial change: {st.CasingAxialChange}");
            log($"  Vane passage (builder): {st.VanePassageClassification}");
            log($"  Mount interpretation (primary): {st.MountInterpretationPrimary}");
            foreach (string tag in st.MountInterpretationTags)
                log($"    • {tag}");
            if (st.VaneCount <= 16)
            {
                log("  Per-vane placement (same pattern every 360/N deg; beams skewed in builder — R/X nominal):");
                for (int i = 0; i < st.VaneCount; i++)
                {
                    double phiDeg = i * 360.0 / st.VaneCount;
                    log(
                        $"    Vane[{i}] φ={F(phiDeg, "F2")}°  axial_X_mm: {F(st.VaneAxialRootStartMm)}..{F(st.VaneAxialRootEndMm)}  R_root≈{F(st.VaneRootRadiusMm)}  R_tip≈{F(st.VaneTipRadiusMm)}");
                }
            }
            else
                log($"  ({st.VaneCount} vanes — per-vane table omitted; dφ = {360.0 / st.VaneCount:F3}°)");
            log("");
            log("  Stator role labels (debug):");
            log("    • constant-area recovery section — annulus inner radius is constant in StatorSectionBuilder.");
            log("    • diffuser-mounted recovery section — placed immediately after diverging expander.");
            log("    • transition-mounted — NOT used here (no axial change in casing ID within stator).");
            log("    • exit-mounted recovery — NOT primary (exit section follows stator when ExitDiameterMm is applied).");
            log("");
        }

        log("── Transition / continuity diagnostics ──");
        foreach (TransitionMismatchDebugInfo m in r.Mismatches)
        {
            log($"  {m.FromSegment} → {m.ToSegment}: R_up={F(m.UpstreamRadiusMm)} R_dn={F(m.DownstreamRadiusMm)} ΔR={F(m.DeltaRadiusMm)} ΔD={F(m.DeltaDiameterMm)} OK={m.WithinTolerance}");
            if (!string.IsNullOrWhiteSpace(m.Note))
                log($"    {m.Note}");
        }
        log("");

        log("── Builder explanations ──");
        foreach (string line in r.BuilderExplanations)
            log($"  • {line}");
        log("");

        log("── Geometry warnings ──");
        if (r.Warnings.Count == 0)
            log("  (none)");
        else
            foreach (string w in r.Warnings)
                log($"  ⚠ {w}");
        log("");

        log("── Summary table (inlet → exit) ──");
        log("Segment | X_start_mm | X_end_mm | Length_mm | D_start_mm | D_end_mm | R_start_mm | R_end_mm | HalfAngle_deg | Notes");
        log("--------+------------+----------+-----------+------------+----------+------------+----------+---------------+------");
        foreach (GeometrySegmentDebugInfo s in r.Segments)
        {
            string shortName = s.Name.Length > 28 ? s.Name[..25] + "..." : s.Name;
            string note = s.Notes.Replace('\r', ' ').Replace('\n', ' ');
            if (note.Length > 48)
                note = note[..45] + "...";
            log($"{shortName,-28}|{F(s.XStartMm),11}|{F(s.XEndMm),10}|{F(s.LengthMm),11}|{F(s.DiameterStartMm),12}|{F(s.DiameterEndMm),10}|{F(s.RadiusStartMm),12}|{F(s.RadiusEndMm),10}|{Fa(s.HalfAngleDeg),15}| {note}");
        }
        log("");

        if (r.AssemblyAudit != null)
            NozzleGeometryAssemblyAuditBuilder.WriteAssemblyAudit(r.AssemblyAudit, log);
    }
}
