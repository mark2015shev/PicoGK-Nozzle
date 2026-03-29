using System;
using System.Collections.Generic;
using PicoGK_Run.Core;
using PicoGK_Run.Geometry;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Physics.Continuous;

/// <summary>
/// Builds one continuous station table: chamber states come from the existing SI march; expander integrates
/// Ġ_θ transport, radial wall pressure, and axial wall force without resetting at the chamber boundary.
/// Stator/exit rows reflect the same stator recovery outputs already computed on the SI path.
/// </summary>
public static class ContinuousNozzleSolver
{
    public const int DefaultExpanderSubsteps = 16;

    public static ContinuousNozzleSolution Solve(
        GeometryAssemblyPath pathMm,
        NozzleDesignInputs design,
        SourceInputs source,
        FlowMarchDetailedResult chamberMarch,
        JetState chamberMarchInlet,
        double injectorTangentialVelocityMps,
        double injectorAxialVelocityMps,
        double minInletStaticPressurePa,
        GasProperties gas,
        ReducedOrderClosureCoefficients? closure,
        SwirlDiffuserRecoveryResult expanderLumpedReference,
        StatorRecoveryOutput statorOut,
        double axialVelocityAfterStatorMps,
        double tangentialVelocityAfterStatorMps,
        double staticPressureAfterStatorPa,
        double densityAfterStatorKgM3,
        double staticTemperatureAfterStatorK,
        double totalTemperatureReferenceK,
        JetState finalOutlet,
        int expanderSubsteps = DefaultExpanderSubsteps)
    {
        closure ??= ReducedOrderClosureCoefficients.Default;
        var stations = new List<ContinuousPathStation>(64);
        double cumWallForce = 0.0;
        double cumEntrained = 0.0;
        double cumMomFluxGain = 0.0;
        double halfAngleDeg = design.ExpanderHalfAngleDeg;
        double rHubMm = 0.5 * Math.Max(design.StatorHubDiameterMm, 0.0);
        double rChamberWallMm = 0.5 * Math.Max(design.SwirlChamberDiameterMm, 1e-6);
        double rExitWallMm = Math.Max(pathMm.ExpanderEndInnerRadiusMm, 0.5);
        double ambientP = Math.Max(source.AmbientPressurePa, 1.0);
        double g = GasProperties.Gamma;
        double cp = gas.SpecificHeatCp;
        double rGas = GasProperties.R;
        expanderSubsteps = Math.Clamp(expanderSubsteps, 4, 64);

        double p0Ref = chamberMarch.StepPhysicsStates.Count > 0
            ? chamberMarch.StepPhysicsStates[0].PTotalPa
            : chamberMarchInlet.PressurePa * 1.05;

        // --- Inlet / injector coupled row ---
        {
            double xMmIn = pathMm.XInjectorPlane;
            double mCoreIn = chamberMarchInlet.MassFlowKgS;
            double mEntIn = chamberMarchInlet.EntrainedMassFlowKgS;
            double mTotIn = mCoreIn + mEntIn;
            double rhoIn = Math.Max(chamberMarchInlet.DensityKgM3, 1e-9);
            double pIn = chamberMarchInlet.PressurePa;
            double tIn = Math.Max(chamberMarchInlet.TemperatureK, 1.0);
            double vaIn = injectorAxialVelocityMps;
            double vtIn = injectorTangentialVelocityMps;
            double rMeanMmIn = 0.5 * (rChamberWallMm + rHubMm);
            double rMeanMIn = rMeanMmIn * 1e-3;
            double lThetaIn = mTotIn * rMeanMIn * vtIn;
            double vmIn = Math.Sqrt(vaIn * vaIn + vtIn * vtIn);
            double dhIn = HydraulicDiameterAnnulusM(rChamberWallMm, rHubMm);
            double muIn = gas.DynamicViscosityAirPaS(tIn);
            double reIn = rhoIn * vmIn * dhIn / Math.Max(muIn, 1e-12);
            double pCap = closure.InletGlobalCouplingPressureGain * Math.Max(0.0, ambientP - minInletStaticPressurePa);
            var radialIn = RadialVortexPressureModel.ComputeShapingRelativeToBulk(
                pIn,
                Math.Max(p0Ref, pIn + 1.0),
                rhoIn,
                Math.Abs(vtIn),
                rChamberWallMm * 1e-3,
                ChamberPhysicsCoefficients.RadialCoreRadiusFractionOfWall,
                ChamberPhysicsCoefficients.RadialPressureCapPa,
                ambientP);
            double pWallIn = SiPressureGuards.ClampStaticPressurePa(pIn + radialIn.WallPressureRisePa);
            var geoIn = MkGeo(pathMm.XInjectorPlane, NozzleSegmentKind.InletCoupled, rChamberWallMm, rHubMm, halfAngleDeg, true, false, design.StatorVaneAngleDeg, 0.0);
            stations.Add(new ContinuousPathStation(geoIn, new FlowState
            {
                XMm = xMmIn,
                Segment = NozzleSegmentKind.InletCoupled,
                MassFlowCoreKgS = mCoreIn,
                MassFlowEntrainedKgS = mEntIn,
                MassFlowTotalKgS = mTotIn,
                DensityKgM3 = rhoIn,
                StaticPressurePa = pIn,
                TotalPressurePa = p0Ref,
                StaticTemperatureK = tIn,
                TotalTemperatureK = totalTemperatureReferenceK,
                VAxialMps = vaIn,
                VTangentialMps = vtIn,
                Mach = gas.MachNumber(vmIn, tIn),
                Reynolds = reIn,
                AngularMomentumFluxKgM2PerS2 = lThetaIn,
                SwirlMetric = SwirlMath.SwirlCorrelationForEntrainment(lThetaIn, mTotIn * vaIn, mTotIn, rMeanMIn, vmIn, vtIn),
                WallPressurePa = pWallIn,
                CapturePressurePa = Math.Max(ambientP - pCap, 1.0),
                DeltaPRadialPa = radialIn.EstimatedRadialPressureDeltaPa,
                CumulativeEntrainedKgS = mEntIn,
                SwirlEnergyPreservedBucketW = 0.5 * mTotIn * vtIn * vtIn
            }));
        }

        // --- Chamber march (authoritative) ---
        IReadOnlyList<FlowStepState> phys = chamberMarch.StepPhysicsStates;
        IReadOnlyList<FlowMarchStepResult> stepRes = chamberMarch.StepResults;
        for (int i = 0; i < phys.Count; i++)
        {
            FlowStepState s = phys[i];
            double xMm = pathMm.XSwirlStart + s.X * 1000.0;
            double rWallMm = rChamberWallMm;
            double geoArea = s.AreaM2;
            double pWet = s.WettedPerimeterM;
            double dh = 4.0 * geoArea / Math.Max(pWet, 1e-9);
            double rMeanMm = 0.5 * (rWallMm + rHubMm);
            double dmEnt = i == 0 ? s.MdotSecondaryKgS : s.MdotSecondaryKgS - phys[i - 1].MdotSecondaryKgS;
            cumEntrained += Math.Max(0.0, dmEnt);
            double momPrev = i == 0 ? phys[0].MdotTotalKgS * phys[0].VAxialMps : phys[i - 1].MdotTotalKgS * phys[i - 1].VAxialMps;
            cumMomFluxGain += s.MdotTotalKgS * s.VAxialMps - momPrev;

            double dF = i < stepRes.Count ? stepRes[i].PressureForceN : 0.0;
            cumWallForce += dF;

            var geo = new GeometryStation(
                xMm,
                NozzleSegmentKind.SwirlChamber,
                InnerRadiusMm: rHubMm,
                OuterGasRadiusMm: rWallMm,
                FlowAreaM2: geoArea,
                HydraulicDiameterM: dh,
                WettedPerimeterM: pWet,
                MeanRadiusMm: rMeanMm,
                DAreaDxPerM: 0.0,
                DInnerRadiusDx: 0.0,
                WallHalfAngleDeg: 0.0,
                WallSlopeSign: 0,
                LocalWallAreaIncrementM2: 0.0,
                CaptureEligible: true,
                StatorPresent: false,
                VaneAngleDeg: design.StatorVaneAngleDeg);

            double vtCh = s.VTangentialMps;
            stations.Add(new ContinuousPathStation(geo, new FlowState
            {
                XMm = xMm,
                Segment = NozzleSegmentKind.SwirlChamber,
                MassFlowCoreKgS = s.MdotPrimaryKgS,
                MassFlowEntrainedKgS = s.MdotSecondaryKgS,
                MassFlowTotalKgS = s.MdotTotalKgS,
                DensityKgM3 = s.DensityKgM3,
                StaticPressurePa = s.PStaticPa,
                TotalPressurePa = s.PTotalPa,
                StaticTemperatureK = s.TStaticK,
                TotalTemperatureK = s.TTotalK,
                VAxialMps = s.VAxialMps,
                VTangentialMps = vtCh,
                Mach = s.Mach,
                Reynolds = s.Reynolds,
                AngularMomentumFluxKgM2PerS2 = s.AngularMomentumFluxKgM2PerS2,
                SwirlMetric = s.SwirlNumberFlux,
                WallPressurePa = s.WallPressurePa,
                CapturePressurePa = s.CaptureBoundaryStaticPressureForEntrainmentPa,
                DeltaPRadialPa = s.RadialPressureDeltaPa,
                AxialWallForceIncrementN = dF,
                CumulativeWallForceN = cumWallForce,
                EntrainmentIncrementKgS = dmEnt,
                CumulativeEntrainedKgS = cumEntrained,
                CumulativeAxialMomentumFluxGainKgM2PerS2 = cumMomFluxGain,
                MixingLossPressurePa = s.TotalPressureLossStepPa,
                SwirlEnergyPreservedBucketW = 0.5 * s.MdotTotalKgS * vtCh * vtCh
            }));
        }

        if (phys.Count == 0)
        {
            return new ContinuousNozzleSolution
            {
                Stations = stations,
                Energy = new SwirlEnergyAccountingBuckets(),
                ExpanderWallForceIntegratedN = 0.0,
                ExpanderWallForceLumpedReferenceN = expanderLumpedReference.ExpanderWallAxialForceFromPressureN,
                ClosuresUsed = closure,
                SegmentBoundaryAuditLines = new[] { "No chamber march steps — expander chain skipped." }
            };
        }

        FlowStepState lastCh = phys[^1];
        double m = lastCh.MdotTotalKgS;
        double t0 = Math.Max(lastCh.TTotalK, lastCh.TStaticK);
        double p0 = Math.Max(lastCh.PTotalPa, lastCh.PStaticPa + 1.0);
        double rMeanExitMm = 0.5 * (rChamberWallMm + rHubMm);
        double rMeanExitM = rMeanExitMm * 1e-3;
        double lTheta = m * rMeanExitM * lastCh.VTangentialMps;
        double vx = lastCh.VAxialMps;
        double vt = lastCh.VTangentialMps;
        double p = lastCh.PStaticPa;
        double rho = Math.Max(lastCh.DensityKgM3, 1e-9);

        double xExp0 = pathMm.XExpanderStart;
        double xExp1 = pathMm.XAfterExpander;
        double lenMm = Math.Max(xExp1 - xExp0, 1e-6);
        double dxM = lenMm * 1e-3 / expanderSubsteps;

        double expanderWallIntegrated = 0.0;
        double sumFrictionDp = 0.0;
        double sumMixSepDp = 0.0;
        double sumSwirlReliefDp = 0.0;
        double vtPrevSub = lastCh.VTangentialMps;
        double vaChamberExit = lastCh.VAxialMps;

        for (int k = 0; k < expanderSubsteps; k++)
        {
            double f0 = k / (double)expanderSubsteps;
            double f1 = (k + 1) / (double)expanderSubsteps;
            double xMm0 = xExp0 + f0 * lenMm;
            double xMm1 = xExp0 + f1 * lenMm;
            double rWall0Mm = rChamberWallMm + f0 * (rExitWallMm - rChamberWallMm);
            double rWall1Mm = rChamberWallMm + f1 * (rExitWallMm - rChamberWallMm);
            double rMean0M = 0.5e-3 * (rWall0Mm + rHubMm);
            double rMean1M = 0.5e-3 * (rWall1Mm + rHubMm);

            // Ġ_θ: spread with radius (conserves L when Vt ∝ 1/r at fixed m), then explicit closures.
            lTheta *= rMean0M / Math.Max(rMean1M, 1e-9);
            double dh = HydraulicDiameterAnnulusM(rWall1Mm, rHubMm);
            double lWallLoss = closure.SwirlWallFrictionCoeff * (dxM / Math.Max(dh, 1e-9)) * Math.Abs(lTheta);
            double lMixLoss = closure.MixingDecayCoeff * (dxM / Math.Max(lenMm * 1e-3, 1e-9)) * Math.Abs(lTheta);
            lTheta = Math.Sign(lTheta) * Math.Max(Math.Abs(lTheta) - lWallLoss - lMixLoss, 0.0);
            vt = lTheta / (m * Math.Max(rMean1M, 1e-9));

            double a1 = AnnulusAreaM2(rWall0Mm, rHubMm);
            double a2 = AnnulusAreaM2(rWall1Mm, rHubMm);
            double arRatio = a1 / Math.Max(a2, 1e-12);

            // Swirl deceleration → static pressure (explicit closure, not separate energy).
            double dpSwirlRelief = closure.SwirlReliefToStaticPressureCoeff * 0.5 * rho * Math.Max(0.0, vtPrevSub * vtPrevSub - vt * vt);
            sumSwirlReliefDp += Math.Max(0.0, dpSwirlRelief);
            vtPrevSub = vt;

            double q = 0.5 * rho * vx * vx;
            double dpFric = closure.DiffuserWallFrictionFactor * (dxM / Math.Max(dh, 1e-9)) * q;
            double sep = expanderLumpedReference.SeparationRiskScore;
            double dpSep = closure.DiffuserSeparationLossCoeff * sep * q * (dxM / Math.Max(lenMm * 1e-3, 1e-9));
            sumFrictionDp += dpFric;
            sumMixSepDp += dpSep;

            // Losses applied once per substep; inner loop couples area change to isentropic bulk (fixed T₀).
            double pBase = SiPressureGuards.ClampStaticPressurePa(p + dpSwirlRelief - dpFric - dpSep);
            for (int it = 0; it < 6; it++)
            {
                double vm = Math.Sqrt(vx * vx + vt * vt);
                double tStatic = Math.Max(t0 - vm * vm / (2.0 * cp), 1.0);
                double pIsen = p0 * Math.Pow(tStatic / Math.Max(t0, 1.0), g / (g - 1.0));
                double pIdealRecovery = closure.DiffuserIdealRecoveryEfficiency * (pIsen - pBase) * Math.Clamp(1.0 - arRatio, -0.5, 0.85);
                p = SiPressureGuards.ClampStaticPressurePa(pBase + pIdealRecovery);
                rho = Math.Max(p / (rGas * Math.Max(tStatic, 1.0)), 1e-6);
                vx = m / (rho * Math.Max(a2, 1e-12));
            }

            double vmFinal = Math.Sqrt(vx * vx + vt * vt);
            double tStat = Math.Max(t0 - vmFinal * vmFinal / (2.0 * cp), 1.0);
            double mu = gas.DynamicViscosityAirPaS(tStat);
            double re = rho * vmFinal * dh / Math.Max(mu, 1e-12);

            var radial = RadialVortexPressureModel.ComputeShapingRelativeToBulk(
                p,
                p0,
                rho,
                Math.Abs(vt),
                rWall1Mm * 1e-3,
                ChamberPhysicsCoefficients.RadialCoreRadiusFractionOfWall,
                ChamberPhysicsCoefficients.RadialPressureCapPa,
                ambientP);
            double pWall = SiPressureGuards.ClampStaticPressurePa(p + radial.WallPressureRisePa);

            double drM = (rWall1Mm - rWall0Mm) * 1e-3;
            double rMidM = 0.5e-3 * (rWall0Mm + rWall1Mm);
            // First-principles projection: dF_ax ≈ 2π r p_wall dr on axisymmetric diverging wall (see derivation in user spec).
            double dFwall = pWall * (2.0 * Math.PI * rMidM * drM);
            expanderWallIntegrated += dFwall;
            cumWallForce += dFwall;

            double dArea = a2 - a1;
            var geoE = new GeometryStation(
                xMm1,
                NozzleSegmentKind.Expander,
                InnerRadiusMm: rHubMm,
                OuterGasRadiusMm: rWall1Mm,
                FlowAreaM2: a2,
                HydraulicDiameterM: dh,
                WettedPerimeterM: AnnulusWettedPerimeterM(rWall1Mm, rHubMm),
                MeanRadiusMm: 0.5 * (rWall1Mm + rHubMm),
                DAreaDxPerM: dArea / Math.Max(dxM, 1e-12),
                DInnerRadiusDx: (rWall1Mm - rWall0Mm) / Math.Max(xMm1 - xMm0, 1e-9),
                WallHalfAngleDeg: halfAngleDeg,
                WallSlopeSign: 1,
                LocalWallAreaIncrementM2: 2.0 * Math.PI * rMidM * Math.Sqrt(dxM * dxM + drM * drM),
                CaptureEligible: closure.ExpanderEntrainmentCaptureCoeff > 1e-9,
                StatorPresent: false,
                VaneAngleDeg: design.StatorVaneAngleDeg);

            stations.Add(new ContinuousPathStation(geoE, new FlowState
            {
                XMm = xMm1,
                Segment = NozzleSegmentKind.Expander,
                MassFlowCoreKgS = lastCh.MdotPrimaryKgS,
                MassFlowEntrainedKgS = lastCh.MdotSecondaryKgS,
                MassFlowTotalKgS = m,
                DensityKgM3 = rho,
                StaticPressurePa = p,
                TotalPressurePa = p0,
                StaticTemperatureK = tStat,
                TotalTemperatureK = t0,
                VAxialMps = vx,
                VTangentialMps = vt,
                Mach = gas.MachNumber(vmFinal, tStat),
                Reynolds = re,
                AngularMomentumFluxKgM2PerS2 = lTheta,
                SwirlMetric = SwirlMath.SwirlCorrelationForEntrainment(lTheta, m * vx, m, rMean1M, vmFinal, vt),
                WallPressurePa = pWall,
                CapturePressurePa = p,
                DeltaPRadialPa = radial.EstimatedRadialPressureDeltaPa,
                AxialWallForceIncrementN = dFwall,
                CumulativeWallForceN = cumWallForce,
                CumulativeEntrainedKgS = cumEntrained,
                CumulativeAxialMomentumFluxGainKgM2PerS2 = cumMomFluxGain + (m * vx - m * vaChamberExit),
                FrictionLossPressurePa = sumFrictionDp,
                MixingLossPressurePa = sumMixSepDp,
                SwirlEnergyPreservedBucketW = 0.5 * m * vt * vt,
                SwirlToWallForceBucketW = dFwall * Math.Max(Math.Abs(vx), 1e-6)
            }));
        }

        // --- Stator inlet (expander outlet, explicit handoff) ---
        double xStatorMid = 0.5 * (pathMm.XStatorStart + pathMm.XAfterStator);
        {
            FlowState lastExp = stations[^1].Flow;
            var geoStIn = MkGeo(xStatorMid - 0.25 * pathMm.StatorAxialLengthMm, NozzleSegmentKind.Stator, rExitWallMm, rHubMm, 0.0, false, true, design.StatorVaneAngleDeg, 0.0);
            stations.Add(new ContinuousPathStation(geoStIn, new FlowState
            {
                XMm = geoStIn.XMm,
                Segment = NozzleSegmentKind.Stator,
                MassFlowCoreKgS = lastExp.MassFlowCoreKgS,
                MassFlowEntrainedKgS = lastExp.MassFlowEntrainedKgS,
                MassFlowTotalKgS = lastExp.MassFlowTotalKgS,
                DensityKgM3 = lastExp.DensityKgM3,
                StaticPressurePa = lastExp.StaticPressurePa,
                TotalPressurePa = lastExp.TotalPressurePa,
                StaticTemperatureK = lastExp.StaticTemperatureK,
                TotalTemperatureK = lastExp.TotalTemperatureK,
                VAxialMps = lastExp.VAxialMps,
                VTangentialMps = lastExp.VTangentialMps,
                Mach = lastExp.Mach,
                Reynolds = lastExp.Reynolds,
                AngularMomentumFluxKgM2PerS2 = lastExp.AngularMomentumFluxKgM2PerS2,
                SwirlMetric = lastExp.SwirlMetric,
                WallPressurePa = lastExp.WallPressurePa,
                SwirlEnergyPreservedBucketW = lastExp.SwirlEnergyPreservedBucketW,
                CumulativeWallForceN = cumWallForce,
                CumulativeEntrainedKgS = cumEntrained
            }));
        }

        // --- Stator outlet (post recovery model) ---
        {
            var geoStOut = MkGeo(xStatorMid + 0.25 * pathMm.StatorAxialLengthMm, NozzleSegmentKind.Stator, rExitWallMm, rHubMm, 0.0, false, true, design.StatorVaneAngleDeg, 0.0);
            double vm = Math.Sqrt(axialVelocityAfterStatorMps * axialVelocityAfterStatorMps + tangentialVelocityAfterStatorMps * tangentialVelocityAfterStatorMps);
            double mu = gas.DynamicViscosityAirPaS(staticTemperatureAfterStatorK);
            double dh = HydraulicDiameterAnnulusM(rExitWallMm, rHubMm);
            double re = densityAfterStatorKgM3 * vm * dh / Math.Max(mu, 1e-12);
            double powerStator = Math.Max(0.0, statorOut.RecoveredPressureRisePa / Math.Max(densityAfterStatorKgM3, 1e-9)) * m * 0.15
                + m * Math.Max(0.0, statorOut.AxialVelocityGainMps * axialVelocityAfterStatorMps);
            stations.Add(new ContinuousPathStation(geoStOut, new FlowState
            {
                XMm = geoStOut.XMm,
                Segment = NozzleSegmentKind.Stator,
                MassFlowCoreKgS = lastCh.MdotPrimaryKgS,
                MassFlowEntrainedKgS = lastCh.MdotSecondaryKgS,
                MassFlowTotalKgS = m,
                DensityKgM3 = densityAfterStatorKgM3,
                StaticPressurePa = staticPressureAfterStatorPa,
                TotalPressurePa = p0,
                StaticTemperatureK = staticTemperatureAfterStatorK,
                TotalTemperatureK = totalTemperatureReferenceK,
                VAxialMps = axialVelocityAfterStatorMps,
                VTangentialMps = tangentialVelocityAfterStatorMps,
                Mach = gas.MachNumber(vm, staticTemperatureAfterStatorK),
                Reynolds = re,
                AngularMomentumFluxKgM2PerS2 = m * (0.5e-3 * (rExitWallMm + rHubMm)) * tangentialVelocityAfterStatorMps,
                SwirlToAxialBucketW = m * statorOut.AxialVelocityGainMps * Math.Max(axialVelocityAfterStatorMps, 1e-6),
                CumulativeWallForceN = cumWallForce,
                CumulativeEntrainedKgS = cumEntrained,
                SwirlEnergyPreservedBucketW = 0.5 * m * tangentialVelocityAfterStatorMps * tangentialVelocityAfterStatorMps
            }));
        }

        // --- Exit plane ---
        {
            double xMm = pathMm.XAfterExit;
            double rWall = Math.Max(pathMm.ExitInnerRadiusEndMm, rExitWallMm);
            var geoX = MkGeo(xMm, NozzleSegmentKind.Exit, rHubMm, rWall, 0.0, false, false, design.StatorVaneAngleDeg, 0.0);
            double aExit = Math.Max(finalOutlet.AreaM2, AnnulusAreaM2(rWall, rHubMm));
            double vm = Math.Abs(finalOutlet.VelocityMps);
            double t = Math.Max(finalOutlet.TemperatureK, 1.0);
            double mu = gas.DynamicViscosityAirPaS(t);
            double dh = 4.0 * aExit / Math.Max(AnnulusWettedPerimeterM(rWall, rHubMm), 1e-12);
            double re = finalOutlet.DensityKgM3 * vm * dh / Math.Max(mu, 1e-12);
            stations.Add(new ContinuousPathStation(geoX, new FlowState
            {
                XMm = xMm,
                Segment = NozzleSegmentKind.Exit,
                MassFlowCoreKgS = finalOutlet.MassFlowKgS,
                MassFlowEntrainedKgS = finalOutlet.EntrainedMassFlowKgS,
                MassFlowTotalKgS = finalOutlet.TotalMassFlowKgS,
                DensityKgM3 = finalOutlet.DensityKgM3,
                StaticPressurePa = finalOutlet.PressurePa,
                StaticTemperatureK = t,
                TotalTemperatureK = totalTemperatureReferenceK,
                VAxialMps = finalOutlet.VelocityMps,
                VTangentialMps = tangentialVelocityAfterStatorMps,
                Mach = gas.MachNumber(vm, t),
                Reynolds = re,
                CumulativeWallForceN = cumWallForce,
                CumulativeEntrainedKgS = cumEntrained,
                SwirlEnergyPreservedBucketW = 0.5 * finalOutlet.TotalMassFlowKgS * tangentialVelocityAfterStatorMps * tangentialVelocityAfterStatorMps
            }));
        }

        double md = Math.Max(finalOutlet.TotalMassFlowKgS, 1e-12);
        double vtExit = tangentialVelocityAfterStatorMps;
        double powerPreserved = 0.5 * md * vtExit * vtExit;
        double powerAxial = m * statorOut.AxialVelocityGainMps * Math.Max(axialVelocityAfterStatorMps, 1e-6);
        double powerWall = expanderWallIntegrated * Math.Max(Math.Abs(vx), 1e-6);
        double powerStatorBook = statorOut.RecoveredPressureRisePa * m / Math.Max(densityAfterStatorKgM3, 1e-9) * 0.2;

        var energy = new SwirlEnergyAccountingBuckets
        {
            PreservedSwirlPowerW = powerPreserved,
            ToAxialPowerW = powerAxial,
            ToWallThrustPowerW = powerWall,
            MixingDissipationPowerW = Math.Max(0.0, sumMixSepDp * m / Math.Max(rho, 1e-9)),
            FrictionDissipationPowerW = Math.Max(0.0, sumFrictionDp * m / Math.Max(rho, 1e-9)),
            StatorRecoveryPowerW = powerStatorBook
        };

        var audit = new List<string>
        {
            "--- Continuous path boundary audit ---",
            $"  Chamber exit → expander: ṁ={lastCh.MdotTotalKgS:F5} kg/s, V_a={lastCh.VAxialMps:F2} m/s, V_t={lastCh.VTangentialMps:F2} m/s, P={lastCh.PStaticPa:F1} Pa",
            $"  Expander outlet → stator:  V_a={vx:F2} m/s, V_t={vt:F2} m/s, P={p:F1} Pa (last expander step)",
            $"  Stator model: ΔP_rec={statorOut.RecoveredPressureRisePa:F1} Pa, ΔV_a={statorOut.AxialVelocityGainMps:F3} m/s, V_t_out={tangentialVelocityAfterStatorMps:F2} m/s",
            $"  Expander wall force: integrated={expanderWallIntegrated:F3} N | lumped ref={expanderLumpedReference.ExpanderWallAxialForceFromPressureN:F3} N"
        };

        return new ContinuousNozzleSolution
        {
            Stations = stations,
            Energy = energy,
            ExpanderWallForceIntegratedN = expanderWallIntegrated,
            ExpanderWallForceLumpedReferenceN = expanderLumpedReference.ExpanderWallAxialForceFromPressureN,
            ClosuresUsed = closure,
            SegmentBoundaryAuditLines = audit
        };
    }

    private static GeometryStation MkGeo(
        double xMm,
        NozzleSegmentKind seg,
        double outerRmm,
        double hubRmm,
        double wallHalfAngleDeg,
        bool capture,
        bool stator,
        double vaneDeg,
        double localWallDArea)
    {
        double a = AnnulusAreaM2(outerRmm, hubRmm);
        double pWet = AnnulusWettedPerimeterM(outerRmm, hubRmm);
        double dh = 4.0 * a / Math.Max(pWet, 1e-12);
        double rMean = 0.5 * (outerRmm + hubRmm);
        int slope = wallHalfAngleDeg > 0.5 ? 1 : 0;
        double drDx = wallHalfAngleDeg > 0.5 ? Math.Tan(wallHalfAngleDeg * (Math.PI / 180.0)) : 0.0;
        return new GeometryStation(
            xMm,
            seg,
            hubRmm,
            outerRmm,
            a,
            dh,
            pWet,
            rMean,
            DAreaDxPerM: 0.0,
            DInnerRadiusDx: drDx,
            WallHalfAngleDeg: wallHalfAngleDeg,
            WallSlopeSign: slope,
            LocalWallAreaIncrementM2: localWallDArea,
            capture,
            stator,
            vaneDeg);
    }

    private static double AnnulusAreaM2(double outerRmm, double hubRmm)
    {
        double ro = Math.Max(outerRmm, 0.0) * 1e-3;
        double ri = Math.Max(hubRmm, 0.0) * 1e-3;
        return Math.Max(Math.PI * (ro * ro - ri * ri), 1e-10);
    }

    private static double AnnulusWettedPerimeterM(double outerRmm, double hubRmm)
    {
        double ro = Math.Max(outerRmm, 0.0) * 1e-3;
        double ri = Math.Max(hubRmm, 0.0) * 1e-3;
        return 2.0 * Math.PI * (ro + ri);
    }

    private static double HydraulicDiameterAnnulusM(double outerRmm, double hubRmm)
    {
        double a = AnnulusAreaM2(outerRmm, hubRmm);
        double p = AnnulusWettedPerimeterM(outerRmm, hubRmm);
        return 4.0 * a / Math.Max(p, 1e-12);
    }
}
