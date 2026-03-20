using PicoGK;
using PicoGK_Run.Core;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

public sealed class NozzleGeometryBuilder
{
    public NozzleGeometryResult Build(NozzleDesignInputs design, NozzleSolvedState solved)
    {
        float x = 0f;

        Voxels inlet = InletBuilder.Build(design, x, out float xAfterInlet);

        Voxels swirl = SwirlChamberBuilder.Build(design, xAfterInlet, out float xAfterSwirl);

        Voxels injectorMarkers = InjectorReferenceMarkersBuilder.Build(design, xAfterInlet);

        Voxels expander = ExpanderBuilder.Build(design, xAfterSwirl, out float xAfterExpander);

        Voxels stator = StatorSectionBuilder.Build(design, xAfterExpander, out float xAfterStator);

        Voxels exit = ExitBuilder.Build(design, xAfterStator, out float xAfterExit);

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
