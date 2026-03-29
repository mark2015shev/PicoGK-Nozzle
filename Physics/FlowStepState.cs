namespace PicoGK_Run.Physics;

/// <summary>Per-axial-station swirl chamber march state (compressible mixed stream, 1-D SI).</summary>
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

    /// <summary>Chamber bulk total pressure after named Δp₀ losses at this station (authoritative for bulk isentropic closure).</summary>
    public double PTotalPa { get; init; }

    /// <summary>Chamber bulk total temperature from mixed h₀ (authoritative with <see cref="PTotalPa"/> for bulk closure).</summary>
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

    /// <summary>Flux swirl number S at step start (diagnostic only; entrainment scaling uses η_mix(L/D, Re)).</summary>
    public double EntrainmentSwirlCorrelation { get; init; }

    /// <summary>min(capture, mixed annulus, bore, free annulus) used for pressure-driven entrainment this step [m²].</summary>
    public double EffectiveEntrainmentEntryAreaM2 { get; init; }

    /// <summary>
    /// P_capture at step entry from radial pressure balance (core-side static vs bulk before entrainment increment) [Pa].
    /// </summary>
    public double CaptureBoundaryStaticPressureForEntrainmentPa { get; init; }

    public double AngularMomentumWallLossKgM2PerS2 { get; init; }
    public double AngularMomentumMixingLossKgM2PerS2 { get; init; }
    public double AngularMomentumEntrainmentDilutionLossKgM2PerS2 { get; init; }

    /// <summary>ΔP₀ mixing + wall + angular-momentum this step [Pa].</summary>
    public double TotalPressureLossStepPa { get; init; }

    /// <summary>False if bulk static/total ordering or finiteness checks failed for this step.</summary>
    public bool StepBulkPressureValid { get; init; } = true;

    public double CorePressurePa { get; init; }
    public double WallPressurePa { get; init; }
    public double RadialPressureDeltaPa { get; init; }

    /// <summary>Core radius used in radial shaping integral this step [m].</summary>
    public double RadialCoreRadiusUsedM { get; init; }

    public bool RadialShapingInvariantsOk { get; init; } = true;

    public string RadialShapingInvariantNote { get; init; } = "";

    /// <summary>|ρ A V_ax − ṁ_total| / max(ṁ_total, ε).</summary>
    public double ContinuityResidualRelative { get; init; }

    /// <summary>|P_isentropic(M,|V|) − P_bulk,closure| / max(P_bulk, ε) when isentropic consistency replaces bulk static.</summary>
    public double ChamberBulkPressureConsistencyResidualRelative { get; init; }

    public FlowStepUpdate StepUpdate { get; init; }
    public CompressibleState Compressible { get; init; }

    /// <summary>
    /// Quasi-steady upstream vs downstream escape split at this bulk state (finite control-volume narrative).
    /// Null when dual-path discharge is disabled for the march.
    /// </summary>
    public SwirlChamberDualPathDischargeResult? DualPathDischarge { get; init; }
}
