using System;
using System.Collections.Generic;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

/// <summary>
/// Validates axial station ordering and diameter continuity for assembled nozzle segments (mm scale).
/// Downstream gas-path radii are checked against <see cref="DownstreamGeometryResolver"/> (same as voxel build).
/// </summary>
public static class GeometryContinuityValidator
{
    private const double MaxDiameterJumpRatio = 2.8;
    private const double MinPositiveDiameterMm = 1.0;
    /// <summary>Hard check: expander outlet, stator casing ID, and exit start must match within this [mm].</summary>
    public const double DownstreamRadiusContinuityToleranceMm = 0.051;

    public static GeometryContinuityReport Check(NozzleDesignInputs d, RunConfiguration? run = null)
    {
        var issues = new List<string>();
        GeometryAssemblyPath p = GeometryAssemblyPath.Compute(d, run);

        void add(string s) => issues.Add(s);

        if (d.InletDiameterMm < MinPositiveDiameterMm)
            add($"GEOM: InletDiameterMm {d.InletDiameterMm:F2} mm — non-physical.");
        if (d.SwirlChamberDiameterMm < MinPositiveDiameterMm)
            add($"GEOM: SwirlChamberDiameterMm {d.SwirlChamberDiameterMm:F2} mm — non-physical.");
        if (d.ExitDiameterMm < MinPositiveDiameterMm)
            add($"GEOM: ExitDiameterMm {d.ExitDiameterMm:F2} mm — non-physical.");

        if (p.XAfterInlet <= p.XInletStart + 1e-6)
            add("GEOM: inlet segment has zero or negative axial extent.");
        if (p.XAfterSwirl <= p.XSwirlStart + 1e-6)
            add("GEOM: swirl chamber has zero or negative axial extent.");

        if (p.XExpanderStart < p.XSwirlStart - 0.01)
            add("GEOM: expander starts upstream of swirl chamber start (ordering).");
        if (p.XStatorStart < p.XExpanderStart - 0.01)
            add("GEOM: stator starts upstream of expander start (ordering).");
        if (p.XExitStart < p.XStatorStart - 0.01)
            add("GEOM: exit starts upstream of stator (ordering).");

        double rCh = p.ChamberInnerRadiusMm;
        double rRec = p.RecoveryAnnulusInnerRadiusMm;
        double rExpEnd = p.ExpanderEndInnerRadiusMm;
        if (rCh > 1e-6 && rExpEnd / rCh > MaxDiameterJumpRatio)
            add($"GEOM: expander exit inner R / chamber R = {rExpEnd / rCh:F2} — abrupt expansion (check angle/length).");

        double entranceR = p.EntranceInnerRadiusMm;
        if (entranceR + 1e-6 < rCh)
            add("GEOM: inlet entrance inner R smaller than chamber R — inward choke at lip (should be bell-mouth ≥ chamber ID).");

        if (d.StatorHubDiameterMm >= d.SwirlChamberDiameterMm * 0.98)
            add("GEOM: stator hub diameter nearly blocks chamber bore — check hub OD vs chamber ID.");

        // --- Single downstream annulus: expander outlet = stator ID = exit section start ---
        double d1 = Math.Abs(rExpEnd - rRec);
        double d2 = Math.Abs(p.ExitInnerRadiusStartMm - rRec);
        if (d1 > DownstreamRadiusContinuityToleranceMm)
            add(
                $"GEOM DOWNSTREAM: |R_expander_out − R_recovery| = {d1:F4} mm > tol {DownstreamRadiusContinuityToleranceMm:F3} mm.");
        if (d2 > DownstreamRadiusContinuityToleranceMm)
            add(
                $"GEOM DOWNSTREAM: |R_exit_start − R_recovery| = {d2:F4} mm > tol {DownstreamRadiusContinuityToleranceMm:F3} mm.");

        if (!p.UsesPostStatorExitTaper)
        {
            double d3 = Math.Abs(p.ExitInnerRadiusEndMm - p.ExitInnerRadiusStartMm);
            if (d3 > DownstreamRadiusContinuityToleranceMm)
                add(
                    $"GEOM DOWNSTREAM: constant-area exit mode but |R_exit_end − R_exit_start| = {d3:F4} mm (enable taper if intentional).");
        }

        bool ok = issues.Count == 0;
        return new GeometryContinuityReport { IsAcceptable = ok, Issues = issues };
    }
}
