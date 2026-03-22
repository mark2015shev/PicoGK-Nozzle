using System;
using System.Numerics;
using PicoGK;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

public static class StatorSectionBuilder
{
    /// <param name="upstreamInnerRadiusMm">Inner gas path radius at expander outlet (must match expander end).</param>
    /// <param name="downstreamInnerRadiusMm">Inner radius at stator outlet (same as inlet for straight shell).</param>
    public static Voxels Build(
        NozzleDesignInputs d,
        float xStart,
        float upstreamInnerRadiusMm,
        out float xEnd,
        out float downstreamInnerRadiusMm)
    {
        float innerR = Math.Max(0.5f, upstreamInnerRadiusMm);
        downstreamInnerRadiusMm = innerR;
        float wallThicknessMm = (float)d.WallThicknessMm;
        float length = Math.Max(10f, 0.10f * Math.Max(innerR * 2f, (float)d.ExitDiameterMm));

        Vector3 p0 = new(xStart, 0f, 0f);
        Vector3 p1 = new(xStart + length, 0f, 0f);

        Lattice shellOuter = new();
        Lattice shellInner = new();
        shellOuter.AddBeam(p0, p1, innerR + wallThicknessMm, innerR + wallThicknessMm, false);
        shellInner.AddBeam(p0, p1, innerR, innerR, false);

        Voxels section = new(shellOuter);
        section.BoolSubtract(new Voxels(shellInner));

        int vaneCount = Math.Max(1, d.StatorVaneCount);
        float vaneAngleRad = (float)(d.StatorVaneAngleDeg * Math.PI / 180.0);
        float dPhi = (2f * MathF.PI) / vaneCount;
        float vaneStartX = xStart + 1.0f;
        float vaneEndX = xStart + length - 1.0f;

        float vaneRadius = Math.Max(0.6f, 0.035f * Math.Max(innerR * 2f, (float)d.ExitDiameterMm));
        // Keep vane beams inside the gas passage (clear of outer wall).
        float radialMax = Math.Max(innerR - vaneRadius - 0.6f, innerR * 0.5f);
        float radialCenter = Math.Clamp(innerR * 0.72f, vaneRadius + 0.5f, radialMax);

        for (int i = 0; i < vaneCount; i++)
        {
            float phi = i * dPhi;
            Vector3 radial = new(0f, MathF.Cos(phi), MathF.Sin(phi));
            Vector3 tangent = new(0f, -MathF.Sin(phi), MathF.Cos(phi));
            Vector3 axisSkew = Vector3.Normalize((MathF.Cos(vaneAngleRad) * Vector3.UnitX) + (MathF.Sin(vaneAngleRad) * tangent));

            Vector3 start = new Vector3(vaneStartX, 0f, 0f) + (radialCenter * radial);
            Vector3 end = new Vector3(vaneEndX, 0f, 0f) + (radialCenter * radial) + (1.2f * axisSkew);

            Lattice vane = new();
            vane.AddBeam(start, end, vaneRadius, vaneRadius, false);
            section.BoolAdd(new Voxels(vane));
        }

        xEnd = xStart + length;
        return section;
    }
}
