using PicoGK;
using PicoGK_Run.Core;
using PicoGK_Run.Geometry;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Infrastructure.Services;

/// <summary>Voxel assembly from driven SI design (single build entry for the pipeline).</summary>
internal static class GeometryBuildService
{
    public static NozzleGeometryResult Build(
        NozzleDesignInputs drivenDesign,
        NozzleSolvedState solved,
        RunConfiguration run,
        Voxels? jetTrajectoryDebug)
    {
        var geometryBuilder = new NozzleGeometryBuilder();
        return geometryBuilder.Build(drivenDesign, solved, run, jetTrajectoryDebug);
    }
}
