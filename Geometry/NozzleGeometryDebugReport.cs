namespace PicoGK_Run.Geometry;

/// <summary>Full audit of mm geometry as assembled by <see cref="NozzleGeometryBuilder"/> (no physics).</summary>
public sealed class NozzleGeometryDebugReport
{
    public double AssemblyOverlapMm { get; init; }
    public double TotalBuiltLengthMm { get; init; }
    public double NominalChamberInletPlaneXMm { get; init; }
    public double SwirlVoxelStartXMm { get; init; }
    public double SwirlChamberEndXMm { get; init; }
    public double SwirlChamberPhysicalLengthRequestedMm { get; init; }
    public double SwirlChamberPhysicalLengthBuiltMm { get; init; }
    public double InjectorUpstreamGuardLengthMm { get; init; }
    public double RequestedInjectorAxialRatio { get; init; }
    public double ClampedInjectorAxialRatio { get; init; }
    public double InjectorDistanceFromChamberUpstreamFaceMm { get; init; }
    public double InjectorDistanceFromChamberDownstreamFaceMm { get; init; }
    public double ChamberUpstreamOvershootMm { get; init; }
    public string SwirlChamberPlacementStatusLabel { get; init; } = "PASS";
    public double InjectorReferencePlaneXMm { get; init; }
    public double ImpliedExpanderExitDiameterMm { get; init; }
    public double RequestedExitDiameterMm { get; init; }

    /// <summary>Built recovery annulus inner Ø (2·R) — single downstream target for expander/stator/exit start.</summary>
    public double SolvedDownstreamTargetInnerDiameterMm { get; init; }

    public double ExpanderBuiltOutletInnerDiameterMm { get; init; }
    public double StatorCasingInnerDiameterMm { get; init; }
    public double ExitStartInnerDiameterMm { get; init; }
    public double ExitEndInnerDiameterMm { get; init; }
    public double DownstreamContinuityMaxRadialErrorMm { get; init; }
    public string DownstreamContinuityLabel { get; init; } = "PASS";
    public string DownstreamExitModeLabel { get; init; } = "";
    public IReadOnlyList<GeometrySegmentDebugInfo> Segments { get; init; } = Array.Empty<GeometrySegmentDebugInfo>();
    public IReadOnlyList<TransitionMismatchDebugInfo> Mismatches { get; init; } = Array.Empty<TransitionMismatchDebugInfo>();
    public StatorGeometryDebugInfo? Stator { get; init; }
    public IReadOnlyList<string> BuilderExplanations { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>Generators, viewer groups, meridian profiles — single path from <see cref="GeometryAssemblyPath"/>.</summary>
    public NozzleGeometryAssemblyAudit? AssemblyAudit { get; init; }
}
