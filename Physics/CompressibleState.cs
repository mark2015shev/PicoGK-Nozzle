using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// Bulk mixed state: h₀ = cₚ T + V²/2, T₀ = T + V²/(2cₚ), P₀/P = (T₀/T)^{γ/(γ−1)} (isentropic stagnation for |V|).
/// </summary>
public readonly record struct CompressibleState(
    double StaticPressurePa,
    double StaticTemperatureK,
    double DensityKgM3,
    double AxialVelocityMps,
    double TangentialVelocityMps,
    double MagnitudeVelocityMps,
    double SpeedOfSoundMps,
    double MachNumber,
    double TotalTemperatureK,
    double TotalPressurePa,
    double TotalEnthalpyJPerKg)
{
    public static CompressibleState FromMixedStatic(
        GasProperties gas,
        double staticPressurePa,
        double staticTemperatureK,
        double axialVelocityMps,
        double tangentialVelocityMps)
    {
        double p = Math.Max(staticPressurePa, 1.0);
        double t = Math.Max(staticTemperatureK, 1.0);
        double va = axialVelocityMps;
        double vt = tangentialVelocityMps;
        double vmag = Math.Sqrt(va * va + vt * vt);
        double rho = gas.Density(p, t);
        double a = gas.SpeedOfSound(t);
        double m = vmag / Math.Max(a, 1e-9);
        double cp = gas.SpecificHeatCp;
        double t0 = t + vmag * vmag / (2.0 * cp);
        double g = GasProperties.Gamma;
        double p0 = p * Math.Pow(t0 / t, g / (g - 1.0));
        double h0 = cp * t0;
        return new CompressibleState(
            p,
            t,
            rho,
            va,
            vt,
            vmag,
            a,
            m,
            t0,
            Math.Max(p0, 1.0),
            h0);
    }

    /// <summary>
    /// Bulk chamber station: statics from authoritative P₀, T₀ (after modeled losses) and (V_a, V_t) with |V| = √(V_a² + V_t²).
    /// Total pressure on the returned state matches <paramref name="totalPressurePaAfterLosses"/> (not recomputed from static).
    /// </summary>
    public static CompressibleState FromAuthoritativeBulkStagnation(
        GasProperties gas,
        double totalPressurePaAfterLosses,
        double totalTemperatureK,
        double axialVelocityMps,
        double tangentialVelocityMps)
    {
        double va = axialVelocityMps;
        double vt = tangentialVelocityMps;
        double vmag = Math.Sqrt(va * va + vt * vt);
        var (p, t, mach, rho) = CompressibleFlowMath.BulkChamberThermoFromStagnationAndSpeedMagnitude(
            gas,
            totalPressurePaAfterLosses,
            totalTemperatureK,
            vmag);
        double a = gas.SpeedOfSound(t);
        double cp = gas.SpecificHeatCp;
        double t0 = Math.Max(totalTemperatureK, 1.0);
        double p0 = Math.Max(totalPressurePaAfterLosses, 1.0);
        double h0 = cp * t0;
        return new CompressibleState(
            p,
            t,
            rho,
            va,
            vt,
            vmag,
            a,
            mach,
            t0,
            p0,
            h0);
    }
}
