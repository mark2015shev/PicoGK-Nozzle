namespace PicoGK_Run.Geometry;

/// <summary>Neighbouring-segment inner-radius / diameter continuity check at logical interfaces.</summary>
public sealed record TransitionMismatchDebugInfo(
    string FromSegment,
    string ToSegment,
    double UpstreamRadiusMm,
    double DownstreamRadiusMm,
    double DeltaRadiusMm,
    double DeltaDiameterMm,
    bool WithinTolerance,
    string Note);
