namespace PicoGK_Run.Geometry;

/// <summary>Actual-built audit aligned with <see cref="NozzleGeometryBuilder"/> + viewer groups.</summary>
public sealed class NozzleGeometryAssemblyAudit
{
    public GeometryAssemblyPath Path { get; init; } = null!;
    public IReadOnlyList<BuiltGeometrySolidAuditEntry> Solids { get; init; } = Array.Empty<BuiltGeometrySolidAuditEntry>();
    public IReadOnlyList<string> ConsistencyWarnings { get; init; } = Array.Empty<string>();
}
