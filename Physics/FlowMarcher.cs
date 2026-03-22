using System;
using System.Collections.Generic;

namespace PicoGK_Run.Physics;

/// <summary>
/// Explicit axial marching through a mixing duct. Future hooks: pressure loss, swirl, friction, diffuser recovery.
/// </summary>
public sealed class FlowMarcher
{
    private readonly AmbientAir _ambient;
    private readonly EntrainmentModel _entrainment;
    private readonly MixingSectionSolver _mixing;
    private readonly GasProperties _gas;

    public FlowMarcher(
        AmbientAir ambient,
        EntrainmentModel entrainment,
        MixingSectionSolver mixing,
        GasProperties gas)
    {
        _ambient = ambient;
        _entrainment = entrainment;
        _mixing = mixing;
        _gas = gas;
    }

    /// <summary>
    /// March from <paramref name="inletState"/> over [0, <paramref name="sectionLengthM"/>].
    /// Uses ambient pressure as static pressure for the mixed stream (first-order).
    /// </summary>
    public IReadOnlyList<JetState> Solve(
        JetState inletState,
        double sectionLengthM,
        int stepCount,
        Func<double, double> areaFunction,
        Func<double, double> perimeterFunction)
    {
        if (sectionLengthM <= 0 || stepCount < 1)
            return new List<JetState> { inletState };

        double dx = sectionLengthM / stepCount;
        var states = new List<JetState> { inletState };
        JetState current = inletState;
        double primaryMdot = inletState.MassFlowKgS;

        for (int step = 1; step <= stepCount; step++)
        {
            double x = step * dx;
            double area = Math.Max(areaFunction(x), 1e-12);
            double perimeter = Math.Max(perimeterFunction(x), 0.0);

            double dmDotPerM = _entrainment.ComputeEntrainedMassPerLength(
                _ambient.DensityKgM3,
                current.VelocityMps,
                perimeter);
            double deltaMdot = dmDotPerM * dx;

            double mTotalOld = current.TotalMassFlowKgS;
            double vNew = _mixing.ComputeMixedVelocity(
                mTotalOld,
                current.VelocityMps,
                deltaMdot,
                _ambient.VelocityMps);

            double entrainedNew = current.EntrainedMassFlowKgS + deltaMdot;
            double mTotalNew = primaryMdot + entrainedNew;

            double tNew = mTotalNew > 1e-18
                ? (mTotalOld * current.TemperatureK + deltaMdot * _ambient.TemperatureK) / mTotalNew
                : current.TemperatureK;

            double pStatic = _ambient.PressurePa;
            double rhoNew = _gas.Density(pStatic, tNew);

            var next = new JetState(
                axialPositionM: x,
                pressurePa: pStatic,
                temperatureK: tNew,
                densityKgM3: rhoNew,
                velocityMps: vNew,
                areaM2: area,
                primaryMassFlowKgS: primaryMdot,
                entrainedMassFlowKgS: entrainedNew);

            states.Add(next);
            current = next;
        }

        return states;
    }
}
