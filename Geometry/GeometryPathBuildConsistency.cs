using System;
using System.Collections.Generic;

namespace PicoGK_Run.Geometry;

public enum GeometryPathBuildCheckKind
{
    JunctionExpanderAfterSwirl,
    JunctionStatorAfterExpander,
    JunctionExitAfterStator,
    ExpanderAxialLengthClosed,
    StatorAxialLengthClosed,
    ExitAxialLengthClosed,
    ExpanderOutletMatchesRecoveryRadius,
    TotalLengthMatchesPathEnd
}

public enum GeometryPathBuildCheckSeverity
{
    Info,
    Warning,
    Reject
}

public sealed record GeometryPathBuildCheckItem(
    GeometryPathBuildCheckKind Kind,
    bool Passed,
    string Message,
    GeometryPathBuildCheckSeverity Severity);

/// <summary>
/// Path internal closure + optional agreement with <see cref="NozzleGeometryResult.TotalLengthMm"/>.
/// </summary>
public static class GeometryPathBuildConsistencyValidator
{
    public static IReadOnlyList<GeometryPathBuildCheckItem> Validate(
        GeometryAssemblyPath path,
        NozzleGeometryResult? built)
    {
        var list = new List<GeometryPathBuildCheckItem>(12);
        double o = path.OverlapMm;
        double tol = GeometryConsistencyTolerances.AxialPositionToleranceMm;

        list.Add(Mk(
            GeometryPathBuildCheckKind.JunctionExpanderAfterSwirl,
            Math.Abs(path.XExpanderStart - (path.XAfterSwirl - o)) <= tol,
            $"XExpanderStart={path.XExpanderStart:F4} vs XAfterSwirl−overlap={path.XAfterSwirl - o:F4} mm.",
            GeometryPathBuildCheckSeverity.Reject));

        list.Add(Mk(
            GeometryPathBuildCheckKind.JunctionStatorAfterExpander,
            Math.Abs(path.XStatorStart - (path.XAfterExpander - o)) <= tol,
            $"XStatorStart={path.XStatorStart:F4} vs XAfterExpander−overlap={path.XAfterExpander - o:F4} mm.",
            GeometryPathBuildCheckSeverity.Reject));

        list.Add(Mk(
            GeometryPathBuildCheckKind.JunctionExitAfterStator,
            Math.Abs(path.XExitStart - (path.XAfterStator - o)) <= tol,
            $"XExitStart={path.XExitStart:F4} vs XAfterStator−overlap={path.XAfterStator - o:F4} mm.",
            GeometryPathBuildCheckSeverity.Reject));

        double expDelta = path.XAfterExpander - path.XExpanderStart;
        list.Add(Mk(
            GeometryPathBuildCheckKind.ExpanderAxialLengthClosed,
            Math.Abs(expDelta - path.ExpanderAxialLengthMm) <= GeometryConsistencyTolerances.LengthToleranceMm,
            $"Expander ΔX={expDelta:F4} vs ExpanderAxialLengthMm={path.ExpanderAxialLengthMm:F4} mm.",
            GeometryPathBuildCheckSeverity.Reject));

        double stDelta = path.XAfterStator - path.XStatorStart;
        list.Add(Mk(
            GeometryPathBuildCheckKind.StatorAxialLengthClosed,
            Math.Abs(stDelta - path.StatorAxialLengthMm) <= GeometryConsistencyTolerances.LengthToleranceMm,
            $"Stator ΔX={stDelta:F4} vs StatorAxialLengthMm={path.StatorAxialLengthMm:F4} mm.",
            GeometryPathBuildCheckSeverity.Reject));

        double exSeg = path.XAfterExit - path.XExitStart;
        list.Add(Mk(
            GeometryPathBuildCheckKind.ExitAxialLengthClosed,
            Math.Abs(exSeg - path.ExitSectionLengthMm) <= GeometryConsistencyTolerances.LengthToleranceMm,
            $"(XAfterExit−XExitStart)={exSeg:F4} vs ExitSectionLengthMm={path.ExitSectionLengthMm:F4} mm.",
            GeometryPathBuildCheckSeverity.Reject));

        double rRec = path.RecoveryAnnulusInnerRadiusMm;
        double rExpEnd = path.ExpanderEndInnerRadiusMm;
        list.Add(Mk(
            GeometryPathBuildCheckKind.ExpanderOutletMatchesRecoveryRadius,
            Math.Abs(rExpEnd - rRec) <= GeometryConsistencyTolerances.DiameterToleranceMm,
            $"Expander outlet R={rExpEnd:F4} vs recovery R={rRec:F4} mm.",
            GeometryPathBuildCheckSeverity.Reject));

        if (built != null)
        {
            double dLen = Math.Abs(built.TotalLengthMm - path.XAfterExit);
            list.Add(Mk(
                GeometryPathBuildCheckKind.TotalLengthMatchesPathEnd,
                dLen <= GeometryConsistencyTolerances.TotalLengthPathVsReportedMaxMm,
                $"TotalLengthMm={built.TotalLengthMm:F4} vs path XAfterExit={path.XAfterExit:F4} mm (Δ={dLen:F4} mm).",
                dLen > GeometryConsistencyTolerances.AxialPositionToleranceMm
                    ? GeometryPathBuildCheckSeverity.Reject
                    : GeometryPathBuildCheckSeverity.Warning));
        }

        return list;
    }

    private static GeometryPathBuildCheckItem Mk(
        GeometryPathBuildCheckKind kind,
        bool passed,
        string message,
        GeometryPathBuildCheckSeverity failSeverity) =>
        new(kind, passed, message, passed ? GeometryPathBuildCheckSeverity.Info : failSeverity);
}
