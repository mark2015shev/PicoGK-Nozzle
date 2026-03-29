namespace PicoGK_Run.Geometry;

/// <summary>Single structured geometry continuity outcome (design-path audit; not voxel mesh QC).</summary>
public sealed record GeometryConsistencyCheckItem(
    GeometryConsistencyCheckKind Kind,
    bool Passed,
    string Message,
    GeometryConsistencySeverity Severity);

public enum GeometryConsistencyCheckKind
{
    DiameterPhysical,
    AxialExtent,
    SwirlChamberUpstreamPlacement,
    AxialStationOrdering,
    ExpanderChamberRadiusRatio,
    InletLipVsChamber,
    HubBlockage,
    DownstreamAnnulusRadiusExpanderVsRecovery,
    DownstreamAnnulusRadiusExitVsRecovery,
    ExitConstantAreaMismatch
}

public enum GeometryConsistencySeverity
{
    Info,
    Warning,
    Reject
}
