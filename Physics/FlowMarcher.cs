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
    private readonly InletSuctionModel _inletSuction = new();

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

    /// <summary>
    /// March with compressible entrainment intake (sonic / choked cap), mixed static pressure from mass-weighted
    /// entrainment + core pressures, separate axial/tangential momentum, swirl decay on the primary stream.
    /// First-order 1-D model — not CFD.
    /// </summary>
    public FlowMarchDetailedResult SolveDetailed(
        JetState inletState,
        double sectionLengthM,
        int stepCount,
        Func<double, double> areaFunction,
        Func<double, double> perimeterFunction,
        Func<double, double> captureAreaFunction,
        double primaryTangentialVelocityMps,
        double swirlDecayPerStepFactor)
    {
        if (sectionLengthM <= 0 || stepCount < 1)
        {
            return new FlowMarchDetailedResult
            {
                FlowStates = new List<JetState> { inletState },
                StepResults = Array.Empty<FlowMarchStepResult>(),
                FinalTangentialVelocityMps = primaryTangentialVelocityMps,
                FinalAxialVelocityMps = inletState.VelocityMps
            };
        }

        double dx = sectionLengthM / stepCount;
        double primaryMdot = inletState.MassFlowKgS;
        var states = new List<JetState> { inletState };
        var stepResults = new List<FlowMarchStepResult>(stepCount);

        JetState current = inletState;
        double pOld = Math.Max(current.PressurePa, 1.0);
        double tOld = Math.Max(current.TemperatureK, 1.0);
        double va = current.VelocityMps;
        double vtPrimary = primaryTangentialVelocityMps;
        double decay = Math.Clamp(swirlDecayPerStepFactor, 0.5, 1.0);

        for (int step = 1; step <= stepCount; step++)
        {
            vtPrimary *= decay;

            double x = step * dx;
            double area = Math.Max(areaFunction(x), 1e-12);
            double perimeter = Math.Max(perimeterFunction(x), 0.0);
            double aCap = Math.Max(captureAreaFunction(x), 1e-15);

            double mOld = current.TotalMassFlowKgS;
            double vtMixedForCorr = primaryMdot * vtPrimary / Math.Max(mOld, 1e-18);
            double vMag = Math.Sqrt(va * va + vtMixedForCorr * vtMixedForCorr);
            vMag = Math.Max(vMag, Math.Abs(va));

            double dmRequested = _entrainment.ComputeEntrainedMassPerLength(
                _ambient.DensityKgM3,
                vMag,
                perimeter) * dx;

            InletSuctionOutcome intake = _inletSuction.Solve(
                _gas,
                _ambient,
                dmRequested,
                aCap,
                tOld);

            double dmActual = Math.Max(intake.ActualEntrainedMassFlowKgS, 0.0);
            double mNew = mOld + dmActual;
            if (mNew < 1e-18)
                mNew = mOld;

            double tNew = mNew > 1e-18
                ? (mOld * tOld + dmActual * Math.Max(_ambient.TemperatureK, 1.0)) / mNew
                : tOld;
            tNew = Math.Max(tNew, 1.0);

            double pNew = mNew > 1e-18
                ? (mOld * pOld + dmActual * Math.Max(intake.PinletLocalPa, 1.0)) / mNew
                : pOld;
            pNew = Math.Max(pNew, 1.0);

            double vtMixed = primaryMdot * vtPrimary / Math.Max(mNew, 1e-18);

            double vaNew = mNew > 1e-18
                ? (mOld * va + dmActual * intake.EntrainmentVelocityMps) / mNew
                : va;

            double rhoNew = _gas.Density(pNew, tNew);
            double swirlKe = 0.5 * vtMixed * vtMixed;
            double dFN = PressureForceMath.InletCaptureAnnulusAxialForce(
                _ambient.PressurePa,
                intake.PinletLocalPa,
                aCap);

            double entrainedTotal = current.EntrainedMassFlowKgS + dmActual;

            var next = new JetState(
                axialPositionM: x,
                pressurePa: pNew,
                temperatureK: tNew,
                densityKgM3: rhoNew,
                velocityMps: vaNew,
                areaM2: area,
                primaryMassFlowKgS: primaryMdot,
                entrainedMassFlowKgS: entrainedTotal);

            stepResults.Add(new FlowMarchStepResult
            {
                AxialPositionM = x,
                AreaM2 = area,
                PerimeterM = perimeter,
                MixedStaticPressurePa = pNew,
                MixedTemperatureK = tNew,
                MixedDensityKgM3 = rhoNew,
                MixedVelocityMps = vaNew,
                PrimaryMassFlowKgS = primaryMdot,
                EntrainedMassFlowKgS = entrainedTotal,
                RequestedDeltaEntrainedMassFlowKgS = dmRequested,
                DeltaEntrainedMassFlowKgS = dmActual,
                InletLocalPressurePa = intake.PinletLocalPa,
                InletEntrainmentVelocityMps = intake.EntrainmentVelocityMps,
                InletMach = intake.Mach,
                InletIsChoked = intake.IsChoked,
                TangentialVelocityMps = vtMixed,
                AxialVelocityMps = vaNew,
                SwirlKineticEnergyPerKg = swirlKe,
                RecoveredPressureRisePa = 0.0,
                PressureForceN = dFN
            });

            states.Add(next);
            current = next;
            pOld = pNew;
            tOld = tNew;
            va = vaNew;
        }

        double finalVtMixed = stepResults.Count > 0
            ? stepResults[^1].TangentialVelocityMps
            : primaryMdot * vtPrimary / Math.Max(inletState.TotalMassFlowKgS, 1e-18);

        return new FlowMarchDetailedResult
        {
            FlowStates = states,
            StepResults = stepResults,
            FinalTangentialVelocityMps = finalVtMixed,
            FinalAxialVelocityMps = current.VelocityMps
        };
    }
}
