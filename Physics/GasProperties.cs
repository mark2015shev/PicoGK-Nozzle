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

    public double MachNumber(double velocityMps, double temperatureK)
    {
        double a = SpeedOfSound(temperatureK);
        if (a < 1e-6)
            return 0.0;
        return Math.Abs(velocityMps) / a;
    }

    /// <summary>Critical pressure ratio P*/P0 for isentropic choking.</summary>
    public double CriticalPressureRatio()
    {
        double g = Gamma;
        return Math.Pow(2.0 / (g + 1.0), g / (g - 1.0));
    }

    /// <summary>Choked mass flow per unit area [kg/(s·m²)] at M = 1 from (P0, T0).</summary>
    public double ChokedMassFlux(double totalPressurePa, double totalTemperatureK)
    {
        return CompressibleFlowMath.ChokedMassFluxPerArea(
            Math.Max(totalPressurePa, 1.0),
            Math.Max(totalTemperatureK, 1.0),
            Gamma,
            R);
    }

    public double StaticPressureFromTotalAndMach(double totalPressurePa, double mach)
    {
        double m = Math.Clamp(Math.Abs(mach), 0.0, 50.0);
        double ratio = CompressibleFlowMath.StaticPressureRatioFromMach(m, Gamma);
        return Math.Max(totalPressurePa * ratio, 1.0);
    }

    public double StaticTemperatureFromTotalAndMach(double totalTemperatureK, double mach)
    {
        double m = Math.Clamp(Math.Abs(mach), 0.0, 50.0);
        double ratio = CompressibleFlowMath.StaticTemperatureRatioFromMach(m, Gamma);
        return Math.Max(totalTemperatureK * ratio, 1.0);
    }
}
