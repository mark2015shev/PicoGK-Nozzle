using System;
using PicoGK_Run.Physics;
using PicoGK_Run.Infrastructure;

namespace PicoGK_Run.Infrastructure.PhysicsAutotune;

/// <summary>
/// Score = thrust_term × axial_transport × stator_recovery × useful_entrainment − penalties.
/// All terms explicit; weights from <see cref="PhysicsAutotuneScoreWeights"/>.
/// </summary>
public static class PhysicsAutotuneScoring
{
    public static (double Score, AutoTuneScoreBreakdown Breakdown) Compute(
        FlowTuneEvaluation candidate,
        FlowTuneEvaluation baseline,
        PhysicsAutotuneScoreWeights w)
    {
        if (candidate.HasDesignError)
        {
            var bad = new AutoTuneScoreBreakdown
            {
                InvalidStatePenalty = w.InvalidStatePenaltyWeight,
                PenaltiesSum = w.InvalidStatePenaltyWeight,
                FinalScore = -1e9
            };
            return (bad.FinalScore, bad);
        }

        var si = candidate.SiDiagnostics;
        double invalid01 = 0.0;
        if (!double.IsFinite(candidate.NetThrustN) || !double.IsFinite(candidate.EntrainmentRatio))
            invalid01 += 1.0;
        if (si != null)
        {
            if (!si.ThrustControlVolumeIsValid)
                invalid01 += 1.0;
            if (si.MinInletLocalStaticPressurePa < SiPressureGuards.MinStaticPressurePa)
                invalid01 += 0.35;
            double machBulk = si.MarchPhysicsClosure?.FinalMachBulk ?? 0.0;
            if (machBulk > 0.92 || si.MaxInletMach > 0.98)
                invalid01 += 0.35;
            if (si.ChamberMarch?.SwirlEntranceCapacityStations?.CombinedClassification == SwirlEntranceCapacityClassification.FailChoking)
                invalid01 += 0.45;
            else if (si.ChamberMarch?.SwirlEntranceCapacityStations?.CombinedClassification == SwirlEntranceCapacityClassification.FailRestrictive)
                invalid01 += 0.22;
            if (si.PhysicsStepStates.Count > 0)
            {
                double pLast = si.PhysicsStepStates[^1].PStaticPa;
                if (pLast > 0 && pLast < 3_000.0)
                    invalid01 += 0.25;
            }
        }
        else
            invalid01 += 0.4;

        double f0 = Math.Max(Math.Abs(baseline.NetThrustN), 1.0);
        double thrustTerm = Math.Pow(Math.Max(candidate.NetThrustN, 0.0) / f0, Math.Clamp(w.ThrustExponent, 0.2, 1.5));

        double vaBase = BaselineExitAxialMps(baseline);
        double vaExit = Math.Abs(si?.FinalAxialVelocityMps ?? 0.0);
        double axialTransportTerm = Math.Pow(
            Math.Max(vaExit, 1e-6) / Math.Max(vaBase, 1e-6),
            Math.Clamp(w.AxialTransportExponent, 0.15, 1.2));

        double dpSt = si?.StatorRecoveredPressureRisePa ?? 0.0;
        double dpBase = Math.Max(baseline.SiDiagnostics?.StatorRecoveredPressureRisePa ?? dpSt, 400.0);
        double statorRecoveryTerm = 1.0 + w.StatorRecoveryWeight * (dpSt / dpBase - 1.0);
        statorRecoveryTerm = Math.Clamp(statorRecoveryTerm, 0.5, 1.7);

        double er0 = Math.Max(baseline.EntrainmentRatio, 0.035);
        double er = Math.Max(candidate.EntrainmentRatio, 0.0);
        double machCh = ChamberBulkMach(si);
        double erGate = Math.Clamp(machCh / 0.085, 0.2, 1.0);
        double usefulEntrainmentTerm = 1.0 + w.UsefulEntrainmentWeight * ((er / er0) - 1.0) * erGate;
        usefulEntrainmentTerm = Math.Clamp(usefulEntrainmentTerm, 0.45, 1.6);

        double chokingPenalty = (si?.AnyEntrainmentStepChoked ?? false) ? w.ChokingPenaltyWeight : 0.0;
        double separationPenalty = w.SeparationPenaltyWeight * candidate.PhysicsMetrics.SeparationRisk01;
        double invalidPenalty = invalid01 > 0 ? w.InvalidStatePenaltyWeight * invalid01 : 0.0;
        double lossPenalty = w.TotalPressureLossPenaltyWeight * candidate.PhysicsMetrics.TotalLoss01;
        double residVt = Math.Abs(si?.FinalTangentialVelocityMps ?? 0.0);
        double residualSwirlPenalty = w.ResidualSwirlPenaltyWeight * Math.Clamp(residVt / 130.0, 0.0, 1.25);
        double lowChamberAxialPenalty = w.LowChamberAxialPenaltyWeight * candidate.PhysicsMetrics.LowAxialMomentum01;

        double shortfallPenalty = 0.0;
        if (si != null && si.SumRequestedEntrainmentIncrementsKgS > 1e-9)
        {
            double sf = si.EntrainmentShortfallSumKgS / si.SumRequestedEntrainmentIncrementsKgS;
            shortfallPenalty = w.EntrainmentShortfallPenaltyWeight * Math.Clamp(sf, 0.0, 1.4);
        }

        double healthPenalty = w.HealthIssuePenaltyEach * Math.Max(candidate.HealthCount, 0);

        double swirlCapPenalty = 0.0;
        if (si?.ChamberMarch?.SwirlEntranceCapacityStations is { } cap)
        {
            swirlCapPenalty = cap.CombinedClassification switch
            {
                SwirlEntranceCapacityClassification.Warning => 0.22 * w.SwirlEntranceCapacityPenaltyWeight,
                SwirlEntranceCapacityClassification.FailRestrictive => 0.62 * w.SwirlEntranceCapacityPenaltyWeight,
                SwirlEntranceCapacityClassification.FailChoking => w.SwirlEntranceCapacityPenaltyWeight,
                _ => 0.0
            };
        }

        double positive = thrustTerm * axialTransportTerm * statorRecoveryTerm * usefulEntrainmentTerm;
        double penalties = chokingPenalty + separationPenalty + invalidPenalty + lossPenalty + residualSwirlPenalty
            + lowChamberAxialPenalty + shortfallPenalty + healthPenalty + swirlCapPenalty;
        double score = positive - penalties;

        var bd = new AutoTuneScoreBreakdown
        {
            ThrustTerm = thrustTerm,
            AxialTransportTerm = axialTransportTerm,
            StatorRecoveryTerm = statorRecoveryTerm,
            UsefulEntrainmentTerm = usefulEntrainmentTerm,
            ChokingPenalty = chokingPenalty,
            SeparationPenalty = separationPenalty,
            InvalidStatePenalty = invalidPenalty,
            TotalPressureLossPenalty = lossPenalty,
            ResidualSwirlPenalty = residualSwirlPenalty,
            LowChamberAxialPenalty = lowChamberAxialPenalty,
            EntrainmentShortfallPenalty = shortfallPenalty,
            HealthPenalty = healthPenalty,
            SwirlEntranceCapacityPenalty = swirlCapPenalty,
            PositiveProduct = positive,
            PenaltiesSum = penalties,
            FinalScore = score
        };
        return (score, bd);
    }

    private static double BaselineExitAxialMps(FlowTuneEvaluation b) =>
        Math.Max(Math.Abs(b.SiDiagnostics?.FinalAxialVelocityMps ?? 55.0), 6.0);

    private static double ChamberBulkMach(SiFlowDiagnostics? si) =>
        si?.MarchPhysicsClosure != null
            ? Math.Clamp(si.MarchPhysicsClosure.FinalMachBulk, 0.0, 1.5)
            : 0.06;
}
