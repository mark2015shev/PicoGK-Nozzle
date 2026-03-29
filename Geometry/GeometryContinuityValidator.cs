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
    public const double DownstreamRadiusContinuityToleranceMm = GeometryConsistencyTolerances.DiameterToleranceMm;

    public static GeometryContinuityReport Check(NozzleDesignInputs d, RunConfiguration? run = null)
    {
        var issues = new List<string>();
        var checks = new List<GeometryConsistencyCheckItem>();
        GeometryAssemblyPath p = GeometryAssemblyPath.Compute(d, run);

        void add(GeometryConsistencyCheckKind kind, GeometryConsistencySeverity sev, string s)
        {
            issues.Add(s);
            checks.Add(new GeometryConsistencyCheckItem(kind, false, s, sev));
        }

        foreach (GeometryPathBuildCheckItem pathChk in GeometryPathBuildConsistencyValidator.Validate(p, null))
        {
            if (pathChk.Passed)
                continue;
            if (pathChk.Severity == GeometryPathBuildCheckSeverity.Reject)
            {
                add(
                    GeometryConsistencyCheckKind.AxialStationOrdering,
                    GeometryConsistencySeverity.Reject,
                    "PATH ASSEMBLY: " + pathChk.Message);
            }
        }

        if (d.InletDiameterMm < MinPositiveDiameterMm)
            add(GeometryConsistencyCheckKind.DiameterPhysical, GeometryConsistencySeverity.Reject,
                $"GEOM: InletDiameterMm {d.InletDiameterMm:F2} mm — non-physical.");
        if (d.SwirlChamberDiameterMm < MinPositiveDiameterMm)
            add(GeometryConsistencyCheckKind.DiameterPhysical, GeometryConsistencySeverity.Reject,
                $"GEOM: SwirlChamberDiameterMm {d.SwirlChamberDiameterMm:F2} mm — non-physical.");
        if (d.ExitDiameterMm < MinPositiveDiameterMm)
            add(GeometryConsistencyCheckKind.DiameterPhysical, GeometryConsistencySeverity.Reject,
                $"GEOM: ExitDiameterMm {d.ExitDiameterMm:F2} mm — non-physical.");

        if (p.XAfterInlet <= p.XInletStart + 1e-6)
            add(GeometryConsistencyCheckKind.AxialExtent, GeometryConsistencySeverity.Reject,
                "GEOM: inlet segment has zero or negative axial extent.");
        if (p.XAfterSwirl <= p.XSwirlStart + 1e-6)
            add(GeometryConsistencyCheckKind.AxialExtent, GeometryConsistencySeverity.Reject,
                "GEOM: swirl chamber has zero or negative axial extent.");

        SwirlChamberPlacement sp = p.SwirlPlacement;
        if (sp.PlacementHealth == SwirlChamberPlacementHealth.Fail)
        {
            double hard = run?.SwirlChamberUpstreamOvershootHardRejectMm ?? 2.0;
            add(
                GeometryConsistencyCheckKind.SwirlChamberUpstreamPlacement,
                GeometryConsistencySeverity.Reject,
                $"GEOM SWIRL: upstream overshoot {sp.ChamberUpstreamOvershootMm:F3} mm exceeds hard reject {hard:F2} mm (inlet junction vs main chamber start).");
        }
        else if (sp.PlacementHealth == SwirlChamberPlacementHealth.Warn)
        {
            double w = run?.SwirlChamberUpstreamOvershootWarnMm ?? 0.05;
            checks.Add(new GeometryConsistencyCheckItem(
                GeometryConsistencyCheckKind.SwirlChamberUpstreamPlacement,
                false,
                $"GEOM SWIRL: upstream overshoot {sp.ChamberUpstreamOvershootMm:F3} mm exceeds warn {w:F2} mm.",
                GeometryConsistencySeverity.Warning));
        }

        if (p.XExpanderStart < p.XSwirlStart - 0.01)
            add(GeometryConsistencyCheckKind.AxialStationOrdering, GeometryConsistencySeverity.Reject,
                "GEOM: expander starts upstream of swirl chamber start (ordering).");
        if (p.XStatorStart < p.XExpanderStart - 0.01)
            add(GeometryConsistencyCheckKind.AxialStationOrdering, GeometryConsistencySeverity.Reject,
                "GEOM: stator starts upstream of expander start (ordering).");
        if (p.XExitStart < p.XStatorStart - 0.01)
            add(GeometryConsistencyCheckKind.AxialStationOrdering, GeometryConsistencySeverity.Reject,
                "GEOM: exit starts upstream of stator (ordering).");

        double rCh = p.ChamberInnerRadiusMm;
        double rRec = p.RecoveryAnnulusInnerRadiusMm;
        double rExpEnd = p.ExpanderEndInnerRadiusMm;
        if (rCh > 1e-6 && rExpEnd / rCh > MaxDiameterJumpRatio)
            add(GeometryConsistencyCheckKind.ExpanderChamberRadiusRatio, GeometryConsistencySeverity.Warning,
                $"GEOM: expander exit inner R / chamber R = {rExpEnd / rCh:F2} — abrupt expansion (check angle/length).");

        double entranceR = p.EntranceInnerRadiusMm;
        if (entranceR + 1e-6 < rCh)
            add(GeometryConsistencyCheckKind.InletLipVsChamber, GeometryConsistencySeverity.Warning,
                "GEOM: inlet entrance inner R smaller than chamber R — inward choke at lip (should be bell-mouth ≥ chamber ID).");

        if (d.StatorHubDiameterMm >= d.SwirlChamberDiameterMm * 0.98)
            add(GeometryConsistencyCheckKind.HubBlockage, GeometryConsistencySeverity.Warning,
                "GEOM: stator hub diameter nearly blocks chamber bore — check hub OD vs chamber ID.");

        double d1 = Math.Abs(rExpEnd - rRec);
        double d2 = Math.Abs(p.ExitInnerRadiusStartMm - rRec);
        if (d1 > DownstreamRadiusContinuityToleranceMm)
            add(GeometryConsistencyCheckKind.DownstreamAnnulusRadiusExpanderVsRecovery, GeometryConsistencySeverity.Warning,
                $"GEOM DOWNSTREAM: |R_expander_out − R_recovery| = {d1:F4} mm > tol {DownstreamRadiusContinuityToleranceMm:F3} mm.");
        if (d2 > DownstreamRadiusContinuityToleranceMm)
            add(GeometryConsistencyCheckKind.DownstreamAnnulusRadiusExitVsRecovery, GeometryConsistencySeverity.Warning,
                $"GEOM DOWNSTREAM: |R_exit_start − R_recovery| = {d2:F4} mm > tol {DownstreamRadiusContinuityToleranceMm:F3} mm.");

        if (!p.UsesPostStatorExitTaper)
        {
            double d3 = Math.Abs(p.ExitInnerRadiusEndMm - p.ExitInnerRadiusStartMm);
            if (d3 > DownstreamRadiusContinuityToleranceMm)
                add(GeometryConsistencyCheckKind.ExitConstantAreaMismatch, GeometryConsistencySeverity.Warning,
                    $"GEOM DOWNSTREAM: constant-area exit mode but |R_exit_end − R_exit_start| = {d3:F4} mm (enable taper if intentional).");
        }

        bool ok = issues.Count == 0;
        return new GeometryContinuityReport { IsAcceptable = ok, Issues = issues, Checks = checks };
    }
}
