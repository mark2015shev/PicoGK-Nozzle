using System;
using System.Numerics;
using PicoGK;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

public static class ExitBuilder
{
    /// <summary>
    /// Axial length of the exit annular frustum [mm]. Uses max of: 12 mm floor, a slenderness term vs max bore,
    /// and a term proportional to inner-radius change so the cone is not arbitrarily steep.
    /// Must stay in sync with geometry audits (<see cref="GeometryAssemblyPath"/>).
    /// </summary>
    public static float ComputeExitSectionLengthMm(float innerRadiusStart, float innerRadiusEnd)
    {
        float dMax = Math.Max(innerRadiusStart * 2f, innerRadiusEnd * 2f);
        float radialDelta = Math.Abs(innerRadiusEnd - innerRadiusStart);
        float fromDiameter = 0.20f * dMax;
        float fromDelta = radialDelta > 1e-4f ? 4.0f * radialDelta : 0f;
        return Math.Max(12f, Math.Max(fromDiameter, fromDelta));
    }

    /// <summary>Build exit section from path radii and axial length (authoritative — no recomputed L).</summary>
    public static Voxels Build(
        NozzleDesignInputs d,
        GeometryAssemblyPath path,
        out float xEnd,
        out float downstreamInnerRadiusMm)
    {
        float xStart = (float)path.XExitStart;
        float r0 = Math.Max(0.5f, (float)path.ExitInnerRadiusStartMm);
        float r1 = Math.Max(0.5f, (float)path.ExitInnerRadiusEndMm);
        downstreamInnerRadiusMm = r1;

        float wallThicknessMm = (float)d.WallThicknessMm;
        if (wallThicknessMm <= 0f)
            throw new InvalidOperationException("Exit geometry invalid: WallThicknessMm must be > 0 for a hollow shell.");

        if (r0 <= 0f || r1 <= 0f)
            throw new InvalidOperationException(
                "Exit geometry invalid: inner radius collapsed to non-positive — nozzle bore must stay open (R_inner_exit > 0).");

        float rOut0 = r0 + wallThicknessMm;
        float rOut1 = r1 + wallThicknessMm;
        if (rOut0 <= r0 || rOut1 <= r1)
            throw new InvalidOperationException("Exit geometry invalid: outer radius must exceed inner radius at both ends.");

        float length = (float)path.ExitSectionLengthMm;

        Vector3 p0 = new(xStart, 0f, 0f);
        Vector3 p1 = new(xStart + length, 0f, 0f);

        Lattice outerLat = new();
        Lattice innerLat = new();

        const bool flatAnnulusEnds = false;
        outerLat.AddBeam(p0, p1, rOut0, rOut1, flatAnnulusEnds);
        innerLat.AddBeam(p0, p1, r0, r1, flatAnnulusEnds);

        Voxels outer = new(outerLat);
        Voxels inner = new(innerLat);
        outer.BoolSubtract(inner);

        xEnd = xStart + length;
        return outer;
    }
}
