using PicoGK;
using PicoGK_Run.Core;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

public sealed class NozzleGeometryBuilder
{
    public NozzleGeometryResult Build(NozzleDesignInputs design, NozzleSolvedState solved)
    {
        float x = 0f;
        Voxels nozzle = new Voxels();

        Voxels inlet = InletBuilder.Build(design, x, out float xAfterInlet);
        nozzle.BoolAdd(inlet);

        Voxels swirl = SwirlChamberBuilder.Build(design, xAfterInlet, out float xAfterSwirl);
        nozzle.BoolAdd(swirl);

        Voxels injectorRefs = InjectorRingBuilder.BuildReferences(design, xAfterInlet);
        nozzle.BoolAdd(injectorRefs);

        Voxels expander = ExpanderBuilder.Build(design, xAfterSwirl, out float xAfterExpander);
        nozzle.BoolAdd(expander);

        Voxels stator = StatorSectionBuilder.Build(design, xAfterExpander, out float xAfterStator);
        nozzle.BoolAdd(stator);

        Voxels exit = ExitBuilder.Build(design, xAfterStator, out float xAfterExit);
        nozzle.BoolAdd(exit);

        return new NozzleGeometryResult(
            nozzleBody: nozzle,
            injectorReferences: injectorRefs,
            injectorCountPlaced: design.InjectorCount,
            totalLengthMm: xAfterExit);
    }
}

