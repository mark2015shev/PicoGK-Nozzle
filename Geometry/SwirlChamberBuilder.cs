using System.Numerics;
using PicoGK;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

public static class SwirlChamberBuilder
{
    /// <summary>Constant-bore annulus segment along +X; <paramref name="axialLengthMm"/> is exact physical length.</summary>
    public static Voxels BuildCylindricalAnnulusMm(NozzleDesignInputs d, float xStart, float axialLengthMm, out float xEnd)
    {
        float length = axialLengthMm;
        float rInner = 0.5f * (float)d.SwirlChamberDiameterMm;
        float wallThicknessMm = (float)d.WallThicknessMm;
        float rOuter = rInner + wallThicknessMm;

        Vector3 p0 = new(xStart, 0f, 0f);
        Vector3 p1 = new(xStart + length, 0f, 0f);

        Lattice outerLat = new();
        Lattice innerLat = new();
        outerLat.AddBeam(p0, p1, rOuter, rOuter, false);
        innerLat.AddBeam(p0, p1, rInner, rInner, false);

        Voxels outer = new(outerLat);
        Voxels inner = new(innerLat);
        outer.BoolSubtract(inner);

        xEnd = xStart + length;
        return outer;
    }

    /// <summary>Main chamber plus optional upstream retention (same bore), unioned for viewer group “Swirl chamber”.</summary>
    public static Voxels BuildSwirlChamberAssembly(
        NozzleDesignInputs d,
        in SwirlChamberPlacement p,
        out float xAfterMainChamberEnd)
    {
        float x0 = (float)p.MainChamberStartXMm;
        float L = (float)p.PhysicalChamberLengthBuiltMm;
        Voxels main = BuildCylindricalAnnulusMm(d, x0, L, out xAfterMainChamberEnd);

        if (p.UpstreamRetentionLengthMm > 1e-6)
        {
            float xr = (float)p.UpstreamRetentionStartXMm;
            float g = (float)p.UpstreamRetentionLengthMm;
            Voxels guard = BuildCylindricalAnnulusMm(d, xr, g, out _);
            main.BoolAdd(guard);
        }

        return main;
    }
}
