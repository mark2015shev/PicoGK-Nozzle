namespace PicoGK_Run.Physics;

/// <summary>
/// End-to-end trace of <c>SwirlChamberDiameterMm</c> for one pipeline run (first-order transparency; not CFD).
/// </summary>
public sealed class ChamberDiameterAudit
{
    /// <summary>Hand template or autotune winning seed as seen at <see cref="NozzleInput.Design"/>.</summary>
    public double InputDesignMm { get; init; }

    /// <summary>After optional synthesis (same as input when <c>UsePhysicsInformedGeometry</c> is false).</summary>
    public double AfterSynthesisMm { get; init; }

    /// <summary>Design into SI march (same as after synthesis here).</summary>
    public double PreSiSolveMm { get; init; }

    /// <summary>After <see cref="Geometry.FlowDrivenNozzleBuilder"/> (usually unchanged).</summary>
    public double PostFlowDrivenMm { get; init; }

    /// <summary>Bore used by voxel builders (= post flow-driven).</summary>
    public double UsedForVoxelBuildMm { get; init; }

    /// <summary>Human-readable primary label for this run configuration.</summary>
    public string DeclaredPrimarySource { get; init; } = "";

    /// <summary>True if autotune search had derived bore sizing; final run may still skip synthesis.</summary>
    public bool AutotuneSearchUsedDerivedBoreSizing { get; init; }

    /// <summary>True if autotune applied a non-unity chamber-D scale (only when override allowed).</summary>
    public bool AutotuneAppliedDirectChamberScale { get; init; }

    public string? Footnote { get; init; }

    /// <summary>
    /// Bore from <see cref="SwirlChamberSizingModel.ComputeDerived"/> at <see cref="Parameters.RunConfiguration.GeometrySynthesisTargetEntrainmentRatio"/>
    /// using the **current** <see cref="Parameters.NozzleDesignInputs"/> (hub, A_inj). Null if derived sizing disabled.
    /// </summary>
    public double? ReferenceDerivedBoreAtConfiguredTargetErMm { get; init; }
}
