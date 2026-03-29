using PicoGK_Run.Geometry;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Infrastructure.Services;

/// <summary>Single owner for design-path geometry continuity (declared mm assembly); voxel mesh QC is separate.</summary>
internal static class GeometryConsistencyService
{
    public static GeometryContinuityReport? EvaluateDrivenDesign(
        NozzleDesignInputs drivenDesign,
        bool runCheck,
        RunConfiguration run) =>
        runCheck ? GeometryContinuityValidator.Check(drivenDesign, run) : null;
}
