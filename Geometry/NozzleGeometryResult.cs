using PicoGK;

namespace PicoGK_Run.Geometry;

/// <summary>
/// Staged nozzle solids for viewer coloring (one <see cref="Voxels"/> per segment).
/// </summary>
public sealed class NozzleGeometryResult
{
    public Voxels Inlet { get; }
    public Voxels SwirlChamber { get; }

    /// <summary>Reference-only marker beams — not real injector passages.</summary>
    public Voxels InjectorReferenceMarkers { get; }

    public Voxels Expander { get; }
    public Voxels StatorSection { get; }
    public Voxels Exit { get; }

    public int InjectorCountPlaced { get; }
    public double TotalLengthMm { get; }

    /// <summary>Optional physics-traced jet path debug overlay (not part of the metal CAD envelope).</summary>
    public Voxels? JetTrajectoryDebug { get; }

    public NozzleGeometryResult(
        Voxels inlet,
        Voxels swirlChamber,
        Voxels injectorReferenceMarkers,
        Voxels expander,
        Voxels statorSection,
        Voxels exit,
        int injectorCountPlaced,
        double totalLengthMm,
        Voxels? jetTrajectoryDebug = null)
    {
        Inlet = inlet;
        SwirlChamber = swirlChamber;
        InjectorReferenceMarkers = injectorReferenceMarkers;
        Expander = expander;
        StatorSection = statorSection;
        Exit = exit;
        InjectorCountPlaced = injectorCountPlaced;
        TotalLengthMm = totalLengthMm;
        JetTrajectoryDebug = jetTrajectoryDebug;
    }
}
