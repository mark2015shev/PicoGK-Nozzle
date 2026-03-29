namespace PicoGK_Run.Physics.Continuous;

/// <summary>Authoritative bulk flow state at one station (1-D mixed stream + swirl, reduced-order).</summary>
public sealed class FlowState
{
    public double XMm { get; init; }
    public NozzleSegmentKind Segment { get; init; }

    public double MassFlowCoreKgS { get; init; }
    public double MassFlowEntrainedKgS { get; init; }
    public double MassFlowTotalKgS { get; init; }

    public double DensityKgM3 { get; init; }
    public double StaticPressurePa { get; init; }
    public double TotalPressurePa { get; init; }
    public double StaticTemperatureK { get; init; }
    public double TotalTemperatureK { get; init; }

    public double VAxialMps { get; init; }
    public double VTangentialMps { get; init; }
    public double Mach { get; init; }
    public double Reynolds { get; init; }

    /// <summary>Ġ_θ ≈ ṁ r_mean V_t [kg·m²/s²].</summary>
    public double AngularMomentumFluxKgM2PerS2 { get; init; }

    /// <summary>Dimensionless swirl metric (flux correlation compatible with march).</summary>
    public double SwirlMetric { get; init; }

    public double WallPressurePa { get; init; }
    public double CapturePressurePa { get; init; }
    public double DeltaPRadialPa { get; init; }

    public double AxialWallForceIncrementN { get; init; }
    public double CumulativeWallForceN { get; init; }

    public double EntrainmentIncrementKgS { get; init; }
    public double CumulativeEntrainedKgS { get; init; }

    public double CumulativeAxialMomentumFluxGainKgM2PerS2 { get; init; }

    /// <summary>Explicit closure: friction-like Δp₀ this station [Pa].</summary>
    public double FrictionLossPressurePa { get; init; }

    /// <summary>Explicit closure: mixing / separation Δp this station [Pa].</summary>
    public double MixingLossPressurePa { get; init; }

    public double SwirlEnergyPreservedBucketW { get; init; }
    public double SwirlToAxialBucketW { get; init; }
    public double SwirlToWallForceBucketW { get; init; }
}
