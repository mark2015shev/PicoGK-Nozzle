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
    public const double ExpanderExitVsExitDiameterWarnMm = 1.5;

    public static NozzleGeometryDebugReport Build(NozzleDesignInputs d)
    {
        double overlap = NozzleGeometryBuilder.AssemblyOverlapMm;
        var segments = new List<GeometrySegmentDebugInfo>();
        var explanations = new List<string>();
        var warnings = new List<string>();

        double wall = Math.Max(d.WallThicknessMm, 0.0);
        double inletD = Math.Max(d.InletDiameterMm, 1.0);
        double chamberD = Math.Max(d.SwirlChamberDiameterMm, 1.0);
        double chamberLen = Math.Max(d.SwirlChamberLengthMm, 1.0);
        double refD = Math.Max(inletD, chamberD);
        double lipLen = Math.Max(4.0, 0.08 * refD);
        double flareLen = Math.Max(14.0, 0.30 * refD);
        double inletNominalR = 0.5 * inletD;
        double chamberInnerR = 0.5 * chamberD;
        double entranceInnerR = Math.Max(inletNominalR, chamberInnerR);

        double x = 0.0;
        double xLipEnd = x + lipLen;
        double xFlareEnd = xLipEnd + flareLen;
        double xAfterInlet = xFlareEnd;

        // --- Inlet lip ---
        double? flareHalfAngleDeg = entranceInnerR > chamberInnerR + 1e-9
            ? RadiansToDeg(Math.Atan((entranceInnerR - chamberInnerR) / Math.Max(flareLen, 1e-6)))
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

        // --- Inlet contraction (flare) ---
        segments.Add(MkSeg(
            "Inlet contraction / inlet cone",
            xLipEnd, xFlareEnd,
            entranceInnerR, chamberInnerR,
            wall,
            flareHalfAngleDeg,
            "Inner wall contracts toward swirl chamber ID; equivalent |ΔR|/L encoded as HalfAngle_deg when monotonic."));

        // --- Swirl chamber voxel ---
        double xSwirlStart = xAfterInlet - overlap;
        double xAfterSwirl = xSwirlStart + chamberLen;
        segments.Add(MkSeg(
            "Swirl chamber",
            xSwirlStart, xAfterSwirl,
            chamberInnerR, chamberInnerR,
            wall,
            null,
            $"Voxel start = inlet end − assembly overlap ({overlap:F2} mm) for watertight union."));

        double ratio = Math.Clamp(d.InjectorAxialPositionRatio, 0.0, 1.0);
        double xInjectorPlane = xAfterInlet + ratio * chamberLen;
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
            $"X = nominal chamber inlet plane (x={xAfterInlet:F3} mm) + ratio×L_ch = {ratio:F4}×{chamberLen:F2} mm."));

        if (Math.Abs(xInjectorPlane - xSwirlStart) < overlap + 0.01 || Math.Abs(xInjectorPlane - xAfterSwirl) < overlap + 0.01)
            warnings.Add("Injector reference plane lies near a swirl overlap boundary — viewer overlap can visually shift markers vs chamber bore.");

        // --- Expander ---
        double xExpStart = xAfterSwirl - overlap;
        double expLen = Math.Max(d.ExpanderLengthMm, 0.0);
        double halfRad = d.ExpanderHalfAngleDeg * (Math.PI / 180.0);
        double expanderEndInnerR = chamberInnerR + Math.Tan(halfRad) * expLen;
        double xAfterExpander = xExpStart + expLen;
        double impliedExitD = 2.0 * expanderEndInnerR;
        segments.Add(MkSeg(
            "Expander (conical diffuser)",
            xExpStart, xAfterExpander,
            chamberInnerR, expanderEndInnerR,
            wall,
            d.ExpanderHalfAngleDeg,
            "Inner radius follows ExpanderHalfAngleDeg from chamber ID; implied outlet Ø = 2·R_end (not ExitDiameterMm)."));

        // --- Stator ---
        double xStatorStart = xAfterExpander - overlap;
        StatorGeometryDebugInfo statorDiag = ComputeStatorDiagnostics(d, xStatorStart, expanderEndInnerR, wall, out double xAfterStator);
        segments.Add(MkSeg(
            "Stator section (casing + hub + reference vanes)",
            xStatorStart, xAfterStator,
            expanderEndInnerR, expanderEndInnerR,
            wall,
            null,
            "Annulus inner casing radius is held constant (matches expander exit R); hub + blades are solid add-ons."));

        explanations.Add("Stator annulus inner wall does not expand in current builder — any perceived opening in the viewer is usually overlap with the expander or the exit taper after the stator.");

        // --- Exit ---
        double xExitStart = xAfterStator - overlap;
        double targetExitR = 0.5 * Math.Max(d.ExitDiameterMm, 1.0);
        double rExit0 = Math.Max(0.5, expanderEndInnerR);
        double rExit1 = Math.Max(0.5, targetExitR);
        double exitLen = Math.Max(12.0, 0.12 * Math.Max(rExit0 * 2.0, rExit1 * 2.0));
        double xAfterExit = xExitStart + exitLen;
        double exitSlopeHalfAngleDeg = RadiansToDeg(Math.Atan(Math.Abs(rExit1 - rExit0) / Math.Max(exitLen, 1e-6)));

        if (Math.Abs(impliedExitD - d.ExitDiameterMm) > ExpanderExitVsExitDiameterWarnMm)
        {
            explanations.Add(
                "Expander end diameter does not match requested ExitDiameterMm; ExitBuilder linearly connects stator/exit interface inner R to ExitDiameterMm (this is the geometric bridge — no separate transition voxel).");
            warnings.Add(
                $"Expander-implied exit Ø ({impliedExitD:F2} mm) differs from requested ExitDiameterMm ({d.ExitDiameterMm:F2} mm) by {Math.Abs(impliedExitD - d.ExitDiameterMm):F2} mm — exit section carries the diameter change.");
        }
        else
            explanations.Add("Expander-implied outlet Ø is close to ExitDiameterMm; exit section may still add a short flare for minimum length rule.");

        if (rExit1 > rExit0 + 0.25)
            explanations.Add("Exit flare added after stator: inner wall opens from casing R to ExitDiameterMm over the exit section length.");
        if (rExit1 < rExit0 - 0.25)
            explanations.Add("Exit section contracts inner wall from stator casing R toward ExitDiameterMm.");

        segments.Add(MkSeg(
            "Exit section",
            xExitStart, xAfterExit,
            rExit0, rExit1,
            wall,
            rExit0 != rExit1 ? exitSlopeHalfAngleDeg : null,
            "Linear frustum (inner); length = max(12 mm, 0.12×max(Ø_start,Ø_end)) per ExitBuilder."));

        segments.Add(MkSeg(
            "Final outlet lip / exit plane",
            xAfterExit, xAfterExit,
            rExit1, rExit1,
            wall,
            null,
            "End of built duct; inner radius = 0.5×ExitDiameterMm."));

        if (2.0 * rExit1 > chamberD * 1.2 && rExit1 > rExit0 + 0.5)
            warnings.Add(
                $"Final exit inner Ø ({2 * rExit1:F2} mm) is much larger than swirl chamber Ø ({chamberD:F2} mm) — most post-chamber 'opening' is expander + exit taper, not stator row expansion.");

        if (Math.Abs(rExit1 - rExit0) > 1.0 && exitLen < 20.0)
            warnings.Add(
                $"Exit section length ({exitLen:F2} mm) is short relative to inner-radius change (ΔR={Math.Abs(rExit1 - rExit0):F2} mm) — steep exit wall slope.");

        if (Math.Abs(impliedExitD - d.ExitDiameterMm) > ExpanderExitVsExitDiameterWarnMm)
            warnings.Add(
                "No dedicated 'mismatch transition' voxel exists: diameter reconciliation is embedded in the exit frustum (plus assembly overlap for sealing).");

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
            "Exit upstream R uses max(0.5, stator downstream inner) — equals casing R in builder.");
        // Not an assembly seam: single ExitBuilder frustum from r0 → r1 by design.
        double dExitR = rExit1 - rExit0;
        mismatches.Add(new TransitionMismatchDebugInfo(
            "Exit start (inner)",
            "Final outlet (inner, exit frustum end)",
            rExit0,
            rExit1,
            dExitR,
            2.0 * dExitR,
            WithinTolerance: true,
            Note: Math.Abs(dExitR) < 1e-6
                ? "Exit inner wall is constant along exit section."
                : "Intentional ExitBuilder taper to ExitDiameterMm (not a missing bridge segment between voxels)."));

        explanations.Add(
            $"Assembly uses {overlap:F2} mm axial overlap between consecutive voxel segments for watertight BoolAdd — not an extra flow passage; diameters are continuity-checked at logical interfaces.");

        // Stator sits after expander: upstream flow path is diverging; casing ID here is constant (see explanations).
        warnings.Add(
            "Stator is placed immediately downstream of a diverging expander (diffuser tail). Casing inner radius is held constant in StatorSectionBuilder — not a diverging stator wall.");

        if (statorDiag.VaneSpanMm < Math.Max(3.0, 0.06 * chamberD))
            warnings.Add($"Vane span (hub-to-casing annulus ≈ {statorDiag.VaneSpanMm:F2} mm) may be short for meaningful swirl interception — check hub OD and casing R.");

        // Stator mount interpretation tags
        var mountTags = new List<string> { "constant-area recovery section (casing inner R constant)" };
        mountTags.Add("diffuser-mounted recovery section (stator after conical expander)");
        if (Math.Abs(rExit1 - rExit0) > 0.25)
            mountTags.Add("exit follows stator — not exit-mounted-only recovery");
        string primaryMount = "diffuser-mounted + constant-area annulus";

        statorDiag = statorDiag with
        {
            MountInterpretationPrimary = primaryMount,
            MountInterpretationTags = mountTags
        };

        return new NozzleGeometryDebugReport
        {
            AssemblyOverlapMm = overlap,
            TotalBuiltLengthMm = xAfterExit,
            NominalChamberInletPlaneXMm = xAfterInlet,
            SwirlVoxelStartXMm = xSwirlStart,
            InjectorReferencePlaneXMm = xInjectorPlane,
            ImpliedExpanderExitDiameterMm = impliedExitD,
            RequestedExitDiameterMm = d.ExitDiameterMm,
            Segments = segments,
            Mismatches = mismatches,
            Stator = statorDiag,
            BuilderExplanations = explanations,
            Warnings = warnings
        };
    }

    private static StatorGeometryDebugInfo ComputeStatorDiagnostics(
        NozzleDesignInputs d,
        double xStart,
        double upstreamInnerRadiusMm,
        double wall,
        out double xEnd)
    {
        double innerR = Math.Max(0.5, upstreamInnerRadiusMm);
        double lenAuto = Math.Max(10.0, 0.10 * Math.Max(innerR * 2.0, d.ExitDiameterMm));
        double length = d.StatorAxialLengthMm > 1.0 ? d.StatorAxialLengthMm : lenAuto;
        xEnd = xStart + length;

        float innerRf = (float)innerR;
        float wallF = (float)wall;
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
        log($"Injector reference plane X [mm]: {F(r.InjectorReferencePlaneXMm)}");
        log($"Implied expander exit Ø [mm]: {F(r.ImpliedExpanderExitDiameterMm, "F2")}  |  Requested ExitDiameterMm: {F(r.RequestedExitDiameterMm, "F2")}");
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
    }
}
