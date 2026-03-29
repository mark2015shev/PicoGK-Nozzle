using System;
using System.Numerics;
using PicoGK;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

/// <summary>
/// Ambient capture entry: mouth is never narrower than the swirl chamber ID (no throat at the lip).
/// Stations and radii come from <see cref="GeometryAssemblyPath"/> (from <see cref="InletSegmentStations"/>) only.
/// </summary>
public static class InletBuilder
{
    /// <summary>Build inlet shell using authoritative path stations (no local length recompute).</summary>
    public static Voxels Build(NozzleDesignInputs d, GeometryAssemblyPath path)
    {
        float xStart = (float)path.XInletStart;
        float xLipEnd = (float)path.XLipEnd;
        float xFlareEnd = (float)path.XAfterInlet;
        float entranceInnerR = (float)path.EntranceInnerRadiusMm;
        float chamberInnerR = (float)path.ChamberInnerRadiusMm;
        float wallThicknessMm = (float)d.WallThicknessMm;

        Vector3 lipP0 = new(xStart, 0f, 0f);
        Vector3 lipP1 = new(xLipEnd, 0f, 0f);
        Vector3 flareP0 = lipP1;
        Vector3 flareP1 = new(xFlareEnd, 0f, 0f);

        Lattice outerLat = new();
        Lattice innerLat = new();

        outerLat.AddBeam(lipP0, lipP1, entranceInnerR + wallThicknessMm, entranceInnerR + wallThicknessMm, false);
        innerLat.AddBeam(lipP0, lipP1, entranceInnerR, entranceInnerR, false);

        outerLat.AddBeam(flareP0, flareP1, entranceInnerR + wallThicknessMm, chamberInnerR + wallThicknessMm, false);
        innerLat.AddBeam(flareP0, flareP1, entranceInnerR, chamberInnerR, false);

        Voxels outer = new(outerLat);
        Voxels inner = new(innerLat);
        outer.BoolSubtract(inner);

        return outer;
    }
}
