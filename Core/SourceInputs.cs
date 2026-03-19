namespace PicoGK_Run.Core;

/// <summary>
/// Source boundary condition for the nozzle solver.
/// SourceOutletAreaMm2 is the authoritative source-size constraint.
/// SourceOutletDiameterMm is optional and only used as a helper/reference.
/// </summary>
public sealed class SourceInputs
{
    public double SourceOutletAreaMm2 { get; }
    public double? SourceOutletDiameterMm { get; }
    public double MassFlowKgPerSec { get; }
    public double SourceVelocityMps { get; }
    public double PressureRatio { get; }
    public double ExhaustTemperatureK { get; }

    public SourceInputs(
        double sourceOutletAreaMm2,
        double massFlowKgPerSec,
        double sourceVelocityMps,
        double pressureRatio,
        double exhaustTemperatureK,
        double? sourceOutletDiameterMm = null)
    {
        SourceOutletAreaMm2 = sourceOutletAreaMm2;
        SourceOutletDiameterMm = sourceOutletDiameterMm;
        MassFlowKgPerSec = massFlowKgPerSec;
        SourceVelocityMps = sourceVelocityMps;
        PressureRatio = pressureRatio;
        ExhaustTemperatureK = exhaustTemperatureK;
    }
}
