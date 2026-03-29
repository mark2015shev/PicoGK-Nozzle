using System;
using System.Collections.Generic;

#pragma warning disable CS0618 // legacy duct march intentionally retained for tooling

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
    /// Uses ambient static pressure and mass-weighted temperature for the mixed stream (simplified duct model).
    /// </summary>
    /// <remarks>
    /// <b>Legacy / simplified:</b> not used by the live SI nozzle path, which uses <see cref="SolveDetailed"/> (stagnation-based
    /// mixing, P₀/h₀ carry, derived statics). Prefer <see cref="SolveDetailed"/> for nozzle physics.
    /// </remarks>
    [Obsolete(
        "Legacy simplified duct march (ambient P_static + mass-weighted T). Live SI nozzle uses SolveDetailed only — do not use in the SI pipeline.")]
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

#pragma warning restore CS0618

    /// <summary>
    /// Compressible entrainment (choked cap): carry h₀ and P₀ by mass mixing of stagnation properties, apply named
    /// <see cref="ChamberMarchLossModel"/> P₀ decrements, derive (P,T) from P₀, h₀, and |V|; evolve ṁ and axial momentum
    /// via mixing; evolve Ġ_θ with wall / mixing / entrainment-dilution loss terms (plus ṁ growth diluting V_θ,bulk);
    /// entrainment demand: pressure deficit at capture (Bernoulli entry speed) × lumped η_mix(L/D, Re) for axial distribution,
    /// plus a small bounded shear term;
    /// bulk P_static from <see cref="CompressibleFlowMath.BulkChamberThermoFromStagnationAndSpeedMagnitude"/> (P₀, T₀, |V|);
    /// radial shaping only via <see cref="RadialVortexPressureModel.ComputeShapingRelativeToBulk"/> (reduced-order dP/dr balance).
    /// Optional swirl-passage Mach cap limits ṁ before inlet suction when <paramref name="chamberFullBoreAreaM2"/> &gt; 0.
    /// <paramref name="swirlDecayPerStepFactor"/> is unused (call-site stability); Ġ_θ uses explicit loss terms.
    /// <paramref name="freeAnnulusAreaM2"/> when &gt; 0 enters min(capture, annulus, bore, free) for entrainment entry area.
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
        double captureStaticPressureDeficitAugmentationPa = 0.0,
        double chamberLdRatio = 1.0,
        double chamberDiameterMm = 50.0,
        bool useReynoldsOnEntrainmentCe = false,
        double swirlMomentRadiusM = double.NaN,
        bool validateMarchStepInvariants = false,
        double chamberFullBoreAreaM2 = 0.0,
        double freeAnnulusAreaM2 = 0.0,
        bool capEntrainmentToSwirlPassageMach = true,
        SwirlEntranceCapacityLimits? swirlPassageMachLimitsForEntrainmentCap = null,
        SwirlChamberDischargePathSpec? dischargePathSpec = null)
    {
        if (sectionLengthM <= 0 || stepCount < 1)
        {
            return new FlowMarchDetailedResult
            {
                FlowStates = new List<JetState> { inletState },
                StepResults = Array.Empty<FlowMarchStepResult>(),
                StepPhysicsStates = Array.Empty<FlowStepState>(),
                MarchInvariantWarnings = Array.Empty<string>(),
                MarchClosure = null,
                MarchResidualSummary = null,
                FinalTangentialVelocityMps = primaryTangentialVelocityMps,
                FinalAxialVelocityMps = inletState.VelocityMps,
                FinalPrimaryTangentialVelocityMps = primaryTangentialVelocityMps,
                EntrainmentStepsLimitedBySwirlPassageCapacity = 0,
                SumPrimaryPressureDrivenEntrainmentDemandKgS = 0.0,
                SumEntrainmentMassTrimmedByPassageGovernorKgS = 0.0,
                EntrainmentGovernorMachMaxUsed =
                    (swirlPassageMachLimitsForEntrainmentCap ?? SwirlEntranceCapacityLimits.Default).EntrainmentGovernorMachMax,
                AppliedDischargePathSpec = null,
                FinalChamberDischargeSplit = null
            };
        }

        var invariantSink = validateMarchStepInvariants ? new List<string>() : null;

        double pAmbEnt = Math.Max(_ambient.PressurePa, 1.0);
        double rhoAmbEnt = Math.Max(_ambient.DensityKgM3, 1e-9);
        double aSoundAmb = _gas.SpeedOfSound(_ambient.TemperatureK);
        double maxEntEntryMps = 0.92 * Math.Max(aSoundAmb, 1.0);
        double dx = sectionLengthM / stepCount;
        double primaryMdot = inletState.MassFlowKgS;
        var states = new List<JetState> { inletState };
        var stepResults = new List<FlowMarchStepResult>(stepCount);
        var physicsSteps = new List<FlowStepState>(stepCount);

        JetState current = inletState;
        double pOld = Math.Max(current.PressurePa, 1.0);
        double tOld = Math.Max(current.TemperatureK, 1.0);
        double va = current.VelocityMps;
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

        SwirlEntranceCapacityLimits passageLim = swirlPassageMachLimitsForEntrainmentCap ?? SwirlEntranceCapacityLimits.Default;
        int entrainmentPassageCapSteps = 0;
        double sumPrimaryPressureDrivenEntrainmentDemandKgS = 0.0;
        double sumEntrainmentTrimmedByGovernorKgS = 0.0;

        double maxContinuityResidualRelative = 0.0;
        double sumContinuityResidualRelative = 0.0;
        double maxAxialMomentumBudgetResidualRelative = 0.0;
        double maxAngularMomentumFluxClosureResidualRelative = 0.0;

        _ = swirlDecayPerStepFactor;

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

            double angularMomFluxDiag = angularMomentumFluxKgM2PerS2;
            double axialMomFluxDiag = mOld * va;
            double vMagPreStep = Math.Sqrt(va * va + vtOldBulk * vtOldBulk);
            double vMagCorr = Math.Max(vMagPreStep, 1e-9);
            double sFluxDiagnostic = SwirlMath.SwirlCorrelationForEntrainment(
                angularMomFluxDiag,
                axialMomFluxDiag,
                mOld,
                rRef,
                vMagCorr,
                vtOldBulk);

            double reApprox = SwirlChamberMarchGeometry.ChamberReynoldsApprox(vMagCorr, chamberDiameterMm);
            double etaMixStep = _entrainment.LumpedAxialMixingEffectiveness(
                chamberLdRatio,
                reApprox,
                useReynoldsOnEntrainmentCe);

            double aEffEntry = EffectiveEntrainmentEntryAreaM2(aCap, area, chamberFullBoreAreaM2, freeAnnulusAreaM2);

            double rhoPreStep = Math.Max(current.DensityKgM3, 1e-6);
            double qSwirlPreStep = 0.5 * rhoPreStep * vtOldBulk * vtOldBulk;
            double capRadialPreStep = Math.Min(
                ChamberPhysicsCoefficients.RadialPressureCapAbsoluteMaxPa,
                Math.Max(
                    ChamberPhysicsCoefficients.RadialPressureCapPa,
                    ChamberAerodynamicsConfiguration.RadialIntegralCapTimesSwirlDynamicPressure * qSwirlPreStep));
            var radialPreEntrainment = RadialVortexPressureModel.ComputeShapingRelativeToBulk(
                pOld,
                Math.Max(p0Carried, pOld),
                rhoPreStep,
                Math.Abs(vtOldBulk),
                rWallGeom,
                ChamberPhysicsCoefficients.RadialCoreRadiusFractionOfWall,
                capRadialPreStep,
                _ambient.PressurePa);
            double pCaptureBoundaryForEntrainmentPa = SiPressureGuards.ClampStaticPressurePa(
                Math.Max(pOld - radialPreEntrainment.CorePressureDropPa, _ambient.PressurePa * 0.5));

            double dmPress = PressureDrivenEntrainmentPhysics.MassIncrementForStep(
                pAmbEnt,
                rhoAmbEnt,
                pCaptureBoundaryForEntrainmentPa,
                aEffEntry,
                ChamberPhysicsCoefficients.CaptureEntrainmentDischargeCoefficient,
                dx,
                sectionLengthM,
                etaMixStep,
                captureStaticPressureDeficitAugmentationPa,
                maxEntEntryMps);
            double dmShear = _entrainment.ComputeShearAugmentedMassIncrement(
                    etaMixStep,
                    rhoAmbEnt,
                    vMagCorr,
                    perimeter,
                    dx)
                * ChamberPhysicsCoefficients.EntrainmentShearAugmentationFraction;
            double dmRequested = dmPress + dmShear;
            sumPrimaryPressureDrivenEntrainmentDemandKgS += dmRequested;

            double pMixClamped = Math.Clamp(
                pCaptureBoundaryForEntrainmentPa,
                1.0,
                _ambient.PressurePa * 0.99999);
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

            double dmBeforeSwirlPassageGovernor = dmDemandCapped;
            double passageMdotCeil = double.NaN;
            bool cappedBySwirlPassage = false;
            double govMach = passageLim.EntrainmentGovernorMachMax;
            if (capEntrainmentToSwirlPassageMach
                && chamberFullBoreAreaM2 > 1e-18
                && govMach > 1e-9)
            {
                double aEffPass = Math.Min(Math.Min(aCap, area), chamberFullBoreAreaM2);
                double mdotCeil = SwirlEntranceCapacityEvaluator.MaxMdotForBulkMachLimit(
                    _gas,
                    current.DensityKgM3,
                    current.TemperatureK,
                    aEffPass,
                    govMach);
                passageMdotCeil = mdotCeil;
                if (mOld > mdotCeil + 1e-9)
                {
                    dmDemandCapped = 0.0;
                    cappedBySwirlPassage = true;
                    entrainmentPassageCapSteps++;
                    invariantSink?.Add(
                        $"March step {step}: SWIRL PASSAGE GOVERNOR — ṁ_mix ({mOld:E}) exceeds ρ·A_eff·a·M limit (M={govMach:F3}); ceiling {mdotCeil:E} kg/s; entrainment increment forced to zero.");
                }
                else
                {
                    double mWouldBe = mOld + dmDemandCapped;
                    if (mWouldBe > mdotCeil + 1e-12)
                    {
                        double dmAllow = Math.Max(0.0, mdotCeil - mOld);
                        if (dmAllow + 1e-15 < dmDemandCapped)
                        {
                            dmDemandCapped = dmAllow;
                            cappedBySwirlPassage = true;
                            entrainmentPassageCapSteps++;
                        }
                    }
                }
            }

            sumEntrainmentTrimmedByGovernorKgS += Math.Max(0.0, dmBeforeSwirlPassageGovernor - dmDemandCapped);

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

            double dHyd = 2.0 * Math.Max(rWallGeom, 1e-5);
            double lBeforeStep = angularMomentumFluxKgM2PerS2;
            ChamberMarchLossModel.AngularMomentumFluxLossesStep(
                lBeforeStep,
                dx,
                dHyd,
                dmActual,
                mOld,
                out double wallLossAngMom,
                out double mixingLossAngMom,
                out double dilutionLossAngMom);
            double lAbs0 = Math.Abs(lBeforeStep);
            double lAbs1 = Math.Max(lAbs0 - wallLossAngMom - mixingLossAngMom - dilutionLossAngMom, 0.0);
            angularMomentumFluxKgM2PerS2 = lAbs0 < 1e-30 ? 0.0 : Math.Sign(lBeforeStep) * lAbs1;

            double vtMixed = SwirlMath.BulkTangentialVelocityFromAngularMomentumFlux(
                angularMomentumFluxKgM2PerS2,
                mNew,
                rRef);

            double rhoApprox = Math.Max(current.DensityKgM3, 1e-6);
            double vmagMixGuess = Math.Sqrt(vaNew * vaNew + vtMixed * vtMixed);
            double qBar = 0.5 * rhoApprox * Math.Max(vmagMixGuess * vmagMixGuess, 1e-12);
            double qSwirl = 0.5 * rhoApprox * vtMixed * vtMixed;
            double dp0Mix = ChamberMarchLossModel.MixingTotalPressureLossPa(dmActual, mNew, qBar);
            double dp0Wall = ChamberMarchLossModel.WallTotalPressureLossPa(dx, dHyd, qBar);
            double dp0Swirl = ChamberMarchLossModel.AngularMomentumTotalPressureLossPa(
                lBeforeStep,
                angularMomentumFluxKgM2PerS2,
                qSwirl);
            double dp0StepTotal = dp0Mix + dp0Wall + dp0Swirl;
            double p0AfterLoss = p0Mix - dp0Mix - dp0Wall - dp0Swirl;
            p0AfterLoss = Math.Max(p0AfterLoss, _ambient.PressurePa + 1.0);

            double t0FromH = Math.Max(h0Mix / cp, 1.0);

            double vaIter = vaNew;
            const int contIters = 8;
            for (int it = 0; it < contIters; it++)
            {
                double vmagIt = Math.Sqrt(vaIter * vaIter + vtMixed * vtMixed);
                var bulkIt = CompressibleFlowMath.BulkChamberThermoFromStagnationAndSpeedMagnitude(
                    _gas,
                    p0AfterLoss,
                    t0FromH,
                    vmagIt);
                double ps = bulkIt.StaticPressurePa;
                double ts = bulkIt.StaticTemperatureK;
                ps = SiPressureGuards.ClampStaticPressurePa(ps);
                double rh = _gas.Density(ps, ts);
                double vaNeed = mNew / (rh * area);
                bool contOk = Math.Abs(vaNeed - vaIter) <= 0.012 * Math.Max(Math.Abs(vaNeed), 1.0);
                bool contSmall = Math.Abs(vaNeed - vaIter) < 0.35;
                if (contOk || contSmall)
                    break;
                vaIter = 0.5 * vaIter + 0.5 * vaNeed;
            }

            double vaFinal = vaIter;
            double vmagFinal = Math.Sqrt(vaFinal * vaFinal + vtMixed * vtMixed);
            var bulkFinal = CompressibleFlowMath.BulkChamberThermoFromStagnationAndSpeedMagnitude(
                _gas,
                p0AfterLoss,
                t0FromH,
                vmagFinal);
            double pNew = bulkFinal.StaticPressurePa;
            double tNew = bulkFinal.StaticTemperatureK;
            pNew = SiPressureGuards.ClampStaticPressurePa(pNew);
            double rhoNew = _gas.Density(pNew, tNew);

            double aSound = _gas.SpeedOfSound(tNew);
            double machFromV = vmagFinal / Math.Max(aSound, 1e-9);
            double pFromMach = p0AfterLoss * CompressibleFlowMath.StaticPressureRatioFromMach(machFromV, g);
            if (double.IsFinite(pFromMach) && pNew > 1.0
                && Math.Abs(pFromMach - pNew) / pNew > ChamberAerodynamicsConfiguration.IsentropicPressureConsistencyRelativeTolerance)
            {
                pNew = SiPressureGuards.ClampStaticPressurePa(pFromMach);
                rhoNew = _gas.Density(pNew, tNew);
                aSound = _gas.SpeedOfSound(tNew);
                machFromV = vmagFinal / Math.Max(aSound, 1e-9);
            }

            const double pBulkReasonableMinPa = 50.0;
            const double pBulkReasonableMaxPa = 900_000.0;
            double pCeilBulk = Math.Max(
                pBulkReasonableMinPa + 1.0,
                Math.Min(pBulkReasonableMaxPa, p0AfterLoss * 1.002));
            if (double.IsFinite(pNew))
                pNew = Math.Clamp(pNew, pBulkReasonableMinPa, pCeilBulk);
            if (double.IsFinite(tNew))
                tNew = Math.Clamp(tNew, 50.0, Math.Max(t0FromH * 1.5, 5000.0));
            rhoNew = _gas.Density(pNew, tNew);

            bool stepBulkValid = double.IsFinite(pNew) && double.IsFinite(tNew) && double.IsFinite(rhoNew)
                && pNew >= pBulkReasonableMinPa
                && pNew <= pBulkReasonableMaxPa
                && pNew <= p0AfterLoss * (1.0 + 0.02)
                && tNew >= 50.0;

            if (!stepBulkValid && mNew > 1e-18 && area > 1e-15)
            {
                double rhoCont = mNew / (area * Math.Max(Math.Abs(vaFinal), 0.05));
                double tFloor = Math.Clamp(t0FromH - vmagFinal * vmagFinal / (2.0 * cp), 180.0, t0FromH);
                double pIso = p0AfterLoss * Math.Pow(tFloor / t0FromH, g / (g - 1.0));
                double pCont = rhoCont * GasProperties.R * tFloor;
                double pBlend = 0.5 * (pIso + pCont);
                pNew = SiPressureGuards.ClampStaticPressurePa(Math.Clamp(pBlend, pBulkReasonableMinPa, pCeilBulk));
                tNew = tFloor;
                rhoNew = _gas.Density(pNew, tNew);
                aSound = _gas.SpeedOfSound(tNew);
                machFromV = vmagFinal / Math.Max(aSound, 1e-9);
                stepBulkValid = double.IsFinite(pNew) && pNew >= pBulkReasonableMinPa && pNew <= pBulkReasonableMaxPa;
            }

            double rhoAmb = Math.Max(_ambient.DensityKgM3, 0.2);
            if (rhoNew < 0.28 * rhoAmb)
            {
                rhoNew = 0.28 * rhoAmb;
                pNew = SiPressureGuards.ClampStaticPressurePa(Math.Clamp(rhoNew * GasProperties.R * tNew, pBulkReasonableMinPa, pCeilBulk));
                rhoNew = _gas.Density(pNew, tNew);
                stepBulkValid = false;
            }

            double swirlKe = 0.5 * vtMixed * vtMixed;
            double dFN = PressureForceMath.InletCaptureAnnulusAxialForce(
                _ambient.PressurePa,
                intake.PinletLocalPa,
                aCap);

            double entrainedTotal = entrainedOld + dmActual;

            double qSwirlLoc = 0.5 * rhoNew * vtMixed * vtMixed;
            double capRadialIntegral = Math.Min(
                ChamberPhysicsCoefficients.RadialPressureCapAbsoluteMaxPa,
                Math.Max(
                    ChamberPhysicsCoefficients.RadialPressureCapPa,
                    ChamberAerodynamicsConfiguration.RadialIntegralCapTimesSwirlDynamicPressure * qSwirlLoc));

            var radial = RadialVortexPressureModel.ComputeShapingRelativeToBulk(
                pNew,
                p0AfterLoss,
                rhoNew,
                Math.Abs(vtMixed),
                rWallGeom,
                ChamberPhysicsCoefficients.RadialCoreRadiusFractionOfWall,
                capRadialIntegral,
                _ambient.PressurePa);

            double pCore = SiPressureGuards.ClampStaticPressurePa(Math.Max(pNew - radial.CorePressureDropPa, _ambient.PressurePa * 0.5));
            double pWall = SiPressureGuards.ClampStaticPressurePa(pNew + radial.WallPressureRisePa);
            if (pWall > p0AfterLoss * (1.0 + ChamberAerodynamicsConfiguration.WallStaticExcessOverBulkMaxFractionOfP0))
            {
                pWall = p0AfterLoss * (1.0 + ChamberAerodynamicsConfiguration.WallStaticExcessOverBulkMaxFractionOfP0);
                stepBulkValid = false;
            }

            if (pCore > pNew + 1.0)
                stepBulkValid = false;

            if (!radial.ShapingInvariantsSatisfied)
            {
                stepBulkValid = false;
                invariantSink?.Add(
                    $"March step {step}: RADIAL SHAPING — {radial.ShapingInvariantNote}");
            }

            double mu = _gas.DynamicViscosityAirPaS(tNew);
            double vMagRe = Math.Max(vmagFinal, 1e-9);
            double reStep = rhoNew * vMagRe * dHyd / Math.Max(mu, 1e-12);

            CompressibleState comp = stepBulkValid
                ? CompressibleState.FromAuthoritativeBulkStagnation(_gas, p0AfterLoss, t0FromH, vaFinal, vtMixed)
                : CompressibleState.FromMixedStatic(_gas, pNew, tNew, vaFinal, vtMixed);
            double contRes = Math.Abs(rhoNew * area * vaFinal - mNew) / Math.Max(mNew, 1e-18);
            maxContinuityResidualRelative = Math.Max(maxContinuityResidualRelative, contRes);
            sumContinuityResidualRelative += contRes;

            double axialMomMixApprox = mOld * va + dmActual * intake.EntrainmentVelocityMps;
            double axialMomNew = mNew * vaFinal;
            double axMomRes = Math.Abs(axialMomNew - axialMomMixApprox)
                / Math.Max(Math.Abs(axialMomMixApprox), 1e-9);
            maxAxialMomentumBudgetResidualRelative = Math.Max(maxAxialMomentumBudgetResidualRelative, axMomRes);

            double gMag = Math.Abs(angularMomentumFluxKgM2PerS2);
            double angFluxClosure = gMag > 1e-20
                ? Math.Abs(angularMomentumFluxKgM2PerS2 - mNew * rRef * vtMixed) / gMag
                : 0.0;
            maxAngularMomentumFluxClosureResidualRelative = Math.Max(
                maxAngularMomentumFluxClosureResidualRelative,
                angFluxClosure);

            h0Carried = h0Mix;
            p0Carried = comp.TotalPressurePa;

            double axialMomFluxNew = mNew * vaFinal;
            double angFluxBulk = angularMomentumFluxKgM2PerS2;
            double sFluxPost = SwirlMath.SwirlCorrelationForEntrainment(
                angFluxBulk,
                axialMomFluxNew,
                mNew,
                rRef,
                vmagFinal,
                vtMixed);
            double chamberBulk = SwirlMath.ChamberSwirlBulkRatio(
                vtMixed,
                vaFinal,
                ChamberAerodynamicsConfiguration.VaFloorForBulkSwirlMps);

            var stepUpdate = new FlowStepUpdate(
                dmActual,
                dmRequested,
                intake.MaxSupportedEntrainedMassFlowKgS,
                mdotPressureLimited,
                passageMdotCeil,
                cappedBySwirlPassage);

            SwirlChamberDualPathDischargeResult? dualPath = null;
            if (dischargePathSpec is { } dps)
            {
                dualPath = SwirlChamberDualPathDischargeModel.Compute(
                    pNew,
                    rhoNew,
                    primaryMdot,
                    entrainedTotal,
                    vaFinal,
                    vtMixed,
                    dps);
            }

            var stepPhysics = new FlowStepState
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
                PTotalPa = p0AfterLoss,
                TTotalK = t0FromH,
                VAxialMps = vaFinal,
                VTangentialMps = vtMixed,
                VMagnitudeMps = comp.MagnitudeVelocityMps,
                Mach = comp.MachNumber,
                Reynolds = reStep,
                SwirlNumberFlux = sFluxPost,
                AngularMomentumFluxKgM2PerS2 = angFluxBulk,
                AxialMomentumFluxKgM2PerS2 = axialMomFluxNew,
                TotalPressureAfterLossesPa = p0AfterLoss,
                ChamberSwirlBulkRatio = chamberBulk,
                EntrainmentSwirlCorrelation = sFluxDiagnostic,
                EffectiveEntrainmentEntryAreaM2 = aEffEntry,
                CaptureBoundaryStaticPressureForEntrainmentPa = pCaptureBoundaryForEntrainmentPa,
                AngularMomentumWallLossKgM2PerS2 = wallLossAngMom,
                AngularMomentumMixingLossKgM2PerS2 = mixingLossAngMom,
                AngularMomentumEntrainmentDilutionLossKgM2PerS2 = dilutionLossAngMom,
                TotalPressureLossStepPa = dp0StepTotal,
                StepBulkPressureValid = stepBulkValid,
                CorePressurePa = pCore,
                WallPressurePa = pWall,
                RadialPressureDeltaPa = radial.EstimatedRadialPressureDeltaPa,
                RadialCoreRadiusUsedM = radial.CoreRadiusM,
                RadialShapingInvariantsOk = radial.ShapingInvariantsSatisfied,
                RadialShapingInvariantNote = radial.ShapingInvariantNote,
                ContinuityResidualRelative = contRes,
                StepUpdate = stepUpdate,
                Compressible = comp,
                DualPathDischarge = dualPath
            };
            physicsSteps.Add(stepPhysics);
            if (invariantSink != null)
            {
                if (!stepBulkValid)
                {
                    invariantSink.Add(
                        $"March step {step}: CHAMBER BULK — invalid static/total ordering or radial wall exceeded P₀ ceiling (see FlowStepState).");
                }

                invariantSink.AddRange(MarchInvariantValidation.CollectStep(stepPhysics, _gas, step));
            }

            var next = new JetState(
                axialPositionM: x,
                pressurePa: pNew,
                temperatureK: tNew,
                densityKgM3: rhoNew,
                velocityMps: vaFinal,
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
                MixedVelocityMps = vaFinal,
                PrimaryMassFlowKgS = primaryMdot,
                EntrainedMassFlowKgS = entrainedTotal,
                RequestedDeltaEntrainedMassFlowKgS = dmRequested,
                DeltaEntrainedMassFlowKgS = dmActual,
                InletLocalPressurePa = intake.PinletLocalPa,
                InletEntrainmentVelocityMps = intake.EntrainmentVelocityMps,
                InletMach = intake.Mach,
                InletIsChoked = intake.IsChoked,
                TangentialVelocityMps = vtMixed,
                AxialVelocityMps = vaFinal,
                SwirlKineticEnergyPerKg = swirlKe,
                RecoveredPressureRisePa = 0.0,
                PressureForceN = dFN,
                DuctEffectiveAreaM2 = area,
                CaptureAreaM2 = aCap,
                EntrainmentMixingEffectivenessUsed = etaMixStep,
                CaptureBoundaryStaticPressureForEntrainmentPa = pCaptureBoundaryForEntrainmentPa
            });

            states.Add(next);
            current = next;
            pOld = pNew;
            tOld = tNew;
            va = vaFinal;
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
                FinalChamberSwirlBulk = lastP.ChamberSwirlBulkRatio,
                FinalEntrainmentSwirlCorrelation = lastP.EntrainmentSwirlCorrelation,
                FinalContinuityResidualRelative = lastP.ContinuityResidualRelative,
                EntrainmentStepsLimitedBySwirlPassageCapacity = entrainmentPassageCapSteps
            };

        SwirlChamberDualPathDischargeResult? finalSplit = physicsSteps.Count > 0 ? physicsSteps[^1].DualPathDischarge : null;

        int nStepsPhys = physicsSteps.Count;
        double meanContRes = nStepsPhys > 0 ? sumContinuityResidualRelative / nStepsPhys : 0.0;
        var marchResiduals = new PhysicsResidualSummary
        {
            MaxChamberContinuityResidualRelative = maxContinuityResidualRelative,
            MeanChamberContinuityResidualRelative = meanContRes,
            MaxChamberAxialMomentumBudgetResidualRelative = maxAxialMomentumBudgetResidualRelative,
            MaxChamberAngularMomentumFluxClosureResidualRelative = maxAngularMomentumFluxClosureResidualRelative,
            ExitControlVolumeMassFluxResidualRelative = double.NaN
        };

        return new FlowMarchDetailedResult
        {
            FlowStates = states,
            StepResults = stepResults,
            StepPhysicsStates = physicsSteps,
            MarchInvariantWarnings = invariantSink ?? (IReadOnlyList<string>)Array.Empty<string>(),
            MarchClosure = closure,
            MarchResidualSummary = marchResiduals,
            FinalTangentialVelocityMps = finalVtMixed,
            FinalAxialVelocityMps = current.VelocityMps,
            FinalPrimaryTangentialVelocityMps = SwirlMath.BulkTangentialVelocityFromAngularMomentumFlux(
                angularMomentumFluxKgM2PerS2,
                primaryMdot,
                rRef),
            EntrainmentStepsLimitedBySwirlPassageCapacity = entrainmentPassageCapSteps,
            SumPrimaryPressureDrivenEntrainmentDemandKgS = sumPrimaryPressureDrivenEntrainmentDemandKgS,
            SumEntrainmentMassTrimmedByPassageGovernorKgS = sumEntrainmentTrimmedByGovernorKgS,
            EntrainmentGovernorMachMaxUsed = passageLim.EntrainmentGovernorMachMax,
            AppliedDischargePathSpec = dischargePathSpec,
            FinalChamberDischargeSplit = finalSplit
        };
    }

    /// <summary>Pressure-driven entrainment bottleneck: min of positive geometric areas supplied.</summary>
    private static double EffectiveEntrainmentEntryAreaM2(
        double captureAreaM2,
        double mixedAnnulusAreaM2,
        double chamberFullBoreAreaM2,
        double freeAnnulusAreaM2)
    {
        double a = Math.Max(captureAreaM2, 1e-18);
        a = Math.Min(a, Math.Max(mixedAnnulusAreaM2, 1e-18));
        if (chamberFullBoreAreaM2 > 1e-18)
            a = Math.Min(a, chamberFullBoreAreaM2);
        if (freeAnnulusAreaM2 > 1e-18)
            a = Math.Min(a, freeAnnulusAreaM2);
        return Math.Max(a, 1e-18);
    }
}
