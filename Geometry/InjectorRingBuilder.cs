using PicoGK;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

/// <summary>
/// Injector reference ring / station. Delegates to <see cref="InjectorReferenceMarkersBuilder"/>.
/// Axial position follows <see cref="NozzleDesignInputs.InjectorAxialPositionRatio"/> along the swirl segment (see <see cref="InjectorReferenceMarkersBuilder"/>).
/// </summary>
public static class InjectorRingBuilder
{
    public static Voxels Build(NozzleDesignInputs d, float swirlSegmentStartX, RunConfiguration? run = null) =>
        InjectorReferenceMarkersBuilder.Build(d, swirlSegmentStartX, run);
}
