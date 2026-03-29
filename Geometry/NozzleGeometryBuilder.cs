using PicoGK;
using PicoGK_Run.Core;
using PicoGK_Run.Infrastructure;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

/// <summary>
/// Assembles nozzle segments along +X. Neighboring solids overlap slightly so voxel unions stay watertight;
/// exact face-touching bodies can look detached in the viewer — overlap is intentional.
/// Axial stations and radii for logging: <see cref="GeometryAssemblyPath"/> (keep in sync when changing overlap or segment lengths).
/// </summary>
public sealed class NozzleGeometryBuilder
{
    /// <summary>Axial overlap between consecutive segments [mm] for robust <see cref="Voxels.BoolAdd"/>.</summary>
    public const float AssemblyOverlapMm = 0.75f;

    public NozzleGeometryResult Build(NozzleDesignInputs design, NozzleSolvedState solved, RunConfiguration? run = null)
    {
        _ = solved;
        DownstreamGeometryTargets downstream = DownstreamGeometryResolver.Resolve(design, run);
        float overlap = AssemblyOverlapMm;

        float x = 0f;

        float xAfterInlet;
        Voxels inlet;
        using (PipelineProfiler.Stage("geometry.segment.inlet"))
            inlet = InletBuilder.Build(design, x, out xAfterInlet);

        GeometryAssemblyPath assembly = GeometryAssemblyPath.Compute(design, run);
        float xSwirlStart = (float)assembly.XSwirlStart;
        float xAfterSwirl;
        Voxels swirl;
        using (PipelineProfiler.Stage("geometry.segment.swirlChamber"))
            swirl = SwirlChamberBuilder.Build(design, xSwirlStart, out xAfterSwirl, run);

        Voxels injectorMarkers;
        using (PipelineProfiler.Stage("geometry.segment.injectorMarkers"))
            injectorMarkers = InjectorRingBuilder.Build(design, xSwirlStart, run);

        float xExpStart = xAfterSwirl - overlap;
        float xAfterExpander;
        float expanderEndInnerR;
        Voxels expander;
        using (PipelineProfiler.Stage("geometry.segment.expander"))
            expander = ExpanderBuilder.Build(design, xExpStart, downstream, out xAfterExpander, out expanderEndInnerR);

        float xStatorStart = xAfterExpander - overlap;
        float xAfterStator;
        float statorDownstreamInnerR;
        Voxels stator;
        using (PipelineProfiler.Stage("geometry.segment.stator"))
            stator = StatorSectionBuilder.Build(
                design,
                xStatorStart,
                downstream,
                out xAfterStator,
                out statorDownstreamInnerR);

        float xExitStart = xAfterStator - overlap;
        float xAfterExit;
        Voxels exit;
        using (PipelineProfiler.Stage("geometry.segment.exit"))
            exit = ExitBuilder.Build(
                design,
                xExitStart,
                downstream,
                out xAfterExit,
                out _);

        return new NozzleGeometryResult(
            inlet: inlet,
            swirlChamber: swirl,
            injectorReferenceMarkers: injectorMarkers,
            expander: expander,
            statorSection: stator,
            exit: exit,
            injectorCountPlaced: design.InjectorCount,
            totalLengthMm: xAfterExit);
    }
}
