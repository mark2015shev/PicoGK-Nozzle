using System;
using System.Collections.Generic;
using System.Linq;
using PicoGK_Run.Geometry;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Infrastructure.Pipeline;

/// <summary>Maps SI diagnostics + continuity into numeric penalty structs for the unified objective.</summary>
public static class PenaltyBreakdownBuilder
{
    public const double HealthPenaltyPerMessage = 0.04;

    public static PhysicsPenaltyBreakdown BuildPhysics(
        SiFlowDiagnostics? si,
        FlowTunePhysicsMetrics metrics,
        IReadOnlyList<string> healthMessages,
        int designErrorCount,
        double finalTotalMassFlowKgS = 0.0)
    {
        if (si == null)
        {
            return new PhysicsPenaltyBreakdown(
                0, 0, 0.5, 0.3, metrics.SeparationRisk01 * 0.4, 0.2, 0, 0.3,
                0.8, 0.2, 0.2, 0.3,
                HealthPenaltyPerMessage * Math.Max(healthMessages.Count, 0),
                CaptureBoundaryDeficitPenalty: metrics.CapturePressureDeficitWeakness01 * 0.35,
                InletSpillTendencyPenalty: metrics.BidirectionalSpillRisk01 * 0.28,
                SwirlContainmentPenalty: metrics.InletContainmentRisk01 * 0.24);
        }

        double massBal = 0.0;
        if (si.ConservationResiduals is { } crMarch)
        {
            double relCont = Math.Max(
                Math.Abs(si.MarchPhysicsClosure?.FinalContinuityResidualRelative ?? 0.0),
                crMarch.MaxChamberContinuityResidualRelative);
            massBal = Math.Clamp(relCont * 2.5, 0.0, 0.85);
            massBal = Math.Max(
                massBal,
                Math.Clamp(
                    crMarch.MaxChamberAxialMomentumBudgetResidualRelative
                    * PhysicsCalibrationHooks.AxialMomentumBudgetResidualPenaltyWeight
                    * 0.22,
                    0.0,
                    0.55));
            massBal = Math.Max(
                massBal,
                Math.Clamp(
                    crMarch.MaxChamberAngularMomentumFluxClosureResidualRelative
                    * PhysicsCalibrationHooks.AngularMomentumFluxClosurePenaltyWeight
                    * 0.22,
                    0.0,
                    0.55));
            double thr = PhysicsCalibrationHooks.ChamberContinuityResidualPenaltyThreshold;
            if (crMarch.MeanChamberContinuityResidualRelative > thr)
            {
                massBal = Math.Max(
                    massBal,
                    Math.Clamp(
                        (crMarch.MeanChamberContinuityResidualRelative - thr)
                        * PhysicsCalibrationHooks.ChamberContinuityResidualPenaltyWeight,
                        0.0,
                        0.35));
            }

            double pCons = crMarch.MaxChamberBulkPressureConsistencyResidualRelative;
            if (pCons > 0.006)
            {
                massBal = Math.Max(
                    massBal,
                    Math.Clamp(pCons * 14.0, 0.0, 0.42));
            }
        }
        else if (si.MarchPhysicsClosure != null)
        {
            double rel = Math.Abs(si.MarchPhysicsClosure.FinalContinuityResidualRelative);
            massBal = Math.Clamp(rel * 2.5, 0.0, 0.85);
        }

        double momBal = 0.0;
        if (!si.ThrustControlVolumeIsValid)
            momBal += 0.55;
        if (si.ConservationResiduals is { } crExit
            && !double.IsNaN(crExit.ExitControlVolumeMassFluxResidualRelative))
        {
            momBal += Math.Clamp(crExit.ExitControlVolumeMassFluxResidualRelative * 0.48, 0.0, 0.42);
        }
        double mdotExit = si.ThrustCvMdotExitKgS;
        if (mdotExit > 1e-9 && finalTotalMassFlowKgS > 1e-9)
        {
            double r = Math.Abs(mdotExit - finalTotalMassFlowKgS) / finalTotalMassFlowKgS;
            momBal += Math.Clamp(r * 0.4, 0.0, 0.35);
        }

        double contRes = massBal * 0.35;

        double choke = si.AnyEntrainmentStepChoked ? 0.45 : 0.0;

        double sep = Math.Clamp(metrics.SeparationRisk01, 0.0, 1.0) * 0.35;

        double residVt = Math.Abs(si.FinalTangentialVelocityMps);
        double exSwirl = Math.Clamp(residVt / 180.0, 0.0, 1.0) * 0.28;
        if (si.Chamber?.SwirlSegmentReport?.Containment is { } ctRm)
        {
            exSwirl = Math.Max(
                exSwirl,
                Math.Clamp(ctRm.ResidualChamberEndSwirlRatioVtOverVx / 3.8 * 0.32, 0.0, 0.38));
        }

        int capSteps = si.EntrainmentStepsLimitedBySwirlPassageCapacity;
        double govClip = capSteps > 0
            ? Math.Clamp(0.12 + 0.03 * capSteps, 0.12, 0.95)
            : 0.0;

        double shortfall = 0.0;
        if (si.SumRequestedEntrainmentIncrementsKgS > 1e-9)
        {
            double sf = si.EntrainmentShortfallSumKgS / si.SumRequestedEntrainmentIncrementsKgS;
            shortfall = Math.Clamp(sf, 0.0, 1.2) * 0.55;
        }

        double thrustInv = si.ThrustControlVolumeIsValid ? 0.0 : 0.6;

        double lowP = 0.0;
        if (si.PhysicsStepStates.Count > 0)
        {
            double pLast = si.PhysicsStepStates[^1].PStaticPa;
            if (pLast > 0 && pLast < 3_000.0)
                lowP = 0.28;
        }

        double machBand = 0.0;
        double machBulk = si.MarchPhysicsClosure?.FinalMachBulk ?? 0.0;
        if (machBulk > 0.92 || si.MaxInletMach > 0.98)
            machBand = 0.32;

        double capCls = 0.0;
        if (si.ChamberMarch?.SwirlEntranceCapacityStations is { } cap)
        {
            capCls = cap.CombinedClassification switch
            {
                SwirlEntranceCapacityClassification.Warning => 0.22,
                SwirlEntranceCapacityClassification.FailRestrictive => 0.55,
                SwirlEntranceCapacityClassification.FailChoking => 0.85,
                _ => 0.0
            };
        }

        int nonDesignHealth = healthMessages.Count(m => !m.StartsWith("DESIGN ERROR", StringComparison.Ordinal));
        double healthPen = HealthPenaltyPerMessage * nonDesignHealth + 0.35 * designErrorCount;

        double capDefPen = 0.0;
        double spillPen = 0.0;
        double containPen = 0.0;
        if (si.Chamber != null)
        {
            capDefPen = Math.Clamp(si.Chamber.CapturePressureDeficitWeakness01 * 0.38, 0.0, 0.55);
            spillPen = Math.Clamp((si.Chamber.SwirlSegmentReport?.Spill?.BidirectionalSpillRisk01 ?? 0.0) * 0.30, 0.0, 0.48);
            if (si.Chamber.SwirlSegmentReport?.Spill is { } spm)
            {
                spillPen += Math.Clamp(Math.Max(0.0, spm.InletSpillPressureMarginPa) / 11_000.0 * 0.22, 0.0, 0.28);
                spillPen += Math.Clamp(Math.Max(0.0, spm.ExitDrivePressureMarginPa) / 11_000.0 * 0.20, 0.0, 0.26);
            }

            containPen = Math.Clamp(
                (si.Chamber.SwirlSegmentReport?.Containment?.InletContainmentRisk01 ?? 0.0) * 0.28,
                0.0,
                0.45);
            if (si.Chamber.SwirlSegmentReport?.Containment is { } ctDev)
            {
                double devDef = ctDev.ChamberDevelopmentLengthRatio < 1.0
                    ? Math.Clamp(1.0 - ctDev.ChamberDevelopmentLengthRatio, 0.0, 1.0)
                    : 0.0;
                containPen += Math.Clamp(devDef * 0.26, 0.0, 0.32);
                containPen += Math.Clamp(ctDev.FreeAnnulusBlockageRatio * 0.18, 0.0, 0.22);
            }
        }

        return new PhysicsPenaltyBreakdown(
            massBal,
            momBal,
            contRes,
            choke,
            sep,
            exSwirl,
            govClip,
            shortfall,
            thrustInv,
            lowP,
            machBand,
            capCls,
            healthPen,
            capDefPen,
            spillPen,
            containPen);
    }

    /// <summary>
    /// Downstream mismatch: normalized |R_nominal_cone − R_declared_exit| / R_chamber (template inconsistency),
    /// plus infeasible / length-clamp surcharges so autotune avoids overshoot-and-correct designs.
    /// </summary>
    public const double DownstreamMismatchScale = 0.95;

    public static GeometryPenaltyBreakdown BuildGeometry(
        GeometryContinuityReport? report,
        DownstreamGeometryTargets? downstream = null,
        RunConfiguration? run = null,
        NozzleDesignInputs? designForSwirlPlacement = null)
    {
        double contPen = 0.0;
        int n = 0;
        if (report is { IsAcceptable: false })
        {
            n = report.Issues.Count;
            contPen = Math.Clamp(0.15 * n, 0.15, 1.2);
        }

        double dMis = 0.0;
        if (downstream != null)
        {
            double rCh = Math.Max(downstream.ChamberInnerRadiusMm, 1e-3);
            // Constant-area mode: penalize template (nominal L + angle) vs declared exit — drives self-consistent CAD.
            if (run?.EnablePostStatorExitTaper != true)
            {
                double normConeExit =
                    Math.Abs(downstream.NominalConeOutletInnerRadiusMm - downstream.DeclaredExitInnerRadiusMm) / rCh;
                dMis += DownstreamMismatchScale * Math.Clamp(normConeExit, 0.0, 1.8);
            }

            if (downstream.ConeCannotReachDeclaredExit)
                dMis += 0.85;
            if (downstream.ExpanderLengthClampedToMax)
            {
                double normBuilt =
                    Math.Abs(downstream.RecoveryAnnulusRadiusMm - downstream.DeclaredExitInnerRadiusMm) / rCh;
                dMis += 0.55 * Math.Clamp(normBuilt, 0.0, 1.2);
            }
        }

        double overPen = 0.0;
        if (designForSwirlPlacement != null)
        {
            GeometryAssemblyPath path = GeometryAssemblyPath.Compute(designForSwirlPlacement, run);
            overPen = SwirlChamberPlacement.ComputeUpstreamOvershootPenalty(path.SwirlPlacement.ChamberUpstreamOvershootMm);
        }

        return new GeometryPenaltyBreakdown(contPen, n, dMis, overPen);
    }

    public static ConstraintViolationBreakdown BuildConstraints(
        SiFlowDiagnostics? si,
        GeometryContinuityReport? continuity,
        bool hasDesignError,
        IReadOnlyList<string> healthMessages,
        double finalTotalMassFlowKgS = 0.0,
        DownstreamGeometryTargets? downstream = null,
        RunConfiguration? run = null,
        NozzleDesignInputs? designForSwirlPlacement = null)
    {
        var reasons = new List<string>();
        if (hasDesignError)
            reasons.Add("DESIGN_ERROR");
        if (si != null)
        {
            if (!double.IsFinite(si.NetThrustN) || !double.IsFinite(finalTotalMassFlowKgS))
                reasons.Add("NON_FINITE_SI");
            if (si.ChamberMarch?.SwirlEntranceCapacityStations?.CombinedClassification
                == SwirlEntranceCapacityClassification.FailChoking)
                reasons.Add("CAPACITY_FAIL_CHOKING");
            if (si.AnyEntrainmentStepChoked && si.MaxInletMach > 0.99)
                reasons.Add("SEVERE_CHOKING");
        }
        if (continuity is { IsAcceptable: false })
            reasons.Add("GEOMETRY_CONTINUITY_FAIL");

        if (downstream?.ConeCannotReachDeclaredExit == true && (run?.HardRejectInfeasibleDownstreamCone ?? true))
            reasons.Add("DOWNSTREAM_CONE_INFEASIBLE");

        if (designForSwirlPlacement != null && run != null)
        {
            GeometryAssemblyPath path = GeometryAssemblyPath.Compute(designForSwirlPlacement, run);
            double hard = run.SwirlChamberUpstreamOvershootHardRejectMm;
            if (path.SwirlPlacement.ChamberUpstreamOvershootMm > hard)
                reasons.Add("CHAMBER_UPSTREAM_OVERSHOOT");
        }

        return reasons.Count > 0
            ? new ConstraintViolationBreakdown(true, reasons)
            : ConstraintViolationBreakdown.Ok;
    }
}
