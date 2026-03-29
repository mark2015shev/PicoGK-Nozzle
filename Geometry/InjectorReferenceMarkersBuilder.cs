using System;
using System.Numerics;
using PicoGK;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

/// <summary>
/// <b>Reference-only</b> geometry: small beams showing intended injector station and direction.
/// These are <b>not</b> flow passages, drilled holes, or CFD mesh — placeholders until real injector ports are modeled.
/// </summary>
public static class InjectorReferenceMarkersBuilder
{
    /// <summary>
    /// Axial station: <paramref name="swirlSegmentStartX"/> + ratio × effective chamber length (0 = upstream, 1 = downstream / expander).
    /// Length matches <see cref="SwirlChamberBuilder.EffectiveLengthMm"/> when <paramref name="run"/> is provided.
    /// </summary>
    public static Voxels Build(NozzleDesignInputs d, float swirlSegmentStartX, RunConfiguration? run = null)
    {
        int n = Math.Max(1, d.InjectorCount);
        float ratio = (float)Math.Clamp(d.InjectorAxialPositionRatio, 0.0, 1.0);
        float len = (float)SwirlChamberBuilder.EffectiveLengthMm(d, run);
        float x = swirlSegmentStartX + ratio * len;
        float chamberR = 0.5f * (float)d.SwirlChamberDiameterMm;
        float wallThicknessMm = (float)d.WallThicknessMm;
        float outerR = chamberR + wallThicknessMm;

        float markerRadius = Math.Max(0.6f, 0.5f * (float)Math.Min(d.InjectorWidthMm, d.InjectorHeightMm));
        float markerLength = Math.Max(6f, 1.5f * (float)Math.Max(d.InjectorWidthMm, d.InjectorHeightMm));

        float yawRad = (float)(d.InjectorYawAngleDeg * Math.PI / 180.0);
        float pitchRad = (float)(d.InjectorPitchAngleDeg * Math.PI / 180.0);
        float rollRad = (float)(d.InjectorRollAngleDeg * Math.PI / 180.0);

        Voxels combined = new Voxels();
        float dPhi = (2f * MathF.PI) / n;

        for (int i = 0; i < n; i++)
        {
            float phi = i * dPhi;
            Vector3 radial = new(0f, MathF.Cos(phi), MathF.Sin(phi));
            Vector3 tangent = new(0f, -MathF.Sin(phi), MathF.Cos(phi));
            Vector3 axial = Vector3.UnitX;

            Vector3 baseDirection = Vector3.Normalize((MathF.Cos(yawRad) * axial) + (MathF.Sin(yawRad) * tangent));
            Vector3 pitchedDirection = Vector3.Normalize((MathF.Cos(pitchRad) * baseDirection) + (MathF.Sin(pitchRad) * -radial));
            Vector3 rollDirection = Vector3.Normalize((MathF.Cos(rollRad) * pitchedDirection) + (MathF.Sin(rollRad) * tangent));
            Vector3 direction = rollDirection;

            Vector3 p0 = new Vector3(x, 0f, 0f) + (outerR * radial);
            Vector3 p1 = p0 + (markerLength * direction);

            Lattice lat = new();
            lat.AddBeam(p0, p1, markerRadius, markerRadius, false);
            combined.BoolAdd(new Voxels(lat));
        }

        return combined;
    }
}
