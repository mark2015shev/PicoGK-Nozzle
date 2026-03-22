using System;
using System.Numerics;
using PicoGK;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

/// <summary>
/// Ambient capture entry: mouth is never narrower than the swirl chamber ID (no throat at the lip).
/// Short straight lip at that capture radius, then a flare that meets the swirl segment inner radius.
/// </summary>
public static class InletBuilder
{
    public static Voxels Build(NozzleDesignInputs d, float xStart, out float xEnd)
    {
        float inletD = (float)d.InletDiameterMm;
        float chamberD = (float)d.SwirlChamberDiameterMm;
        // Length scales use the larger of the two diameters so lip/flare scale with the real opening.
        float refD = Math.Max(inletD, chamberD);
        float lipLen = Math.Max(4f, 0.08f * refD);
        float flareLen = Math.Max(14f, 0.30f * refD);

        float inletNominalR = 0.5f * inletD;
        float chamberInnerR = 0.5f * chamberD;
        // Capture rule: plane-1 (entrance) inner radius ≥ swirl inner radius — not a choke ahead of the chamber.
        float entranceInnerR = Math.Max(inletNominalR, chamberInnerR);
        float wallThicknessMm = (float)d.WallThicknessMm;

        float xLipEnd = xStart + lipLen;
        float xFlareEnd = xLipEnd + flareLen;

        Vector3 lipP0 = new(xStart, 0f, 0f);
        Vector3 lipP1 = new(xLipEnd, 0f, 0f);
        Vector3 flareP0 = lipP1;
        Vector3 flareP1 = new(xFlareEnd, 0f, 0f);

        Lattice outerLat = new();
        Lattice innerLat = new();

        // Lip: constant inner radius at the capture mouth (≥ chamber ID).
        outerLat.AddBeam(lipP0, lipP1, entranceInnerR + wallThicknessMm, entranceInnerR + wallThicknessMm, false);
        innerLat.AddBeam(lipP0, lipP1, entranceInnerR, entranceInnerR, false);

        // Flare: inner goes from capture radius down to (or along) chamber ID — never widens from a throat.
        outerLat.AddBeam(flareP0, flareP1, entranceInnerR + wallThicknessMm, chamberInnerR + wallThicknessMm, false);
        innerLat.AddBeam(flareP0, flareP1, entranceInnerR, chamberInnerR, false);

        Voxels outer = new(outerLat);
        Voxels inner = new(innerLat);
        outer.BoolSubtract(inner);

        xEnd = xFlareEnd;
        return outer;
    }
}
