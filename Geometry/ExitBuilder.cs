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
        if (wallThicknessMm <= 0f)
            throw new InvalidOperationException("Exit geometry invalid: WallThicknessMm must be > 0 for a hollow shell.");

        if (r0 <= 0f || r1 <= 0f)
            throw new InvalidOperationException(
                "Exit geometry invalid: inner radius collapsed to non-positive — nozzle bore must stay open (R_inner_exit > 0).");

        float rOut0 = r0 + wallThicknessMm;
        float rOut1 = r1 + wallThicknessMm;
        if (rOut0 <= r0 || rOut1 <= r1)
            throw new InvalidOperationException("Exit geometry invalid: outer radius must exceed inner radius at both ends.");

        float length = ComputeExitSectionLengthMm(r0, r1);

        Vector3 p0 = new(xStart, 0f, 0f);
        Vector3 p1 = new(xStart + length, 0f, 0f);

        Lattice outerLat = new();
        Lattice innerLat = new();

        // CRITICAL: roundCap MUST be false for an open nozzle exit.
        // roundCap true adds spherical end caps on the beam, which closes the annulus into bulb/dome-like solids.
        // false => flat annular faces normal to +X at both ends; inner beam is subtracted so the bore stays open.
        const bool flatAnnulusEnds = false;
        outerLat.AddBeam(p0, p1, rOut0, rOut1, flatAnnulusEnds);
        innerLat.AddBeam(p0, p1, r0, r1, flatAnnulusEnds);

        Voxels outer = new(outerLat);
        Voxels inner = new(innerLat);
        outer.BoolSubtract(inner);

        xEnd = xStart + length;

        Library.Log("[ExitBuilder] Hollow exit: outer/inner truncated cones, BoolSubtract — no sphere/dome union, no extra cap step.");
        Library.Log($"[ExitBuilder] length_mm={length:F3}  R_inner_start/end={r0:F3}/{r1:F3}  R_outer_start/end={rOut0:F3}/{rOut1:F3}  wall_mm={wallThicknessMm:F3}");
        Library.Log($"[ExitBuilder] AddBeam roundCap={flatAnnulusEnds} (false = open annulus at exit plane; true would close bore with caps).");
        Library.Log($"[ExitBuilder] Meridian profile points (X_mm, R_inner_bore, R_outer_wall): ({xStart:F3},{r0:F3},{rOut0:F3}) -> ({xEnd:F3},{r1:F3},{rOut1:F3})");

        return outer;
    }
}
