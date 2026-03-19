using System;
using System.Numerics;
using PicoGK;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

public static class StatorSectionBuilder
{
    private const float WallThicknessMm = 3.0f;

    public static Voxels Build(NozzleDesignInputs d, float xStart, out float xEnd)
    {
        float innerR = 0.5f * (float)d.ExitDiameterMm;
        float length = Math.Max(10f, 0.10f * (float)d.ExitDiameterMm);

        Vector3 p0 = new(xStart, 0f, 0f);
        Vector3 p1 = new(xStart + length, 0f, 0f);

        Lattice shellOuter = new();
        Lattice shellInner = new();
        shellOuter.AddBeam(p0, p1, innerR + WallThicknessMm, innerR + WallThicknessMm, false);
        shellInner.AddBeam(p0, p1, innerR, innerR, false);

        Voxels section = new(shellOuter);
        section.BoolSubtract(new Voxels(shellInner));

        // Lightweight vane markers to expose vane count and angle in geometry.
        int vaneCount = Math.Max(1, d.StatorVaneCount);
        float vaneAngleRad = (float)(d.StatorVaneAngleDeg * Math.PI / 180.0);
        float dPhi = (2f * MathF.PI) / vaneCount;
        float vaneStartX = xStart + 1.0f;
        float vaneEndX = xStart + length - 1.0f;

        for (int i = 0; i < vaneCount; i++)
        {
            float phi = i * dPhi;
            Vector3 radial = new(0f, MathF.Cos(phi), MathF.Sin(phi));
            Vector3 tangent = new(0f, -MathF.Sin(phi), MathF.Cos(phi));
            Vector3 axisSkew = Vector3.Normalize((MathF.Cos(vaneAngleRad) * Vector3.UnitX) + (MathF.Sin(vaneAngleRad) * tangent));

            float vaneRadius = Math.Max(0.8f, 0.04f * (float)d.ExitDiameterMm);
            Vector3 start = new Vector3(vaneStartX, 0f, 0f) + ((innerR - 1.5f) * radial);
            Vector3 end = new Vector3(vaneEndX, 0f, 0f) + ((innerR - 1.5f) * radial) + (2.0f * axisSkew);

            Lattice vane = new();
            vane.AddBeam(start, end, vaneRadius, vaneRadius, false);
            section.BoolAdd(new Voxels(vane));
        }

        xEnd = xStart + length;
        return section;
    }
}
