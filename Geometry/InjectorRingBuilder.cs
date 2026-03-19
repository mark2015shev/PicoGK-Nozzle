using System;
using System.Numerics;
using PicoGK;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

public static class InjectorRingBuilder
{
    private const float WallThicknessMm = 3.0f;

    public static Voxels BuildReferences(NozzleDesignInputs d, float chamberStartX)
    {
        int n = Math.Max(1, d.InjectorCount);
        float x = chamberStartX + 0.5f * (float)d.SwirlChamberLengthMm;
        float chamberR = 0.5f * (float)d.SwirlChamberDiameterMm;
        float outerR = chamberR + WallThicknessMm;

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

            // Yaw: axial/tangential mix, pitch: radial lean, roll: in-plane spin.
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

