using System.Numerics;

namespace PicoGK_Run.Physics.JetTrajectory;

/// <summary>
/// Physical state at an injector exit port: position on the casing, velocity vector, and thermo scalars
/// used to seed the reduced-order trajectory march (engineering model — not CFD).
/// </summary>
public sealed class InjectorInitialState
{
    public int InjectorIndex { get; init; }
    /// <summary>Port center [mm], PicoGK-style +X axial.</summary>
    public Vector3 PositionMm { get; init; }
    /// <summary>Velocity vector [m/s] (not necessarily unit length).</summary>
    public Vector3 VelocityMps { get; init; }
    public double SpeedMps { get; init; }
    public double StaticPressurePa { get; init; }
    public double TemperatureK { get; init; }
    public double DensityKgM3 { get; init; }
    /// <summary>This port’s share of total core mass flow [kg/s].</summary>
    public double MassFlowKgS { get; init; }
}
