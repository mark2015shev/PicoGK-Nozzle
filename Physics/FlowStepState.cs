namespace PicoGK_Run.Physics;

/// <summary>Per-axial-station SI mixed-stream state (first-order 1-D march).</summary>
public sealed class FlowStepState
{
    public double X { get; init; }
    public double AreaM2 { get; init; }
    public double CaptureAreaM2 { get; init; }
    public double WettedPerimeterM { get; init; }

    public double MdotPrimaryKgS { get; init; }
    public double MdotSecondaryKgS { get; init; }
    public double MdotTotalKgS { get; init; }

    public double PStaticPa { get; init; }
    public double TStaticK { get; init; }
    public double DensityKgM3 { get; init; }
    public double PTotalPa { get; init; }
    public double TTotalK { get; init; }

    public double VAxialMps { get; init; }
    public double VTangentialMps { get; init; }
    public double VMagnitudeMps { get; init; }
    public double Mach { get; init; }
    public double Reynolds { get; init; }

    public double SwirlNumberFlux { get; init; }
    public double AngularMomentumFluxKgM2PerS2 { get; init; }
    public double AxialMomentumFluxKgM2PerS2 { get; init; }

    /// <summary>P₀ after mass mixing and named Δp₀ losses, before deriving bulk static (Pa).</summary>
    public double TotalPressureAfterLossesPa { get; init; }

    /// <summary>|Vt|/max(|Va|, <see cref="ChamberAerodynamicsConfiguration.VaFloorForBulkSwirlMps"/>).</summary>
    public double ChamberSwirlBulkRatio { get; init; }

    /// <summary>Swirl metric passed to entrainment Ce correlation this step (bounded).</summary>
    public double EntrainmentSwirlCorrelation { get; init; }

    /// <summary>False if bulk static/total ordering or finiteness checks failed for this step.</summary>
    public bool StepBulkPressureValid { get; init; } = true;

    public double CorePressurePa { get; init; }
    public double WallPressurePa { get; init; }
    public double RadialPressureDeltaPa { get; init; }

    /// <summary>|ρ A V_ax − ṁ_total| / max(ṁ_total, ε).</summary>
    public double ContinuityResidualRelative { get; init; }

    public FlowStepUpdate StepUpdate { get; init; }
    public CompressibleState Compressible { get; init; }
}
