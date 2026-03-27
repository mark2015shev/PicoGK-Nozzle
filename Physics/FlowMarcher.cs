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
                _entrainment.Coefficient,
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
    /// Compressible entrainment (choked cap), mass-weighted P and T, separate axial/tangential momentum mixing,
    /// Ġ_θ = ṁ_p r V_θ,p for Ce correlation, radial equilibrium each step via <see cref="RadialVortexPressureModel"/>.
    /// </summary>
    public FlowMarchDetailedResult SolveDetailed(
        JetState inletState,
        double sectionLengthM,
        int stepCount,
        Func<double, double> areaFunction,
        Func<double, double> perimeterFunction,
        Func<double, double> captureAreaFunction,
        double primaryTangentialVelocityMps,
        double swirlDecayPerStepFactor,
        double entrainmentMassDemandMultiplier = 1.0,
        double chamberLdRatio = 1.0,
        double chamberDiameterMm = 50.0,
        bool useReynoldsOnEntrainmentCe = false,
        double swirlMomentRadiusM = double.NaN)
    {
        if (sectionLengthM <= 0 || stepCount < 1)
        {
            return new FlowMarchDetailedResult
            {
                FlowStates = new List<JetState> { inletState },
                StepResults = Array.Empty<FlowMarchStepResult>(),
                StepPhysicsStates = Array.Empty<FlowStepState>(),
                MarchClosure = null,
                FinalTangentialVelocityMps = primaryTangentialVelocityMps,
                FinalAxialVelocityMps = inletState.VelocityMps,
                FinalPrimaryTangentialVelocityMps = primaryTangentialVelocityMps
            };
        }

        double boost = Math.Clamp(
            entrainmentMassDemandMultiplier,
            0.25,
            ChamberPhysicsCoefficients.EntrainmentMassDemandBoostClampMax);
        double dx = sectionLengthM / stepCount;
        double primaryMdot = inletState.MassFlowKgS;
        var states = new List<JetState> { inletState };
        var stepResults = new List<FlowMarchStepResult>(stepCount);
        var physicsSteps = new List<FlowStepState>(stepCount);

        JetState current = inletState;
        double pOld = Math.Max(current.PressurePa, 1.0);
        double tOld = Math.Max(current.TemperatureK, 1.0);
        double va = current.VelocityMps;
        double vtPrimary = primaryTangentialVelocityMps;
        double decay = Math.Clamp(swirlDecayPerStepFactor, 0.5, 1.0);
        bool anyChoked = false;

        for (int step = 1; step <= stepCount; step++)
        {
            vtPrimary *= decay;

            double x = step * dx;
            double area = Math.Max(areaFunction(x), 1e-12);
            double perimeter = Math.Max(perimeterFunction(x), 0.0);
            double aCap = Math.Max(captureAreaFunction(x), 1e-15);

            double rWallGeom = Math.Sqrt(area / Math.PI);
            double rMom = double.IsNaN(swirlMomentRadiusM) || swirlMomentRadiusM <= 0 ? rWallGeom : swirlMomentRadiusM;

            double mOld = current.TotalMassFlowKgS;
            double entrainedOld = current.EntrainedMassFlowKgS;
            double vtOldBulk = primaryMdot * vtPrimary / Math.Max(mOld, 1e-18);

            double angularMomFluxForCe = primaryMdot * rMom * vtPrimary;
            double axialMomFluxForCe = mOld * va;
            double sFlux = SwirlMath.FluxSwirlNumber(angularMomFluxForCe, axialMomFluxForCe, rMom);

            double vMagCorr = Math.Sqrt(va * va + vtOldBulk * vtOldBulk);
            vMagCorr = Math.Max(vMagCorr, Math.Abs(va));

            double reApprox = SwirlChamberMarchGeometry.ChamberReynoldsApprox(vMagCorr, chamberDiameterMm);
            double ceStep = _entrainment.ComputeCoefficient(
                sFlux,
                chamberLdRatio,
                reApprox,
                useReynoldsOnEntrainmentCe);

            double dmRequested = _entrainment.ComputeEntrainedMassPerLength(
                ceStep,
                _ambient.DensityKgM3,
                vMagCorr,
                perimeter) * dx * boost;

            InletSuctionOutcome intake = _inletSuction.Solve(
                _gas,
                _ambient,
                dmRequested,
                aCap,
                tOld);

            double dmActual = Math.Max(intake.ActualEntrainedMassFlowKgS, 0.0);
            if (intake.IsChoked)
                anyChoked = true;

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

            double vaNew = _mixing.ComputeMixedVelocity(mOld, va, dmActual, intake.EntrainmentVelocityMps);
            double vtMixed = _mixing.ComputeMixedTangentialVelocity(mOld, vtOldBulk, dmActual, 0.0);

            double rhoNew = _gas.Density(pNew, tNew);
            double swirlKe = 0.5 * vtMixed * vtMixed;
            double dFN = PressureForceMath.InletCaptureAnnulusAxialForce(
                _ambient.PressurePa,
                intake.PinletLocalPa,
                aCap);

            double entrainedTotal = entrainedOld + dmActual;

            var radial = RadialVortexPressureModel.Compute(
                rhoNew,
                Math.Abs(vtMixed),
                rWallGeom,
                ChamberPhysicsCoefficients.RadialCoreRadiusFractionOfWall,
                ChamberPhysicsCoefficients.RadialPressureCapPa);

            double pCore = Math.Max(pNew - radial.CorePressureDropPa, 1.0);
            double pWall = pNew + radial.WallPressureRisePa;

            double mu = _gas.DynamicViscosityAirPaS(tNew);
            double dHyd = 2.0 * rWallGeom;
            double reStep = rhoNew * vMagCorr * dHyd / Math.Max(mu, 1e-12);

            var comp = CompressibleState.FromMixedStatic(_gas, pNew, tNew, vaNew, vtMixed);
            double contRes = Math.Abs(rhoNew * area * vaNew - mNew) / Math.Max(mNew, 1e-18);

            double axialMomFluxNew = mNew * vaNew;
            double angFluxBulk = mNew * rMom * vtMixed;
            double sFluxPost = SwirlMath.FluxSwirlNumber(angFluxBulk, axialMomFluxNew, rMom);

            var stepUpdate = new FlowStepUpdate(dmActual, dmRequested, intake.MaxSupportedEntrainedMassFlowKgS);

            physicsSteps.Add(new FlowStepState
            {
                X = x,
                AreaM2 = area,
                CaptureAreaM2 = aCap,
                WettedPerimeterM = perimeter,
                MdotPrimaryKgS = primaryMdot,
                MdotSecondaryKgS = entrainedTotal,
                MdotTotalKgS = mNew,
                PStaticPa = pNew,
                TStaticK = tNew,
                DensityKgM3 = rhoNew,
                PTotalPa = comp.TotalPressurePa,
                TTotalK = comp.TotalTemperatureK,
                VAxialMps = vaNew,
                VTangentialMps = vtMixed,
                VMagnitudeMps = comp.MagnitudeVelocityMps,
                Mach = comp.MachNumber,
                Reynolds = reStep,
                SwirlNumberFlux = sFluxPost,
                AngularMomentumFluxKgM2PerS2 = angFluxBulk,
                AxialMomentumFluxKgM2PerS2 = axialMomFluxNew,
                CorePressurePa = pCore,
                WallPressurePa = pWall,
                RadialPressureDeltaPa = radial.EstimatedRadialPressureDeltaPa,
                ContinuityResidualRelative = contRes,
                StepUpdate = stepUpdate,
                Compressible = comp
            });

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
                PressureForceN = dFN,
                DuctEffectiveAreaM2 = area,
                CaptureAreaM2 = aCap,
                EntrainmentCeEffective = ceStep
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

        FlowStepState? lastP = physicsSteps.Count > 0 ? physicsSteps[^1] : null;
        MarchClosureResult? closure = lastP == null
            ? null
            : new MarchClosureResult
            {
                FinalMachBulk = lastP.Mach,
                FinalReynolds = lastP.Reynolds,
                AnyEntrainmentChoked = anyChoked,
                FinalFluxSwirlNumber = lastP.SwirlNumberFlux,
                FinalContinuityResidualRelative = lastP.ContinuityResidualRelative
            };

        return new FlowMarchDetailedResult
        {
            FlowStates = states,
            StepResults = stepResults,
            StepPhysicsStates = physicsSteps,
            MarchClosure = closure,
            FinalTangentialVelocityMps = finalVtMixed,
            FinalAxialVelocityMps = current.VelocityMps,
            FinalPrimaryTangentialVelocityMps = vtPrimary
        };
    }
}
