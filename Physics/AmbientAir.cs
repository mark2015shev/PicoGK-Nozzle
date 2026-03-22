using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// Freestream / secondary air boundary. Density from ideal gas at (P, T).
/// </summary>
public sealed class AmbientAir
{
    public double PressurePa { get; }
    public double TemperatureK { get; }
    public double DensityKgM3 { get; }
    public double VelocityMps { get; }

    public AmbientAir(GasProperties gas, double pressurePa, double temperatureK, double velocityMps = 0.0)
    {
        PressurePa = pressurePa;
        TemperatureK = Math.Max(temperatureK, 1.0);
        VelocityMps = velocityMps;
        DensityKgM3 = gas.Density(pressurePa, TemperatureK);
    }
}
