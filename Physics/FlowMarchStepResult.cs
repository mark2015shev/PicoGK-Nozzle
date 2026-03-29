namespace PicoGK_Run.Physics;

/// <summary>Per-axial-step SI march diagnostics (compressible entrainment path).</summary>
public sealed class FlowMarchStepResult
{
    public double AxialPositionM { get; init; }
    public double AreaM2 { get; init; }
    public double PerimeterM { get; init; }
    public double MixedStaticPressurePa { get; init; }
    public double MixedTemperatureK { get; init; }
    public double MixedDensityKgM3 { get; init; }
    public double MixedVelocityMps { get; init; }
    public double PrimaryMassFlowKgS { get; init; }
    public double EntrainedMassFlowKgS { get; init; }
    /// <summary>Requested entrainment increment this step (pressure-driven demand) [kg/s].</summary>
    public double RequestedDeltaEntrainedMassFlowKgS { get; init; }
    /// <summary>Actual entrainment increment after compressible intake limit [kg/s].</summary>
    public double DeltaEntrainedMassFlowKgS { get; init; }
    public double InletLocalPressurePa { get; init; }
    public double InletEntrainmentVelocityMps { get; init; }
    public double InletMach { get; init; }
    public bool InletIsChoked { get; init; }
    public double TangentialVelocityMps { get; init; }
    public double AxialVelocityMps { get; init; }
    public double SwirlKineticEnergyPerKg { get; init; }
    public double RecoveredPressureRisePa { get; init; }
    /// <summary>diagnostic_force_only: inlet capture annulus (P_amb−P_local)×A this step — not summed into net thrust.</summary>
    public double PressureForceN { get; init; }

    /// <summary>Effective duct flow area (annulus minus hub/vane blockage) [m²].</summary>
    public double DuctEffectiveAreaM2 { get; init; }

    /// <summary>Compressible intake capture area (constant along chamber march) [m²].</summary>
    public double CaptureAreaM2 { get; init; }

    /// <summary>Lumped axial mixing effectiveness η_mix used this step (L/D, Re; not a Ce coefficient).</summary>
    public double EntrainmentMixingEffectivenessUsed { get; init; }

    /// <summary>Same as <see cref="FlowStepState.CaptureBoundaryStaticPressureForEntrainmentPa"/> for step CSV export.</summary>
    public double CaptureBoundaryStaticPressureForEntrainmentPa { get; init; }
}
