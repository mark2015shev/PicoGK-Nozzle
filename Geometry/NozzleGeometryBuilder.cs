using PicoGK;
using PicoGK_Run.Core;
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

    public NozzleGeometryResult Build(NozzleDesignInputs design, NozzleSolvedState solved)
    {
        float overlap = AssemblyOverlapMm;

        float x = 0f;

        Voxels inlet = InletBuilder.Build(design, x, out float xAfterInlet);

        float xSwirlStart = xAfterInlet - overlap;
        Voxels swirl = SwirlChamberBuilder.Build(design, xSwirlStart, out float xAfterSwirl);

        // Injector station: measured from nominal chamber inlet plane (not overlap-shifted start).
        Voxels injectorMarkers = InjectorRingBuilder.Build(design, xAfterInlet);

        float xExpStart = xAfterSwirl - overlap;
        Voxels expander = ExpanderBuilder.Build(design, xExpStart, out float xAfterExpander, out float expanderEndInnerR);

        float xStatorStart = xAfterExpander - overlap;
        Voxels stator = StatorSectionBuilder.Build(
            design,
            xStatorStart,
            expanderEndInnerR,
            out float xAfterStator,
            out float statorDownstreamInnerR);

        float xExitStart = xAfterStator - overlap;
        Voxels exit = ExitBuilder.Build(
            design,
            xExitStart,
            statorDownstreamInnerR,
            out float xAfterExit,
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
