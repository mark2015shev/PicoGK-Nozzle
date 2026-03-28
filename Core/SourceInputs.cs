namespace PicoGK_Run.Core;

/// <summary>
/// Source boundary + ambient freestream for a nozzle / ejector analysis.
/// The engine is not modeled as geometry—only these scalars drive the solver.
/// <para>
/// <b>Live SI authority (frozen contract):</b> mass flow, source outlet area, and velocity magnitude define discharge via
/// ρ = ṁ/(A·|V|), static temperature from exhaust temperature (total vs static per flags), P_static = ρ R T_static, and derived P₀
/// for diagnostics only. <see cref="PressureRatio"/> is never used for live march, thrust, or entrainment.
/// </para>
/// <see cref="SourceOutletAreaMm2"/> is the authoritative source flow area.
/// </summary>
public sealed class SourceInputs
{
    public double SourceOutletAreaMm2 { get; }
    public double MassFlowKgPerSec { get; }
    public double SourceVelocityMps { get; }

    /// <summary>
    /// <b>Deprecated legacy field</b> — not read for live SI physics. Use <see cref="double.NaN"/> when unused.
    /// If finite and &gt; 0, an optional informational block may compare P_amb·PR to derived P₀ (does not affect ṁ, P_static, or march).
    /// </summary>
    public double PressureRatio { get; }

    /// <summary>Maps <see cref="ExhaustTemperatureIsTotalK"/> to a named mode for reporting.</summary>
    public SourceTemperatureInterpretation TemperatureInterpretation =>
        ExhaustTemperatureIsTotalK ? SourceTemperatureInterpretation.Total : SourceTemperatureInterpretation.Static;

    /// <summary>True when a numeric legacy pressure ratio was supplied (finite, &gt; 0).</summary>
    public bool HasLegacyPressureRatio =>
        !double.IsNaN(PressureRatio) && !double.IsInfinity(PressureRatio) && PressureRatio > 0.0;

    /// <summary>Exhaust temperature (K); static vs total per <see cref="ExhaustTemperatureIsTotalK"/>.</summary>
    public double? ExhaustTemperatureK { get; }

    /// <summary>When true, <see cref="ExhaustTemperatureK"/> is stagnation temperature; when false, static at source exit.</summary>
    public bool ExhaustTemperatureIsTotalK { get; }

    public double AmbientPressurePa { get; }

    /// <summary>ISA / test-day ambient temperature. Reported and available for future state equations; <b>not used</b> in current thrust formulas.</summary>
    public double AmbientTemperatureK { get; }

    public double AmbientDensityKgPerM3 { get; }

    public SourceInputs(
        double sourceOutletAreaMm2,
        double massFlowKgPerSec,
        double sourceVelocityMps,
        double ambientPressurePa,
        double ambientTemperatureK,
        double ambientDensityKgPerM3,
        double? exhaustTemperatureK = null,
        bool exhaustTemperatureIsTotalK = true,
        double legacyPressureRatio = double.NaN)
    {
        SourceOutletAreaMm2 = sourceOutletAreaMm2;
        MassFlowKgPerSec = massFlowKgPerSec;
        SourceVelocityMps = sourceVelocityMps;
        PressureRatio = legacyPressureRatio;
        AmbientPressurePa = ambientPressurePa;
        AmbientTemperatureK = ambientTemperatureK;
        AmbientDensityKgPerM3 = ambientDensityKgPerM3;
        ExhaustTemperatureK = exhaustTemperatureK;
        ExhaustTemperatureIsTotalK = exhaustTemperatureIsTotalK;
    }
}
