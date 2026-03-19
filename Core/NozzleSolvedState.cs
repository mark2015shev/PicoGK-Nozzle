namespace PicoGK_Run.Core;

/// <summary>
/// Derived quantities computed by the physics solver.
/// </summary>
public sealed class NozzleSolvedState
{
    public double TotalInjectorAreaMM2 { get; init; }
    public double ChamberAreaMM2 { get; init; }
    public double ExitAreaMM2 { get; init; }
    public double CoreVelocityMps { get; init; }
    public double SwirlStrength { get; init; }
    public double PressureLoss { get; init; }
    public double EstimatedThrustGain { get; init; }
}

