using System;
using PicoGK;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics.JetTrajectory;

namespace PicoGK_Run.Geometry;

/// <summary>
/// Debug visualization only: converts <see cref="JetTrajectoryResult"/> polylines into voxel beams.
/// Physics lives in <see cref="JetTrajectorySolver"/>; this type stays free of solver logic.
/// </summary>
public static class TrajectoryGeometryBuilder
{
    /// <summary>
    /// Segmented beams along each traced path for viewer overlay. Returns null when disabled or empty.
    /// </summary>
    public static Voxels? BuildDebugVoxels(JetTrajectoryResult result, RunConfiguration run)
    {
        if (!run.BuildJetTrajectoryDebugVoxels || result.TrajectoriesByInjector.Count == 0)
            return null;

        Voxels combined = new();
        bool any = false;
        foreach (System.Collections.Generic.IReadOnlyList<JetTrajectorySample> chain in result.TrajectoriesByInjector)
        {
            if (chain.Count < 2)
                continue;
            for (int k = 1; k < chain.Count; k++)
            {
                System.Numerics.Vector3 a = chain[k - 1].PositionMm;
                System.Numerics.Vector3 b = chain[k].PositionMm;
                float beamR = Math.Max(0.45f, (float)(0.14 * Math.Max(0.8, chain[k].EnvelopeRadiusMm)));
                Lattice lat = new();
                lat.AddBeam(a, b, beamR, beamR, false);
                combined.BoolAdd(new Voxels(lat));
                any = true;
            }
        }

        return any ? combined : null;
    }
}
