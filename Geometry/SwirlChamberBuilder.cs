using System;
using System.Numerics;
using PicoGK;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Geometry;

public static class SwirlChamberBuilder
{
    /// <summary>Axial swirl segment length [mm] used for voxels and <see cref="GeometryAssemblyPath"/> (includes rule-of-six floor when enabled).</summary>
    public static double EffectiveLengthMm(NozzleDesignInputs d, RunConfiguration? run = null)
    {
        double lenMm = Math.Max(d.SwirlChamberLengthMm, 1.0);
        if (run?.EnforceEjectorMixingRuleOfSix == true)
            lenMm = Math.Max(lenMm, VortexEntrainmentPhysics.MixingLengthMinimumMmRuleOfSix(d));
        return lenMm;
    }

    public static Voxels Build(NozzleDesignInputs d, float xStart, out float xEnd, RunConfiguration? run = null)
    {
        float length = (float)EffectiveLengthMm(d, run);
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
}

