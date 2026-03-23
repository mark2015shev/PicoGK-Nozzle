namespace PicoGK_Run.Geometry;

/// <summary>One viewer group / one <see cref="PicoGK.Voxels"/> added to the viewer.</summary>
public sealed record BuiltGeometrySolidAuditEntry(
    int ViewerGroupId,
    string ViewerGroupName,
    string ResultPropertyName,
    string GeneratorType,
    string GeneratorMethod,
    string SolidDescription,
    double XStartMm,
    double XEndMm,
    double LengthMm,
    double RInnerStartMm,
    double RInnerEndMm,
    double ROuterStartMm,
    double ROuterEndMm,
    double? HalfAngleDeg,
    double WallThicknessMm,
    /// <summary>High-level classification: ConstantArea, Converging, Diverging, Hub, Blade, ReferenceMarkers, Composite, etc.</summary>
    string SolidKind,
    string PicoGkLatticeNotes,
    IReadOnlyList<ProfileMeridianPoint> ProfilePoints,
    IReadOnlyList<string> Subcomponents);
