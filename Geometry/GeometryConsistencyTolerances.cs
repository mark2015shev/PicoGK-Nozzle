namespace PicoGK_Run.Geometry;

/// <summary>Single place for geometry cross-check tolerances (path, build, audit, continuity).</summary>
public static class GeometryConsistencyTolerances
{
    /// <summary>Axial station agreement: path vs nominal build chain [mm].</summary>
    public const double AxialPositionToleranceMm = 0.05;

    /// <summary>Inner/outer diameter agreement [mm] (matches legacy downstream annulus continuity).</summary>
    public const double DiameterToleranceMm = 0.051;

    /// <summary>Segment length / total length agreement [mm].</summary>
    public const double LengthToleranceMm = 0.05;

    /// <summary>Assembly overlap for watertight unions — used when interpreting junction vs segment ends.</summary>
    public const double AssemblyOverlapNominalMm = 0.75;

    /// <summary>Allowed difference path vs reported total length after authoritative build [mm].</summary>
    public const double TotalLengthPathVsReportedMaxMm = 0.02;

    /// <summary>Below this |nominal cone R − declared exit R| we skip “reference vs authoritative” narrative [mm].</summary>
    public const double NominalVersusAuthoritativeNarrativeMinDeltaMm = 0.25;
}
