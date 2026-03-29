using System.Collections.Generic;

namespace PicoGK_Run.Physics.Continuous;

/// <summary>Full continuous station list + energy summary. Chamber rows mirror the SI march; expander is integrated downstream without resetting state.</summary>
public sealed class ContinuousNozzleSolution
{
    public IReadOnlyList<ContinuousPathStation> Stations { get; init; } = System.Array.Empty<ContinuousPathStation>();

    public SwirlEnergyAccountingBuckets Energy { get; init; } = new();

    /// <summary>Integrated expander axial wall force from local p_wall and geometry [N].</summary>
    public double ExpanderWallForceIntegratedN { get; init; }

    /// <summary>Lumped reference from SwirlDiffuserRecoveryModel for calibration comparison [N].</summary>
    public double ExpanderWallForceLumpedReferenceN { get; init; }

    public ReducedOrderClosureCoefficients ClosuresUsed { get; init; } = ReducedOrderClosureCoefficients.Default;

    /// <summary>Audit: segment boundary state handoff (no invented defaults at expander/stator).</summary>
    public IReadOnlyList<string> SegmentBoundaryAuditLines { get; init; } = System.Array.Empty<string>();
}

public sealed record ContinuousPathStation(GeometryStation Geometry, FlowState Flow);
