using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

/// <summary>Build order and lattice semantics mirror <see cref="NozzleGeometryBuilder"/> and segment builders.</summary>
public static class NozzleGeometryAssemblyAuditBuilder
{
    public static NozzleGeometryAssemblyAudit Build(NozzleDesignInputs d, NozzleGeometryResult? builtGeometry = null)
    {
        GeometryAssemblyPath p = GeometryAssemblyPath.Compute(d);
        var solids = new List<BuiltGeometrySolidAuditEntry>();
        var consistency = new List<string>();

        double w = p.WallMm;
        double ch = p.ChamberInnerRadiusMm;
        double ent = p.EntranceInnerRadiusMm;

        // --- Group 1: Inlet (lip + flare, single BoolSubtract shell) ---
        double x0 = p.XInletStart;
        double xLip = p.XLipEnd;
        double xInEnd = p.XAfterInlet;
        solids.Add(new BuiltGeometrySolidAuditEntry(
            ViewerGroupId: 1,
            ViewerGroupName: "Inlet",
            ResultPropertyName: nameof(NozzleGeometryResult.Inlet),
            GeneratorType: nameof(InletBuilder),
            GeneratorMethod: nameof(InletBuilder.Build),
            SolidDescription: "Single annular shell: constant-bore lip + inner-contracting flare (outer wall follows).",
            XStartMm: x0,
            XEndMm: xInEnd,
            LengthMm: xInEnd - x0,
            RInnerStartMm: ent,
            RInnerEndMm: ch,
            ROuterStartMm: ent + w,
            ROuterEndMm: ch + w,
            HalfAngleDeg: ent > ch + 1e-9 ? R2D(Math.Atan((ent - ch) / Math.Max(p.FlareLengthMm, 1e-6))) : null,
            WallThicknessMm: w,
            SolidKind: "Composite: ConstantArea(lip) + Converging(inner flare)",
            PicoGkLatticeNotes: "Two outer/inner AddBeam pairs, BoolSubtract; AddBeam(..., roundCap:false) on all nozzle shells → flat normal caps at segment ends (overlap hides most seams).",
            ProfilePoints: new[]
            {
                new ProfileMeridianPoint(x0, ent, ent + w, "lip start"),
                new ProfileMeridianPoint(xLip, ent, ent + w, "lip end / flare start"),
                new ProfileMeridianPoint(xInEnd, ch, ch + w, "flare end / chamber ID")
            },
            Subcomponents: new[] { "Lip beams (constant R)", "Flare beams (linear R inner)" }));

        // --- Group 2: Swirl ---
        solids.Add(new BuiltGeometrySolidAuditEntry(
            2,
            "Swirl chamber",
            nameof(NozzleGeometryResult.SwirlChamber),
            nameof(SwirlChamberBuilder),
            nameof(SwirlChamberBuilder.Build),
            "Constant-area cylindrical annulus.",
            p.XSwirlStart,
            p.XAfterSwirl,
            p.XAfterSwirl - p.XSwirlStart,
            ch,
            ch,
            ch + w,
            ch + w,
            null,
            w,
            "ConstantArea",
            "AddBeam roundCap false; start X = inlet end − overlap.",
            new[]
            {
                new ProfileMeridianPoint(p.XSwirlStart, ch, ch + w, "start"),
                new ProfileMeridianPoint(p.XAfterSwirl, ch, ch + w, "end")
            },
            Array.Empty<string>()));

        // --- Group 3: Injector markers ---
        double injX = p.XInjectorPlane;
        double markerLen = Math.Max(6.0, 1.5 * Math.Max(d.InjectorWidthMm, d.InjectorHeightMm));
        solids.Add(new BuiltGeometrySolidAuditEntry(
            3,
            "Injector reference markers",
            nameof(NozzleGeometryResult.InjectorReferenceMarkers),
            nameof(InjectorReferenceMarkersBuilder),
            nameof(InjectorReferenceMarkersBuilder.Build),
            "Small beams from outer casing — not bore geometry; not unioned with flow path.",
            injX,
            injX + markerLen,
            markerLen,
            ch,
            ch,
            ch + w,
            ch + w,
            null,
            w,
            "ReferenceMarkers",
            "N separate AddBeam from (injX, outerR·radial) along yaw/pitch/roll direction; does not define inner R.",
            new[] { new ProfileMeridianPoint(injX, ch, ch + w, "station on axis (bore unchanged)") },
            new[] { $"InjectorCount={d.InjectorCount}", $"Approx beam length along dir ≈ {markerLen:F2} mm" }));

        // --- Group 4: Expander ---
        double rExp0 = ch;
        double rExp1 = p.ExpanderEndInnerRadiusMm;
        double halfAngle = d.ExpanderHalfAngleDeg;
        solids.Add(new BuiltGeometrySolidAuditEntry(
            4,
            "Expander",
            nameof(NozzleGeometryResult.Expander),
            nameof(ExpanderBuilder),
            nameof(ExpanderBuilder.Build),
            "Diverging conical annulus (inner half-angle = ExpanderHalfAngleDeg).",
            p.XExpanderStart,
            p.XAfterExpander,
            p.XAfterExpander - p.XExpanderStart,
            rExp0,
            rExp1,
            rExp0 + w,
            rExp1 + w,
            halfAngle,
            w,
            "Diverging",
            "Linear truncated cone; roundCap false.",
            new[]
            {
                new ProfileMeridianPoint(p.XExpanderStart, rExp0, rExp0 + w, "start"),
                new ProfileMeridianPoint(p.XAfterExpander, rExp1, rExp1 + w, "end")
            },
            Array.Empty<string>()));

        // --- Group 5: Stator ---
        double rSt = p.ExpanderEndInnerRadiusMm;
        float innerRf = (float)rSt;
        float wallF = (float)w;
        float hubDmm = (float)(d.StatorHubDiameterMm > 0.5 ? d.StatorHubDiameterMm : 0.28 * d.SwirlChamberDiameterMm);
        float rHub = 0.5f * hubDmm;
        float maxHubR = innerRf * 0.82f - 0.8f;
        rHub = Math.Clamp(rHub, 3f, Math.Max(maxHubR, 4f));
        float bulletLen = Math.Clamp(0.42f * (float)d.SwirlChamberDiameterMm, 5f, 11f);
        float tipR = Math.Max(0.85f, 0.22f * rHub);
        solids.Add(new BuiltGeometrySolidAuditEntry(
            5,
            "Stator section",
            nameof(NozzleGeometryResult.StatorSection),
            nameof(StatorSectionBuilder),
            nameof(StatorSectionBuilder.Build),
            "BoolAdd composite: annulus shell − inner void + hub nose + hub cylinder + N vane beams.",
            p.XStatorStart - bulletLen,
            p.XAfterStator,
            p.XAfterStator - (p.XStatorStart - bulletLen),
            rSt,
            rSt,
            rSt + w,
            rSt + w,
            null,
            w,
            "Composite: ConstantArea casing + Hub + Blades",
            "Annulus uses same inner R start/end; hub nose beam extends upstream of stator XStart; vanes skewed in YZ.",
            new[]
            {
                new ProfileMeridianPoint(p.XStatorStart - bulletLen, 0, tipR, "nose tip (solid hub axis)"),
                new ProfileMeridianPoint(p.XStatorStart, rHub, rSt + w, "stator plane: hub OD vs casing OD"),
                new ProfileMeridianPoint(p.XAfterStator, rHub, rSt + w, "stator end")
            },
            new[]
            {
                $"Annulus shell X [{p.XStatorStart:F3}, {p.XAfterStator:F3}] mm inner R={rSt:F3} constant",
                $"Hub nose AddBeam: ({p.XStatorStart - bulletLen:F3},0)→({p.XStatorStart:F3},0) radii {tipR:F2}→{rHub:F2} mm",
                $"Hub cylinder R={rHub:F2} mm",
                $"{d.StatorVaneCount} vane beam(s) (reference geometry)"
            }));

        // --- Group 6: Exit ---
        double rE0 = p.ExitInnerRadiusStartMm;
        double rE1 = p.ExitInnerRadiusEndMm;
        double exitLen = p.ExitSectionLengthMm;
        double? exitConeHalfAngleDeg = rE0 != rE1 ? R2D(Math.Atan(Math.Abs(rE1 - rE0) / Math.Max(exitLen, 1e-6))) : null;
        double lOverD = exitLen / Math.Max(2.0 * Math.Max(rE0, rE1), 1e-6);
        solids.Add(new BuiltGeometrySolidAuditEntry(
            6,
            "Exit",
            nameof(NozzleGeometryResult.Exit),
            nameof(ExitBuilder),
            nameof(ExitBuilder.Build),
            rE1 >= rE0 ? "Annular frustum opening to ExitDiameterMm." : "Annular frustum contracting to ExitDiameterMm.",
            p.XExitStart,
            p.XAfterExit,
            exitLen,
            rE0,
            rE1,
            rE0 + w,
            rE1 + w,
            exitConeHalfAngleDeg,
            w,
            rE1 > rE0 + 1e-6 ? "Diverging" : (rE1 < rE0 - 1e-6 ? "Converging" : "ConstantArea"),
            "ExitBuilder: outer/inner AddBeam with roundEndCaps=true (avoids dominant flat downstream disk). Length = ExitBuilder.ComputeExitSectionLengthMm.",
            new[]
            {
                new ProfileMeridianPoint(p.XExitStart, rE0, rE0 + w, "exit start"),
                new ProfileMeridianPoint(p.XAfterExit, rE1, rE1 + w, "exit end")
            },
            new[] { $"L/D_axial vs max bore ≈ {lOverD:F3} (length {exitLen:F2} mm)" }));

        if (lOverD < 0.14)
            consistency.Add($"AUDIT: Exit segment L/D={lOverD:F3} is still low — may read stubby in viewer (length={exitLen:F2} mm).");

        if (builtGeometry != null)
        {
            double lenReported = builtGeometry.TotalLengthMm;
            if (Math.Abs(lenReported - p.XAfterExit) > 0.05)
            {
                consistency.Add(
                    $"MISMATCH: NozzleGeometryResult.TotalLengthMm={lenReported:F3} vs GeometryAssemblyPath.XAfterExit={p.XAfterExit:F3} — design drift or builder changed without updating path.");
            }
        }

        return new NozzleGeometryAssemblyAudit
        {
            Path = p,
            Solids = solids,
            ConsistencyWarnings = consistency
        };
    }

    private static double R2D(double rad) => rad * (180.0 / Math.PI);

    public static void WriteAssemblyAudit(NozzleGeometryAssemblyAudit audit, Action<string> log)
    {
        static string F(double v, string fmt = "F3") => v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);
        static string Fn(double? v) => v.HasValue ? F(v.Value) : "—";

        log("");
        log("╔══════════════════════════════════════════════════════════════════════════════╗");
        log("║  ACTUAL-BUILT GEOMETRY AUDIT (generators ↔ viewer groups ↔ lattice profile) ║");
        log("╚══════════════════════════════════════════════════════════════════════════════╝");
        log("Build order (same as NozzleGeometryBuilder.Build): Inlet → Swirl → InjectorMarkers → Expander → Stator → Exit.");
        log("Viewer: one Voxels per group via AppPipeline.DisplayGeometryInViewer (no extra union of segments).");
        log("");

        log("── Viewer group manifest ──");
        foreach (NozzleViewerGroupCatalog.Entry e in NozzleViewerGroupCatalog.Ordered)
        {
            log($"  Group {e.GroupId}: {e.DisplayName,-28} ← geometry.{e.NozzleGeometryResultProperty,-30} color {e.ColorHex}");
        }
        log("");

        foreach (BuiltGeometrySolidAuditEntry s in audit.Solids)
        {
            log($"[Group {s.ViewerGroupId} — {s.ViewerGroupName}]  property: {s.ResultPropertyName}");
            log($"  Generator: {s.GeneratorType}.{s.GeneratorMethod}");
            log($"  {s.SolidDescription}");
            log($"  X_start={F(s.XStartMm)}  X_end={F(s.XEndMm)}  Length={F(s.LengthMm)}");
            log($"  R_in  start/end = {F(s.RInnerStartMm)} / {F(s.RInnerEndMm)}   R_out start/end = {F(s.ROuterStartMm)} / {F(s.ROuterEndMm)}");
            log($"  D_in  start/end = {F(2 * s.RInnerStartMm)} / {F(2 * s.RInnerEndMm)}   HalfAngle_deg={Fn(s.HalfAngleDeg)}  Wall={F(s.WallThicknessMm)}");
            log($"  Kind: {s.SolidKind}");
            log($"  PicoGK: {s.PicoGkLatticeNotes}");
            log("  Meridian profile (X_mm, R_inner, R_outer) [revolve = surface of revolution about X]:");
            foreach (ProfileMeridianPoint pt in s.ProfilePoints)
                log($"    ({F(pt.XMm)}, {F(pt.RInnerMm)}, {F(pt.ROuterMm)})  {pt.Label}");
            if (s.Subcomponents.Count > 0)
            {
                log("  Subcomponents:");
                foreach (string sc in s.Subcomponents)
                    log($"    • {sc}");
            }
            log("");
        }

        log("── Compact actual-built table ──");
        log("Segment          | Generator              | X_start | X_end   | Len     | R_in0  | R_in1  | R_out0 | R_out1 | Half° | Type        | Group | Notes");
        log("-----------------+------------------------+---------+---------+---------+--------+--------+--------+--------+-------+-------------+-------+------");
        foreach (BuiltGeometrySolidAuditEntry s in audit.Solids)
        {
            string seg = s.ViewerGroupName.Length > 15 ? s.ViewerGroupName[..12] + "..." : s.ViewerGroupName;
            string gen = ($"{s.GeneratorType}.{s.GeneratorMethod}").Length > 22 ? ($"{s.GeneratorType}.{s.GeneratorMethod}")[..19] + ".." : $"{s.GeneratorType}.{s.GeneratorMethod}";
            log($"{seg,-17}|{gen,-24}|{F(s.XStartMm),9}|{F(s.XEndMm),9}|{F(s.LengthMm),9}|{F(s.RInnerStartMm),8}|{F(s.RInnerEndMm),8}|{F(s.ROuterStartMm),8}|{F(s.ROuterEndMm),8}|{Fn(s.HalfAngleDeg),7}|{s.SolidKind,-13}|{s.ViewerGroupId,7}| {s.ResultPropertyName}");
        }
        log("");

        if (audit.ConsistencyWarnings.Count > 0)
        {
            log("── Consistency / cross-checks ──");
            foreach (string c in audit.ConsistencyWarnings)
                log($"  ⚠ {c}");
            log("");
        }
    }
}
