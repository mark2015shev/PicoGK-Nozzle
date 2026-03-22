using System;
using System.Numerics;
using PicoGK;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

/// <summary>
/// Conical expander: inner radius follows <see cref="NozzleDesignInputs.ExpanderHalfAngleDeg"/> exactly
/// (no averaging with <see cref="NozzleDesignInputs.ExitDiameterMm"/>). Implied exit diameter is geometric.
/// </summary>
public static class ExpanderBuilder
{
    public static Voxels Build(NozzleDesignInputs d, float xStart, out float xEnd, out float endInnerRadiusMm)
    {
        float length = (float)d.ExpanderLengthMm;
        float chamberInnerR = 0.5f * (float)d.SwirlChamberDiameterMm;
        float wallThicknessMm = (float)d.WallThicknessMm;

        float halfAngleRad = (float)(Math.PI * d.ExpanderHalfAngleDeg / 180.0);
        float exitInnerR = chamberInnerR + (MathF.Tan(halfAngleRad) * length);
        endInnerRadiusMm = exitInnerR;

        Vector3 p0 = new(xStart, 0f, 0f);
        Vector3 p1 = new(xStart + length, 0f, 0f);

        Lattice outerLat = new();
        Lattice innerLat = new();
        outerLat.AddBeam(p0, p1, chamberInnerR + wallThicknessMm, exitInnerR + wallThicknessMm, false);
        innerLat.AddBeam(p0, p1, chamberInnerR, exitInnerR, false);

        Voxels outer = new(outerLat);
        Voxels inner = new(innerLat);
        outer.BoolSubtract(inner);

        xEnd = xStart + length;
        return outer;
    }
}
