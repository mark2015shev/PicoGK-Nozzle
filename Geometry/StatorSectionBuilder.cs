using System;
using System.Numerics;
using PicoGK;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

/// <summary>
/// Stator: annular shell + solid hub/centerbody (no cusped tip) + blades from hub OD to casing ID — reference solids only.
/// </summary>
public static class StatorSectionBuilder
{
    /// <param name="upstreamInnerRadiusMm">Inner gas path radius at expander outlet (must match expander end).</param>
    /// <param name="downstreamInnerRadiusMm">Casing inner radius at stator outlet (annulus outer boundary).</param>
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

        double lenAuto = Math.Max(10.0, 0.10 * Math.Max(innerR * 2.0, d.ExitDiameterMm));
        float length = (float)(d.StatorAxialLengthMm > 1.0 ? d.StatorAxialLengthMm : lenAuto);

        Vector3 p0 = new(xStart, 0f, 0f);
        Vector3 p1 = new(xStart + length, 0f, 0f);

        Lattice shellOuter = new();
        Lattice shellInner = new();
        shellOuter.AddBeam(p0, p1, innerR + wallThicknessMm, innerR + wallThicknessMm, false);
        shellInner.AddBeam(p0, p1, innerR, innerR, false);

        Voxels section = new(shellOuter);
        section.BoolSubtract(new Voxels(shellInner));

        float hubDmm = (float)(d.StatorHubDiameterMm > 0.5 ? d.StatorHubDiameterMm : 0.28 * d.SwirlChamberDiameterMm);
        float rHub = 0.5f * hubDmm;
        float maxHubR = innerR * 0.82f - 0.8f;
        rHub = Math.Clamp(rHub, 3f, Math.Max(maxHubR, 4f));

        float bulletLen = Math.Clamp(0.42f * (float)d.SwirlChamberDiameterMm, 5f, 11f);
        float tipR = Math.Max(0.85f, 0.22f * rHub);
        Lattice nose = new();
        nose.AddBeam(
            new Vector3(xStart - bulletLen, 0f, 0f),
            new Vector3(xStart, 0f, 0f),
            tipR,
            rHub,
            false);

        Lattice hubCyl = new();
        hubCyl.AddBeam(p0, p1, rHub, rHub, false);

        section.BoolAdd(new Voxels(nose));
        section.BoolAdd(new Voxels(hubCyl));

        int vaneCount = Math.Max(1, d.StatorVaneCount);
        float vaneAngleRad = (float)(d.StatorVaneAngleDeg * Math.PI / 180.0);
        float dPhi = (2f * MathF.PI) / vaneCount;
        float marginX = Math.Clamp(0.06f * length, 0.8f, 2.2f);
        float vaneStartX = xStart + marginX;
        float vaneEndX = xStart + length - marginX;

        double span = innerR - rHub;
        float chordMm = (float)(d.StatorBladeChordMm > 0.5 ? d.StatorBladeChordMm : Math.Max(3.5, 0.14 * span));
        float vaneR = Math.Clamp(0.20f * chordMm, 0.48f, 3.2f);
        float rStart = rHub + vaneR + 0.55f;
        float rEnd = innerR - vaneR - 0.9f;
        if (rEnd <= rStart + 0.5f)
            rEnd = Math.Min(innerR - 1f, rStart + 1.5f);

        for (int i = 0; i < vaneCount; i++)
        {
            float phi = i * dPhi;
            Vector3 radial = new(0f, MathF.Cos(phi), MathF.Sin(phi));
            Vector3 tangent = new(0f, -MathF.Sin(phi), MathF.Cos(phi));
            Vector3 axisSkew = Vector3.Normalize((MathF.Cos(vaneAngleRad) * Vector3.UnitX) + (MathF.Sin(vaneAngleRad) * tangent));

            Vector3 start = new Vector3(vaneStartX, 0f, 0f) + (rStart * radial);
            Vector3 end = new Vector3(vaneEndX, 0f, 0f) + (rEnd * radial) + (1.1f * axisSkew);

            Lattice vane = new();
            vane.AddBeam(start, end, vaneR, vaneR, false);
            section.BoolAdd(new Voxels(vane));
        }

        xEnd = xStart + length;
        return section;
    }
}
