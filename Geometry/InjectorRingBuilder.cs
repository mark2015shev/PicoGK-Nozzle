using PicoGK;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

/// <summary>
/// Injector reference ring / station. Delegates to <see cref="InjectorReferenceMarkersBuilder"/>.
/// Axial station from <see cref="SwirlChamberPlacement"/> (clamped ratio, physical chamber length).
/// </summary>
public static class InjectorRingBuilder
{
    public static Voxels Build(NozzleDesignInputs d, in SwirlChamberPlacement p) =>
        InjectorReferenceMarkersBuilder.Build(d, p);
}
