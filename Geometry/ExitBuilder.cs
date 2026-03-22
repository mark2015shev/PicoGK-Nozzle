using System;
using System.Numerics;
using PicoGK;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

public static class ExitBuilder
{
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
        float length = Math.Max(12f, 0.12f * Math.Max(r0 * 2f, r1 * 2f));

        Vector3 p0 = new(xStart, 0f, 0f);
        Vector3 p1 = new(xStart + length, 0f, 0f);

        Lattice outerLat = new();
        Lattice innerLat = new();
        outerLat.AddBeam(p0, p1, r0 + wallThicknessMm, r1 + wallThicknessMm, false);
        innerLat.AddBeam(p0, p1, r0, r1, false);

        Voxels outer = new(outerLat);
        Voxels inner = new(innerLat);
        outer.BoolSubtract(inner);

        xEnd = xStart + length;
        return outer;
    }
}
