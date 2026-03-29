using System.Numerics;

namespace PicoGK_Run.Physics.JetTrajectory;

/// <summary>
/// One step along a traced jet path: position, direction, local scalars, and interaction flags for diagnostics.
/// </summary>
public sealed class JetTrajectorySample
{
    public int StepIndex { get; init; }
    public Vector3 PositionMm { get; init; }
    /// <summary>Unit direction of travel after the step update.</summary>
    public Vector3 DirectionUnit { get; init; }
    public double SpeedMps { get; init; }
    public double StaticPressurePa { get; init; }
    /// <summary>Approximate envelope radius in the cross-plane [mm] when expansion tracking is on.</summary>
    public double EnvelopeRadiusMm { get; init; }
    public bool HadWallInteraction { get; init; }
    public bool HadJetInteraction { get; init; }
}
