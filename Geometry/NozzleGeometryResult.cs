using PicoGK;

namespace PicoGK_Run.Geometry;

public sealed class NozzleGeometryResult
{
    public Voxels NozzleBody { get; }
    public Voxels InjectorReferences { get; }
    public int InjectorCountPlaced { get; }
    public double TotalLengthMm { get; }

    public NozzleGeometryResult(Voxels nozzleBody, Voxels injectorReferences, int injectorCountPlaced, double totalLengthMm)
    {
        NozzleBody = nozzleBody;
        InjectorReferences = injectorReferences;
        InjectorCountPlaced = injectorCountPlaced;
        TotalLengthMm = totalLengthMm;
    }
}

