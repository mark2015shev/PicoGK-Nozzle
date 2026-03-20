namespace PicoGK_Run.Core;

/// <summary>
/// Source boundary + ambient freestream for a nozzle / ejector analysis.
/// The engine is not modeled as geometry—only these scalars drive the solver.
/// <see cref="SourceOutletAreaMm2"/> is the authoritative source flow area.
/// </summary>
public sealed class SourceInputs
{
    public double SourceOutletAreaMm2 { get; }
    public double MassFlowKgPerSec { get; }
    public double SourceVelocityMps { get; }
    public double PressureRatio { get; }

    /// <summary>
    /// Optional exhaust static/stagnation-scale temperature (K) used only for a
    /// first-order core density estimate in continuity (see solver comments).
    /// </summary>
    public double? ExhaustTemperatureK { get; }

    public double AmbientPressurePa { get; }
    public double AmbientTemperatureK { get; }
    public double AmbientDensityKgPerM3 { get; }

    public SourceInputs(
        double sourceOutletAreaMm2,
        double massFlowKgPerSec,
        double sourceVelocityMps,
        double pressureRatio,
        double ambientPressurePa,
        double ambientTemperatureK,
        double ambientDensityKgPerM3,
        double? exhaustTemperatureK = null)
    {
        SourceOutletAreaMm2 = sourceOutletAreaMm2;
        MassFlowKgPerSec = massFlowKgPerSec;
        SourceVelocityMps = sourceVelocityMps;
        PressureRatio = pressureRatio;
        AmbientPressurePa = ambientPressurePa;
        AmbientTemperatureK = ambientTemperatureK;
        AmbientDensityKgPerM3 = ambientDensityKgPerM3;
        ExhaustTemperatureK = exhaustTemperatureK;
    }
}
