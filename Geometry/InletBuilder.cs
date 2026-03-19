using System.Numerics;
using PicoGK;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

public static class InletBuilder
{
    public static Voxels Build(NozzleDesignInputs d, float xStart, out float xEnd)
    {
        float length = Math.Max(16f, 0.30f * (float)d.InletDiameterMm);
        float inletInnerR = 0.5f * (float)d.InletDiameterMm;
        float chamberInnerR = 0.5f * (float)d.SwirlChamberDiameterMm;
        float wallThicknessMm = (float)d.WallThicknessMm;

        Vector3 p0 = new(xStart, 0f, 0f);
        Vector3 p1 = new(xStart + length, 0f, 0f);

        Lattice outerLat = new();
        Lattice innerLat = new();
        outerLat.AddBeam(p0, p1, inletInnerR + wallThicknessMm, chamberInnerR + wallThicknessMm, false);
        innerLat.AddBeam(p0, p1, inletInnerR, chamberInnerR, false);

        Voxels outer = new(outerLat);
        Voxels inner = new(innerLat);
        outer.BoolSubtract(inner);

        xEnd = xStart + length;
        return outer;
    }
}
