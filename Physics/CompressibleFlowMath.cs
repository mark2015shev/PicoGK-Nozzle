using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// Isentropic perfect-gas relations for air (γ, R from <see cref="GasProperties"/>).
/// Side-effect free; SI units only.
/// </summary>
public static class CompressibleFlowMath
{
    public static double StaticTemperatureRatioFromMach(double mach, double gamma)
    {
        double m2 = mach * mach;
        return 1.0 / (1.0 + (gamma - 1.0) / 2.0 * m2);
    }

    public static double StaticPressureRatioFromMach(double mach, double gamma)
    {
        double m2 = mach * mach;
        double inner = 1.0 + (gamma - 1.0) / 2.0 * m2;
        return Math.Pow(inner, -gamma / (gamma - 1.0));
    }

    /// <summary>
    /// Mass flux per unit area ṁ/A [kg/(s·m²)] for isentropic nozzle flow from stagnation (P0,T0).
    /// ṁ/A = P0/√T0 · √(γ/R) · M · (1 + (γ-1)/2 M²)^(-(γ+1)/(2(γ-1))).
    /// </summary>
    public static double MassFluxPerAreaFromMach(double totalPressurePa, double totalTemperatureK, double mach, double gamma, double rGas)
    {
        if (totalPressurePa <= 0 || totalTemperatureK <= 1.0 || mach < 0)
            return 0.0;
        double m = Math.Min(mach, 50.0);
        double exponent = -(gamma + 1.0) / (2.0 * (gamma - 1.0));
        double bracket = 1.0 + (gamma - 1.0) / 2.0 * m * m;
        double factor = m * Math.Pow(bracket, exponent);
        return totalPressurePa / Math.Sqrt(totalTemperatureK) * Math.Sqrt(gamma / rGas) * factor;
    }

    /// <summary>Choked mass flux per area at M = 1.</summary>
    public static double ChokedMassFluxPerArea(double totalPressurePa, double totalTemperatureK, double gamma, double rGas)
    {
        return MassFluxPerAreaFromMach(totalPressurePa, totalTemperatureK, 1.0, gamma, rGas);
    }

    /// <summary>Whether isentropic flow to static P implies M = 1 or higher at minimum area (reference only).</summary>
    public static bool IsPressureBelowCritical(double staticPressurePa, double totalPressurePa, double criticalPressureRatio)
    {
        if (totalPressurePa <= 0)
            return false;
        double pr = staticPressurePa / totalPressurePa;
        return pr <= criticalPressureRatio * 1.0001;
    }

    /// <summary>
    /// Subsonic mass flow through area A from large reservoir (P0,T0) to static P.
    /// Uses isentropic T(P), then V = √(2 cp (T0-T)), capped at local sonic speed.
    /// </summary>
    public static double MassFlowFromStagnationToStaticPressure(
        GasProperties gas,
        double totalPressurePa,
        double totalTemperatureK,
        double staticPressurePa,
        double areaM2)
    {
        double g = GasProperties.Gamma;
        if (areaM2 <= 0 || totalPressurePa <= 0 || totalTemperatureK <= 1.0)
            return 0.0;
        double p = Math.Clamp(staticPressurePa, 1.0, totalPressurePa * 0.99999);
        double t = totalTemperatureK * Math.Pow(p / totalPressurePa, (g - 1.0) / g);
        t = Math.Max(t, 1.0);
        double cp = gas.SpecificHeatCp;
        double vIdeal = Math.Sqrt(Math.Max(0.0, 2.0 * cp * (totalTemperatureK - t)));
        double a = gas.SpeedOfSound(t);
        double v = Math.Min(vIdeal, a * 0.999);
        double rho = gas.Density(p, t);
        return rho * v * areaM2;
    }

    /// <summary>T₀ = T + V²/(2 cₚ).</summary>
    public static double TotalTemperatureFromStatic(double staticTemperatureK, double velocityMps, double cp)
    {
        double t = Math.Max(staticTemperatureK, 1.0);
        return t + velocityMps * velocityMps / (2.0 * Math.Max(cp, 1e-6));
    }

    /// <summary>P₀/P from Mach (isentropic).</summary>
    public static double TotalPressureRatioFromMach(double mach, double gamma)
    {
        double m2 = mach * mach;
        double inner = 1.0 + (gamma - 1.0) / 2.0 * m2;
        return Math.Pow(inner, gamma / (gamma - 1.0));
    }

    /// <summary>Subsonic M from T/T₀: M = √[2/(γ−1)·(T₀/T − 1)].</summary>
    public static double MachFromStaticTotalTemperatureRatio(double tStaticOverTotal, double gamma)
    {
        double r = Math.Clamp(tStaticOverTotal, 1e-6, 1.0);
        double inner = 2.0 / (gamma - 1.0) * (1.0 / r - 1.0);
        return inner > 0 ? Math.Sqrt(inner) : 0.0;
    }

    /// <summary>
    /// <b>Authoritative bulk chamber closure</b> (one state): Vmag magnitude, T₀ and P₀ after losses at the station.
    /// T_s = T₀ − Vmag²/(2cₚ), M = Vmag/a(T_s), P_s = P₀·(1 + (γ−1)/2 M²)^(-γ/(γ−1)) — equivalent to P_s = P₀(T_s/T₀)^(γ/(γ−1)).
    /// </summary>
    public static (double StaticPressurePa, double StaticTemperatureK, double MachNumber, double DensityKgM3)
        BulkChamberThermoFromStagnationAndSpeedMagnitude(
        GasProperties gas,
        double totalPressurePa,
        double totalTemperatureK,
        double velocityMagnitudeMps)
    {
        double g = GasProperties.Gamma;
        double cp = gas.SpecificHeatCp;
        double p0 = Math.Max(totalPressurePa, 1.0);
        double t0 = Math.Max(totalTemperatureK, 1.0);
        double v = Math.Max(0.0, velocityMagnitudeMps);
        double ts = t0 - v * v / (2.0 * Math.Max(cp, 1e-6));
        if (ts < 1.0)
            ts = 1.0;
        double a = gas.SpeedOfSound(ts);
        double mach = v / Math.Max(a, 1e-30);
        double ps = p0 * StaticPressureRatioFromMach(mach, g);
        ps = Math.Max(ps, 1.0);
        double rho = gas.Density(ps, ts);
        return (ps, ts, mach, rho);
    }

    /// <summary>
    /// Ideal calorically perfect gas, adiabatic: T = T₀ − V²/(2 cₚ), then isentropic P = P₀ (T/T₀)^(γ/(γ−1)).
    /// Use when P₀ is the total pressure referenced at the same T₀ (e.g. after applying a modeled Δp₀ loss as a decrement to P₀ only).
    /// Prefer <see cref="BulkChamberThermoFromStagnationAndSpeedMagnitude"/> for explicit M-based bulk chamber documentation.
    /// Does not resolve boundary layers, mixing, or real-gas effects.
    /// </summary>
    public static (double StaticPressurePa, double StaticTemperatureK) StaticPressureTemperatureFromTotalStagnationAndSpeed(
        GasProperties gas,
        double totalPressurePa,
        double totalTemperatureK,
        double velocityMagnitudeMps)
    {
        double g = GasProperties.Gamma;
        double cp = gas.SpecificHeatCp;
        double p0 = Math.Max(totalPressurePa, 1.0);
        double t0 = Math.Max(totalTemperatureK, 1.0);
        double v = Math.Max(0.0, velocityMagnitudeMps);
        double t = t0 - v * v / (2.0 * Math.Max(cp, 1e-6));
        if (t < 1.0)
            t = 1.0;
        double p = p0 * Math.Pow(t / t0, g / (g - 1.0));
        p = Math.Max(p, 1.0);
        return (p, t);
    }
}
