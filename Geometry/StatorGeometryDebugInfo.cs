namespace PicoGK_Run.Geometry;

/// <summary>Hub + annulus + reference vane parameters (matches <see cref="StatorSectionBuilder"/>).</summary>
public sealed record StatorGeometryDebugInfo(
    double StatorStartXMm,
    double StatorEndXMm,
    double StatorAxialLengthMm,
    double CasingInnerRadiusStartMm,
    double CasingInnerRadiusEndMm,
    double CasingOuterRadiusStartMm,
    double CasingOuterRadiusEndMm,
    int VaneCount,
    double VaneAngleDeg,
    double VaneSpanMm,
    double VaneChordMm,
    double VaneBeamRadiusMm,
    double VaneRootRadiusMm,
    double VaneTipRadiusMm,
    double VaneAxialRootStartMm,
    double VaneAxialRootEndMm,
    double HubRadiusMm,
    double HubNoseTipRadiusMm,
    double HubNoseStartXMm,
    /// <summary>Inner casing: Constant / Converging / Diverging along stator length.</summary>
    string CasingAxialChange,
    /// <summary>Whether vanes sit in constant-, converging-, or diverging-area annulus (casing vs hub).</summary>
    string VanePassageClassification,
    /// <summary>One of: constant-area recovery, diffuser-mounted, transition-mounted, exit-mounted (see report text).</summary>
    string MountInterpretationPrimary,
    IReadOnlyList<string> MountInterpretationTags);
