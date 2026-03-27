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
    /// Compressible entrainment (choked cap): carry h₀ and P₀ by mass mixing of stagnation properties, apply named
    /// <see cref="ChamberMarchLossModel"/> P₀ decrements, derive (P,T) from P₀, h₀, and |V|; evolve ṁ and axial momentum
    /// via mixing; evolve Ġ_θ explicitly (entrainment at V_θ=0 dilutes V_θ,bulk); Ce uses <see cref="SwirlMath.FluxSwirlNumber"/>;
    /// radial structure each step via <see cref="RadialVortexPressureModel"/>.
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
        double decay = Math.Clamp(swirlDecayPerStepFactor, 0.5, 1.0);
        bool anyChoked = false;

        double areaFirst = Math.Max(areaFunction(dx), 1e-12);
        double rWallFirst = Math.Sqrt(areaFirst / Math.PI);
        double rRef = double.IsNaN(swirlMomentRadiusM) || swirlMomentRadiusM <= 0
            ? rWallFirst
            : swirlMomentRadiusM;
        rRef = Math.Max(rRef, 1e-4);

        double vtInletBulk = primaryTangentialVelocityMps;
        double angularMomentumFluxKgM2PerS2 = SwirlMath.AngularMomentumFluxFromBulk(
            primaryMdot,
            rRef,
            vtInletBulk);

        double cp = _gas.SpecificHeatCp;
        double g = GasProperties.Gamma;
        var compInlet = CompressibleState.FromMixedStatic(_gas, pOld, tOld, va, vtInletBulk);
        double h0Carried = compInlet.TotalEnthalpyJPerKg;
        double p0Carried = compInlet.TotalPressurePa;

        for (int step = 1; step <= stepCount; step++)
        {
            double x = step * dx;
            double area = Math.Max(areaFunction(x), 1e-12);
            double perimeter = Math.Max(perimeterFunction(x), 0.0);
            double aCap = Math.Max(captureAreaFunction(x), 1e-15);

            double rWallGeom = Math.Sqrt(area / Math.PI);
            double mOld = current.TotalMassFlowKgS;
            double entrainedOld = current.EntrainedMassFlowKgS;
            double vtOldBulk = SwirlMath.BulkTangentialVelocityFromAngularMomentumFlux(
                angularMomentumFluxKgM2PerS2,
                mOld,
                rRef);

            double angularMomFluxForCe = angularMomentumFluxKgM2PerS2;
            double axialMomFluxForCe = mOld * va;
            double sFlux = SwirlMath.FluxSwirlNumber(angularMomFluxForCe, axialMomFluxForCe, rRef);

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

            double pMixClamped = Math.Clamp(pOld, 1.0, _ambient.PressurePa * 0.99999);
            double mdotChokedCeiling = _gas.ChokedMassFlux(_ambient.PressurePa, _ambient.TemperatureK) * aCap;
            double mdotPressureLimited = CompressibleFlowMath.MassFlowFromStagnationToStaticPressure(
                _gas,
                _ambient.PressurePa,
                _ambient.TemperatureK,
                pMixClamped,
                aCap);
            mdotPressureLimited = Math.Max(0.0, mdotPressureLimited);
            double dmDemandCapped = Math.Min(
                dmRequested,
                Math.Min(mdotPressureLimited, Math.Max(mdotChokedCeiling, 0.0)));

            InletSuctionOutcome intake = _inletSuction.Solve(
                _gas,
                _ambient,
                dmDemandCapped,
                aCap,
                tOld);

            double dmActual = Math.Max(intake.ActualEntrainedMassFlowKgS, 0.0);
            if (intake.IsChoked)
                anyChoked = true;

            double mNew = mOld + dmActual;
            if (mNew < 1e-18)
                mNew = mOld;

            var ambStag = CompressibleState.FromMixedStatic(
                _gas,
                _ambient.PressurePa,
                _ambient.TemperatureK,
                intake.EntrainmentVelocityMps,
                0.0);
            double h0Amb = ambStag.TotalEnthalpyJPerKg;
            double p0Amb = ambStag.TotalPressurePa;

            double h0Mix = mNew > 1e-18 ? (mOld * h0Carried + dmActual * h0Amb) / mNew : h0Carried;
            double p0Mix = mNew > 1e-18 ? (mOld * p0Carried + dmActual * p0Amb) / mNew : p0Carried;

            double vaNew = _mixing.ComputeMixedVelocity(mOld, va, dmActual, intake.EntrainmentVelocityMps);

            angularMomentumFluxKgM2PerS2 *= decay;
            double vtMixed = SwirlMath.BulkTangentialVelocityFromAngularMomentumFlux(
                angularMomentumFluxKgM2PerS2,
                mNew,
                rRef);

            double vmag2 = vaNew * vaNew + vtMixed * vtMixed;
            double vmag = Math.Sqrt(vmag2);

            double rhoApprox = Math.Max(current.DensityKgM3, 1e-6);
            double qBar = 0.5 * rhoApprox * vMagCorr * vMagCorr;
            double qSwirl = 0.5 * rhoApprox * vtOldBulk * vtOldBulk;
            double dHyd = 2.0 * Math.Max(rWallGeom, 1e-5);
            double dp0Mix = ChamberMarchLossModel.MixingTotalPressureLossPa(dmActual, mNew, qBar);
            double dp0Wall = ChamberMarchLossModel.WallTotalPressureLossPa(dx, dHyd, qBar);
            double dp0Swirl = ChamberMarchLossModel.SwirlDecayTotalPressureLossPa(decay, qSwirl);
            double p0AfterLoss = p0Mix - dp0Mix - dp0Wall - dp0Swirl;
            p0AfterLoss = Math.Max(p0AfterLoss, _ambient.PressurePa + 1.0);

            double t0FromH = h0Mix / cp;
            double tNew = Math.Max(t0FromH - vmag * vmag / (2.0 * cp), 1.0);
            double pNew = p0AfterLoss * Math.Pow(tNew / Math.Max(t0FromH, 1.0), g / (g - 1.0));
            double pMixedCap = ChamberPhysicsCoefficients.MarchMixedStaticPressureMaxTimesAmbient
                * Math.Max(_ambient.PressurePa, 1.0);
            pNew = Math.Min(pNew, pMixedCap);
            pNew = SiPressureGuards.ClampStaticPressurePa(pNew);
            double rhoNew = _gas.Density(pNew, tNew);

            double mFluxThermo = rhoNew * area * Math.Max(Math.Abs(vaNew), 1e-12);
            if (mFluxThermo > 1e-18 && Math.Abs(mFluxThermo - mNew) / mNew > 0.28)
            {
                double rhoCont = mNew / (area * Math.Max(Math.Abs(vaNew), 1e-5));
                tNew = Math.Max(t0FromH - vmag * vmag / (2.0 * cp), 1.0);
                pNew = rhoCont * GasProperties.R * tNew;
                pNew = Math.Min(pNew, pMixedCap);
                pNew = SiPressureGuards.ClampStaticPressurePa(pNew);
                rhoNew = _gas.Density(pNew, tNew);
            }

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
            double reStep = rhoNew * vMagCorr * dHyd / Math.Max(mu, 1e-12);

            var comp = CompressibleState.FromMixedStatic(_gas, pNew, tNew, vaNew, vtMixed);
            double contRes = Math.Abs(rhoNew * area * vaNew - mNew) / Math.Max(mNew, 1e-18);

            h0Carried = h0Mix;
            p0Carried = comp.TotalPressurePa;

            double axialMomFluxNew = mNew * vaNew;
            double angFluxBulk = angularMomentumFluxKgM2PerS2;
            double sFluxPost = SwirlMath.FluxSwirlNumber(angFluxBulk, axialMomFluxNew, rRef);

            var stepUpdate = new FlowStepUpdate(
                dmActual,
                dmRequested,
                intake.MaxSupportedEntrainedMassFlowKgS,
                mdotPressureLimited);

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
            : SwirlMath.BulkTangentialVelocityFromAngularMomentumFlux(
                angularMomentumFluxKgM2PerS2,
                inletState.TotalMassFlowKgS,
                rRef);

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
            FinalPrimaryTangentialVelocityMps = SwirlMath.BulkTangentialVelocityFromAngularMomentumFlux(
                angularMomentumFluxKgM2PerS2,
                primaryMdot,
                rRef)
        };
    }
}
