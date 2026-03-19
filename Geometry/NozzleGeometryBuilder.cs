using System.Numerics;
using PicoGK;
using PicoGK_Run.Core;

namespace PicoGK_Run.Geometry;

public static class NozzleGeometryBuilder
{
    /// <summary>
    /// Builds a placeholder solid based on solved values.
    /// No physics is performed here.
    /// </summary>
    public static Voxels BuildPlaceholder(NozzleParameters p, NozzleSolvedState solved)
    {
        double lengthMM = Math.Max(1.0, p.MixerLengthMM + p.SwirlChamberLengthMM);
        float rOuter = (float)(0.5 * p.ExitDiameterMM);

        // Placeholder: simple solid cylinder along X axis.
        Vector3 p0 = new(0f, 0f, 0f);
        Vector3 p1 = new((float)lengthMM, 0f, 0f);

        Lattice lat = new();
        lat.AddBeam(p0, p1, rOuter, rOuter, false);
        return new Voxels(lat);
    }
}

