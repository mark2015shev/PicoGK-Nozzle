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
    /// <summary>Requested entrainment increment this step (correlation) [kg/s].</summary>
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
    public double PressureForceN { get; init; }
}
