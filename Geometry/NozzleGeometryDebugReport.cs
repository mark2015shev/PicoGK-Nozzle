namespace PicoGK_Run.Geometry;

/// <summary>Full audit of mm geometry as assembled by <see cref="NozzleGeometryBuilder"/> (no physics).</summary>
public sealed class NozzleGeometryDebugReport
{
    public double AssemblyOverlapMm { get; init; }
    public double TotalBuiltLengthMm { get; init; }
    public double NominalChamberInletPlaneXMm { get; init; }
    public double SwirlVoxelStartXMm { get; init; }
    public double InjectorReferencePlaneXMm { get; init; }
    public double ImpliedExpanderExitDiameterMm { get; init; }
    public double RequestedExitDiameterMm { get; init; }
    public IReadOnlyList<GeometrySegmentDebugInfo> Segments { get; init; } = Array.Empty<GeometrySegmentDebugInfo>();
    public IReadOnlyList<TransitionMismatchDebugInfo> Mismatches { get; init; } = Array.Empty<TransitionMismatchDebugInfo>();
    public StatorGeometryDebugInfo? Stator { get; init; }
    public IReadOnlyList<string> BuilderExplanations { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
