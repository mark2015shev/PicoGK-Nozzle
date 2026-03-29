using PicoGK;
using PicoGK_Run.Core;
using PicoGK_Run.Infrastructure;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

/// <summary>
/// Assembles nozzle segments along +X. Axial stations and lengths are taken only from
/// <see cref="GeometryAssemblyPath"/> after <see cref="GeometryAssemblyPath.Compute"/> — no per-segment length re-solve.
/// </summary>
public sealed class NozzleGeometryBuilder
{
    /// <summary>Axial overlap between consecutive segments [mm] for robust <see cref="Voxels.BoolAdd"/>.</summary>
    public const float AssemblyOverlapMm = 0.75f;

    public NozzleGeometryResult Build(
        NozzleDesignInputs design,
        NozzleSolvedState solved,
        RunConfiguration? run = null,
        Voxels? jetTrajectoryDebug = null)
    {
        _ = solved;
        GeometryAssemblyPath assembly = GeometryAssemblyPath.Compute(design, run);

        Voxels inlet;
        using (PipelineProfiler.Stage("geometry.segment.inlet"))
            inlet = InletBuilder.Build(design, assembly);

        SwirlChamberPlacement swirlPl = assembly.SwirlPlacement;
        Voxels swirl;
        using (PipelineProfiler.Stage("geometry.segment.swirlChamber"))
            swirl = SwirlChamberBuilder.BuildSwirlChamberAssembly(design, swirlPl, out _);

        Voxels injectorMarkers;
        using (PipelineProfiler.Stage("geometry.segment.injectorMarkers"))
            injectorMarkers = InjectorRingBuilder.Build(design, swirlPl);

        Voxels expander;
        using (PipelineProfiler.Stage("geometry.segment.expander"))
            expander = ExpanderBuilder.Build(design, assembly, out _, out _);

        Voxels stator;
        using (PipelineProfiler.Stage("geometry.segment.stator"))
            stator = StatorSectionBuilder.Build(design, assembly, out _, out _);

        Voxels exit;
        using (PipelineProfiler.Stage("geometry.segment.exit"))
            exit = ExitBuilder.Build(design, assembly, out _, out _);

        return new NozzleGeometryResult(
            inlet: inlet,
            swirlChamber: swirl,
            injectorReferenceMarkers: injectorMarkers,
            expander: expander,
            statorSection: stator,
            exit: exit,
            injectorCountPlaced: design.InjectorCount,
            totalLengthMm: assembly.XAfterExit,
            jetTrajectoryDebug: jetTrajectoryDebug);
    }
}
