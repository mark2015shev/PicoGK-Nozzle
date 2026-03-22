using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// Ideal-air helpers. All SI: Pa, K, kg/m³, m/s, N/m² for dynamic pressure.
/// </summary>
public sealed class GasProperties
{
    public const double Gamma = 1.4;
    public const double R = 287.0;

    /// <summary>cp = γ R / (γ - 1) [J/(kg·K)].</summary>
    public double SpecificHeatCp => Gamma * R / (Gamma - 1.0);

    public double Density(double pressurePa, double temperatureK)
    {
        if (temperatureK <= 1.0)
            temperatureK = 1.0;
        return pressurePa / (R * temperatureK);
    }

    public double SpeedOfSound(double temperatureK)
    {
        if (temperatureK <= 1.0)
            temperatureK = 1.0;
        return Math.Sqrt(Gamma * R * temperatureK);
    }

    /// <summary>Static temperature from stagnation and known speed (calorically perfect gas).</summary>
    public double StaticTemperatureFromTotal(double totalTemperatureK, double velocityMps)
    {
        if (totalTemperatureK <= 1.0)
            totalTemperatureK = 1.0;
        double tStatic = totalTemperatureK - (velocityMps * velocityMps) / (2.0 * SpecificHeatCp);
        return Math.Max(tStatic, 1.0);
    }

    public double DynamicPressure(double densityKgM3, double velocityMps)
    {
        return 0.5 * densityKgM3 * velocityMps * velocityMps;
    }
}
