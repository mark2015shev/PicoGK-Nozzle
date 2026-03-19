using System.Numerics;
using PicoGK;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

public static class ExpanderBuilder
{
    private const float WallThicknessMm = 3.0f;

    public static Voxels Build(NozzleDesignInputs d, float xStart, out float xEnd)
    {
        float length = (float)d.ExpanderLengthMm;
        float chamberInnerR = 0.5f * (float)d.SwirlChamberDiameterMm;
        float exitInnerR = 0.5f * (float)d.ExitDiameterMm;

        Vector3 p0 = new(xStart, 0f, 0f);
        Vector3 p1 = new(xStart + length, 0f, 0f);

        Lattice outerLat = new();
        Lattice innerLat = new();
        outerLat.AddBeam(p0, p1, chamberInnerR + WallThicknessMm, exitInnerR + WallThicknessMm, false);
        innerLat.AddBeam(p0, p1, chamberInnerR, exitInnerR, false);

        Voxels outer = new(outerLat);
        Voxels inner = new(innerLat);
        outer.BoolSubtract(inner);

        xEnd = xStart + length;
        return outer;
    }
}
