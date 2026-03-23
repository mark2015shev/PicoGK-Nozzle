using System;
using System.Collections.Generic;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

/// <summary>
/// Validates axial station ordering and diameter continuity for assembled nozzle segments (mm scale).
/// </summary>
public static class GeometryContinuityValidator
{
    private const double MaxDiameterJumpRatio = 2.8;
    private const double MinPositiveDiameterMm = 1.0;

    public static GeometryContinuityReport Check(NozzleDesignInputs d)
    {
        var issues = new List<string>();
        GeometryAssemblyPath p = GeometryAssemblyPath.Compute(d);

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
        double rExpEnd = p.ExpanderEndInnerRadiusMm;
        double rExitTgt = p.ExitInnerRadiusTargetMm;
        if (rCh > 1e-6 && rExpEnd / rCh > MaxDiameterJumpRatio)
            add($"GEOM: expander exit inner R / chamber R = {rExpEnd / rCh:F2} — abrupt expansion (check angle/length).");
        if (rExpEnd > 1e-6 && rExitTgt / rExpEnd > MaxDiameterJumpRatio)
            add($"GEOM: exit target R / expander end R = {rExitTgt / rExpEnd:F2} — possible disconnected jump.");

        double entranceR = p.EntranceInnerRadiusMm;
        if (entranceR + 1e-6 < rCh)
            add("GEOM: inlet entrance inner R smaller than chamber R — inward choke at lip (should be bell-mouth ≥ chamber ID).");

        if (d.StatorHubDiameterMm >= d.SwirlChamberDiameterMm * 0.98)
            add("GEOM: stator hub diameter nearly blocks chamber bore — check hub OD vs chamber ID.");

        bool ok = issues.Count == 0;
        return new GeometryContinuityReport { IsAcceptable = ok, Issues = issues };
    }
}
