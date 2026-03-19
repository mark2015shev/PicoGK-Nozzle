namespace PicoGK_Run.Core;

/// <summary>
/// Represents the K320 exhaust as a flow source (not geometry).
/// All values are assumed to be time-averaged steady-state.
/// </summary>
public sealed class JetStreamK320
{
    public double OutletDiameterMM { get; }
    public double MassFlowKgPerSec { get; }
    public double ExhaustVelocityMps { get; }
    public double PressureRatio { get; }
    public double ExhaustTemperatureK { get; }

    public JetStreamK320(
        double outletDiameterMM,
        double massFlowKgPerSec,
        double exhaustVelocityMps,
        double pressureRatio,
        double exhaustTemperatureK)
    {
        OutletDiameterMM = outletDiameterMM;
        MassFlowKgPerSec = massFlowKgPerSec;
        ExhaustVelocityMps = exhaustVelocityMps;
        PressureRatio = pressureRatio;
        ExhaustTemperatureK = exhaustTemperatureK;
    }
}

