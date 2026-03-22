using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// One-dimensional flow state at an axial station (SI only).
/// </summary>
public sealed class JetState
{
    public double AxialPositionM { get; }
    public double PressurePa { get; }
    public double TemperatureK { get; }
    public double DensityKgM3 { get; }
    public double VelocityMps { get; }
    public double AreaM2 { get; }

    /// <summary>Primary (turbine / core) mass flow rate [kg/s].</summary>
    public double MassFlowKgS { get; }

    /// <summary>Accumulated entrained secondary mass flow [kg/s].</summary>
    public double EntrainedMassFlowKgS { get; }

    public double TotalMassFlowKgS { get; }
    public double MomentumFluxN { get; }

    public JetState(
        double axialPositionM,
        double pressurePa,
        double temperatureK,
        double densityKgM3,
        double velocityMps,
        double areaM2,
        double primaryMassFlowKgS,
        double entrainedMassFlowKgS)
    {
        AxialPositionM = axialPositionM;
        PressurePa = pressurePa;
        TemperatureK = Math.Max(temperatureK, 1.0);
        DensityKgM3 = Math.Max(densityKgM3, 1e-9);
        VelocityMps = velocityMps;
        AreaM2 = Math.Max(areaM2, 1e-12);
        MassFlowKgS = Math.Max(primaryMassFlowKgS, 0.0);
        EntrainedMassFlowKgS = Math.Max(entrainedMassFlowKgS, 0.0);
        TotalMassFlowKgS = MassFlowKgS + EntrainedMassFlowKgS;
        MomentumFluxN = TotalMassFlowKgS * VelocityMps;
    }
}
