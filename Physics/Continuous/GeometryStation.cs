namespace PicoGK_Run.Physics.Continuous;

/// <summary>Immutable geometry at one axial station (SI units where noted; radii in mm for CAD alignment).</summary>
public sealed record GeometryStation(
    double XMm,
    NozzleSegmentKind Segment,
    double InnerRadiusMm,
    double OuterGasRadiusMm,
    double FlowAreaM2,
    double HydraulicDiameterM,
    double WettedPerimeterM,
    double MeanRadiusMm,
    double DAreaDxPerM,
    double DInnerRadiusDx,
    double WallHalfAngleDeg,
    int WallSlopeSign,
    double LocalWallAreaIncrementM2,
    bool CaptureEligible,
    bool StatorPresent,
    double VaneAngleDeg);
