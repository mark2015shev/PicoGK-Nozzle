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

    /// <param name="upstreamInnerRadiusMm">Inner radius at stator / exit interface (continuous with upstream).</param>
    /// <param name="downstreamInnerRadiusMm">Inner radius at duct exit after this section.</param>
    public static Voxels Build(
        NozzleDesignInputs d,
        float xStart,
        float upstreamInnerRadiusMm,
        out float xEnd,
        out float downstreamInnerRadiusMm)
    {
        float targetExitR = 0.5f * (float)d.ExitDiameterMm;
        float r0 = Math.Max(0.5f, upstreamInnerRadiusMm);
        float r1 = Math.Max(0.5f, targetExitR);
        downstreamInnerRadiusMm = r1;

        float wallThicknessMm = (float)d.WallThicknessMm;
        float length = ComputeExitSectionLengthMm(r0, r1);

        Vector3 p0 = new(xStart, 0f, 0f);
        Vector3 p1 = new(xStart + length, 0f, 0f);

        Lattice outerLat = new();
        Lattice innerLat = new();
        // Round end caps (true): bRoundCap=false closes truncated cones with large flat disks normal to +X.
        // Combined with small L/D that reads as a gray "washer/plate" in the viewer — not a separate flange solid.
        const bool roundEndCaps = true;
        outerLat.AddBeam(p0, p1, r0 + wallThicknessMm, r1 + wallThicknessMm, roundEndCaps);
        innerLat.AddBeam(p0, p1, r0, r1, roundEndCaps);

        Voxels outer = new(outerLat);
        Voxels inner = new(innerLat);
        outer.BoolSubtract(inner);

        xEnd = xStart + length;
        return outer;
    }
}
