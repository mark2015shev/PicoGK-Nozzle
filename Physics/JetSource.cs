using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// Primary jet at nozzle / mixing inlet. Exit velocity from isentropic expansion (calorically perfect gas).
/// </summary>
public sealed class JetSource
{
    private readonly GasProperties _gas;

    public double TotalPressurePa { get; }
    public double StaticPressurePa { get; }
    public double TotalTemperatureK { get; }
    public double ExitAreaM2 { get; }
    public double PrimaryMassFlowKgS { get; }

    public JetSource(
        GasProperties gas,
        double totalPressurePa,
        double staticPressurePa,
        double totalTemperatureK,
        double exitAreaM2,
        double primaryMassFlowKgS)
    {
        _gas = gas;
        TotalPressurePa = Math.Max(totalPressurePa, 1.0);
        StaticPressurePa = Math.Clamp(staticPressurePa, 1.0, TotalPressurePa * 0.9999);
        TotalTemperatureK = Math.Max(totalTemperatureK, 1.0);
        ExitAreaM2 = Math.Max(exitAreaM2, 1e-12);
        PrimaryMassFlowKgS = Math.Max(primaryMassFlowKgS, 0.0);
    }

    /// <summary>
    /// Isentropic relation: V = sqrt(2 γ/(γ-1) R T0 (1 - (P/P0)^((γ-1)/γ))).
    /// Clamped to avoid NaN when P ≈ P0 or bad ratios.
    /// </summary>
    public double ComputeExitVelocity()
    {
        double g = GasProperties.Gamma;
        double r = GasProperties.R;
        double t0 = TotalTemperatureK;
        double pr = StaticPressurePa / TotalPressurePa;
        pr = Math.Clamp(pr, 1e-6, 1.0 - 1e-9);
        double exponent = (g - 1.0) / g;
        double inner = 1.0 - Math.Pow(pr, exponent);
        inner = Math.Max(inner, 0.0);
        double vSquared = 2.0 * (g / (g - 1.0)) * r * t0 * inner;
        return Math.Sqrt(Math.Max(vSquared, 0.0));
    }

    /// <summary>Static temperature after isentropic expansion to <see cref="StaticPressurePa"/>.</summary>
    public double ExitStaticTemperatureK()
    {
        double g = GasProperties.Gamma;
        double pr = StaticPressurePa / TotalPressurePa;
        pr = Math.Clamp(pr, 1e-6, 1.0);
        return TotalTemperatureK * Math.Pow(pr, (g - 1.0) / g);
    }

    public JetState CreateInitialState()
    {
        double v = ComputeExitVelocity();
        double tStatic = ExitStaticTemperatureK();
        double rho = _gas.Density(StaticPressurePa, tStatic);
        return new JetState(
            axialPositionM: 0.0,
            pressurePa: StaticPressurePa,
            temperatureK: tStatic,
            densityKgM3: rho,
            velocityMps: v,
            areaM2: ExitAreaM2,
            primaryMassFlowKgS: PrimaryMassFlowKgS,
            entrainedMassFlowKgS: 0.0);
    }
}
