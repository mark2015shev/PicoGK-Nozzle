using PicoGK_Run.Core;
using PicoGK_Run.Geometry;
using PicoGK_Run.Physics;

namespace PicoGK_Run;

/// <summary>
/// Central orchestration for the LEAP-style pipeline:
/// Inputs → Physics → Solved State → Geometry → STL/Visualization.
/// </summary>
internal static class AppPipeline
{
    public static void Run(JetStreamK320 jet, AmbientAir ambient, NozzleParameters p)
    {
        NozzleSolvedState solved = NozzleSolver.Solve(jet, ambient, p);
        _ = NozzleGeometryBuilder.BuildPlaceholder(p, solved);
    }
}

