namespace PicoGK_Run.Geometry;

/// <summary>
/// One contiguous axial segment of the built nozzle solid (inner gas path + wall context).
/// Numbers mirror <see cref="InletBuilder"/>, <see cref="SwirlChamberBuilder"/>, etc. — geometry only.
/// </summary>
public sealed record GeometrySegmentDebugInfo(
    string Name,
    double XStartMm,
    double XEndMm,
    double LengthMm,
    double RadiusStartMm,
    double RadiusEndMm,
    double DiameterStartMm,
    double DiameterEndMm,
    /// <summary>Inner-wall cone half-angle [deg] when meaningful; null if parallel to axis or N/A.</summary>
    double? HalfAngleDeg,
    double WallThicknessMm,
    double InnerFlowAreaStartMm2,
    double InnerFlowAreaEndMm2,
    double OuterDiameterStartMm,
    double OuterDiameterEndMm,
    string Notes);
