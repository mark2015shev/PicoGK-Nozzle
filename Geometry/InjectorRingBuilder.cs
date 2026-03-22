using PicoGK;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

/// <summary>
/// Injector reference ring / station. Delegates to <see cref="InjectorReferenceMarkersBuilder"/>.
/// Axial position follows <see cref="NozzleDesignInputs.InjectorAxialPositionRatio"/> along the chamber.
/// </summary>
public static class InjectorRingBuilder
{
    public static Voxels Build(NozzleDesignInputs d, float chamberStartX) =>
        InjectorReferenceMarkersBuilder.Build(d, chamberStartX);
}
