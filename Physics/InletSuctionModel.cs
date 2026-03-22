using System;

namespace PicoGK_Run.Physics;

/// <summary>Result of compressible intake / suction solve for one entrainment increment (first-order, not CFD).</summary>
public sealed class InletSuctionOutcome
{
    public double PinletLocalPa { get; init; }
    public double EntrainmentVelocityMps { get; init; }
    public bool IsChoked { get; init; }
    public double Mach { get; init; }
    public double MaxSupportedEntrainedMassFlowKgS { get; init; }
    public double ActualEntrainedMassFlowKgS { get; init; }
}

/// <summary>
/// Estimates local intake static pressure and entrained velocity for a demanded entrainment mass-flow increment.
/// First-order compressible intake with sonic cap and choked limit — not CFD.
/// </summary>
public sealed class InletSuctionModel
{
    private const int MaxBisectionIterations = 60;

    public InletSuctionOutcome Solve(
        GasProperties gas,
        AmbientAir ambient,
        double demandedEntrainedMassFlowRateKgS,
        double effectiveCaptureAreaM2,
        double mixedStaticTemperatureForSoundSpeedK)
    {
        double p0 = Math.Max(ambient.PressurePa, 1.0);
        double t0 = Math.Max(ambient.TemperatureK, 1.0);
        double aCapture = Math.Max(effectiveCaptureAreaM2, 1e-15);
        double demand = Math.Max(demandedEntrainedMassFlowRateKgS, 0.0);

        double mdotMax = gas.ChokedMassFlux(p0, t0) * aCapture;
        mdotMax = Math.Max(mdotMax, 0.0);

        if (demand <= 1e-18)
        {
            return new InletSuctionOutcome
            {
                PinletLocalPa = p0,
                EntrainmentVelocityMps = 0.0,
                IsChoked = false,
                Mach = 0.0,
                MaxSupportedEntrainedMassFlowKgS = mdotMax,
                ActualEntrainedMassFlowKgS = 0.0
            };
        }

        if (demand >= mdotMax * (1.0 - 1e-9))
        {
            double prStar = gas.CriticalPressureRatio();
            double pStar = Math.Max(p0 * prStar, 1.0);
            double tStar = gas.StaticTemperatureFromTotalAndMach(t0, 1.0);
            double vStar = gas.SpeedOfSound(tStar) * 0.999;
            return new InletSuctionOutcome
            {
                PinletLocalPa = pStar,
                EntrainmentVelocityMps = vStar,
                IsChoked = true,
                Mach = 0.999,
                MaxSupportedEntrainedMassFlowKgS = mdotMax,
                ActualEntrainedMassFlowKgS = mdotMax
            };
        }

        double pLow = 50.0;
        double pHigh = p0 * 0.9999;
        double pMid = pHigh;
        for (int i = 0; i < MaxBisectionIterations; i++)
        {
            pMid = 0.5 * (pLow + pHigh);
            double mdotTry = CompressibleFlowMath.MassFlowFromStagnationToStaticPressure(
                gas, p0, t0, pMid, aCapture);
            if (mdotTry < demand)
                pHigh = pMid;
            else
                pLow = pMid;
        }

        double pSolve = Math.Clamp(pMid, 1.0, p0);
        double g = GasProperties.Gamma;
        double tStatic = t0 * Math.Pow(pSolve / p0, (g - 1.0) / g);
        tStatic = Math.Max(tStatic, 1.0);
        double cp = gas.SpecificHeatCp;
        double vIdeal = Math.Sqrt(Math.Max(0.0, 2.0 * cp * (t0 - tStatic)));
        double aLocal = gas.SpeedOfSound(tStatic);
        double vEnt = Math.Min(vIdeal, aLocal * 0.999);
        double mach = gas.MachNumber(vEnt, tStatic);

        double mdotActual = CompressibleFlowMath.MassFlowFromStagnationToStaticPressure(
            gas, p0, t0, pSolve, aCapture);
        mdotActual = Math.Min(mdotActual, demand);
        mdotActual = Math.Min(mdotActual, mdotMax);

        return new InletSuctionOutcome
        {
            PinletLocalPa = pSolve,
            EntrainmentVelocityMps = vEnt,
            IsChoked = false,
            Mach = mach,
            MaxSupportedEntrainedMassFlowKgS = mdotMax,
            ActualEntrainedMassFlowKgS = mdotActual
        };
    }
}
