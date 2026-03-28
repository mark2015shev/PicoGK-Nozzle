using System;
using System.Numerics;
using PicoGK;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

/// <summary>
/// Conical expander: inner wall runs from chamber ID to <see cref="DownstreamGeometryTargets.RecoveryAnnulusRadiusMm"/>
/// over <see cref="DownstreamGeometryTargets.EffectiveExpanderLengthMm"/> (single authoritative downstream radius).
/// </summary>
public static class ExpanderBuilder
{
    public static Voxels Build(NozzleDesignInputs d, float xStart, DownstreamGeometryTargets downstream, out float xEnd, out float endInnerRadiusMm)
    {
        float length = (float)downstream.EffectiveExpanderLengthMm;
        float chamberInnerR = (float)downstream.ChamberInnerRadiusMm;
        float wallThicknessMm = (float)d.WallThicknessMm;
        float exitInnerR = (float)downstream.RecoveryAnnulusRadiusMm;
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
