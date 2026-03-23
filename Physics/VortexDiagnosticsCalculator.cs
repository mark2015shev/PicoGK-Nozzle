using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// Builds <see cref="VortexFlowDiagnostics"/> from SI march inputs/outputs — modular, first-order, not CFD.
/// </summary>
public static class VortexDiagnosticsCalculator
{
    /// <summary>
    /// Tangential decay per axial step on the primary stream from chamber L/D, diameter scale, and entrainment loading hint.
    /// </summary>
    public static double ComputeSwirlDecayPerStepFactor(
        double chamberLengthMm,
        double chamberDiameterMm,
        double entrainmentRatioHint,
        int stepCount)
    {
        int n = Math.Max(stepCount, 1);
        double ld = chamberDiameterMm > 1e-6 ? chamberLengthMm / chamberDiameterMm : 1.0;
        double er = Math.Clamp(entrainmentRatioHint, 0.0, 3.0);

        // Longer / stockier chambers and higher entrainment loading increase modeled wall/mixing loss of primary Vθ.
        double dTotal = 0.09 * Math.Tanh(ld / 4.2)
                        + 0.11 * Math.Tanh(er / 1.6)
                        + 0.07 * (1.0 - Math.Exp(-0.018 * ld))
                        + 0.04 * Math.Tanh(85.0 / Math.Max(chamberDiameterMm, 20.0));

        dTotal = Math.Clamp(dTotal, 0.04, 0.52);
        double perStep = Math.Pow(1.0 - dTotal, 1.0 / n);
        return Math.Clamp(perStep, 0.68, 0.995);
    }

    public static VortexFlowDiagnostics Compute(
        double injectorTangentialVelocityMps,
        double injectorAxialVelocityMps,
        double swirlDecayPerStepFactor,
        int marchStepCount,
        double primaryMassFlowKgS,
        double totalMassFlowEndKgS,
        double mixedTangentialVelocityPreStatorMps,
        double mixedAxialVelocityPreStatorMps,
        double tangentialVelocityPostStatorMps,
        double mixedDensityKgM3,
        double chamberDiameterMm,
        double chamberLengthMm,
        double injectorAxialPositionRatio,
        double solvedEntrainmentRatio,
        StatorRecoveryOutput statorOut,
        double statorEtaUsed,
        double statorFracVtUsed)
    {
        double vt0 = injectorTangentialVelocityMps;
        double va0 = Math.Max(Math.Abs(injectorAxialVelocityMps), 1e-6);
        double sInj = vt0 / va0;

        int n = Math.Max(marchStepCount, 1);
        double decay = Math.Clamp(swirlDecayPerStepFactor, 0.5, 1.0);
        double vtPrimaryEnd = vt0 * Math.Pow(decay, n);
        double decayFrac = vt0 * vt0 > 1e-12
            ? Math.Clamp(1.0 - (vtPrimaryEnd * vtPrimaryEnd) / (vt0 * vt0), 0.0, 1.0)
            : 0.0;

        double mEnd = Math.Max(totalMassFlowEndKgS, 1e-12);
        double mCore = Math.Max(primaryMassFlowKgS, 1e-12);
        double dilution = Math.Clamp(mCore / mEnd, 0.0, 1.0);

        double ke0 = vt0 * vt0;
        double keMixedPre = mixedTangentialVelocityPreStatorMps * mixedTangentialVelocityPreStatorMps;
        double remKeRatio = ke0 > 1e-12 ? Math.Clamp(keMixedPre / ke0, 0.0, 2.5) : 0.0;

        // Reference swirl "budget" split (engineering bookkeeping; four buckets sum to 1).
        double fDiss = decayFrac;
        double er = Math.Max(0.0, solvedEntrainmentRatio);
        double fEnt = Math.Clamp(0.10 + 0.28 * Math.Tanh(er / 1.35) * (0.55 + 0.45 * (1.0 - injectorAxialPositionRatio)), 0.05, 0.48);

        double axialGain = Math.Max(0.0, statorOut.AxialVelocityGainMps);
        double fRec = Math.Clamp(
            0.12 * statorEtaUsed
            + 0.55 * (axialGain / Math.Max(vt0, 1e-6)) * statorFracVtUsed
            + 0.08 * Math.Tanh(statorOut.RecoveredPressureRisePa / Math.Max(mixedDensityKgM3 * ke0, 1e3)),
            0.02,
            0.55);

        double fRem = Math.Max(0.0, 1.0 - fDiss - fEnt - fRec);
        double sum = fDiss + fEnt + fRec + fRem;
        if (sum > 1e-12)
        {
            fDiss /= sum;
            fEnt /= sum;
            fRec /= sum;
            fRem /= sum;
        }

        VortexStructureClass cls = Classify(
            sInj,
            chamberLengthMm / Math.Max(chamberDiameterMm, 1e-6),
            injectorAxialPositionRatio,
            decayFrac,
            remKeRatio);

        double rho = Math.Max(mixedDensityKgM3, 1e-6);
        double rWall = 0.5e-3 * Math.Max(chamberDiameterMm, 1.0);
        double rCore = Math.Clamp(0.32 * rWall, 1e-5, rWall * 0.92);
        double vtForPr = Math.Max(Math.Abs(mixedTangentialVelocityPreStatorMps), 1e-6);
        double wallRise = rho * vtForPr * vtForPr * Math.Log(Math.Max(rWall / rCore, 1.02));
        wallRise = Math.Min(wallRise, 0.55 * rho * (mixedAxialVelocityPreStatorMps * mixedAxialVelocityPreStatorMps + vtForPr * vtForPr));
        double coreDep = Math.Clamp(0.62 * wallRise + 0.12 * rho * va0 * va0 * dilution, 0.0, wallRise * 1.35);

        double vq = ComputeVortexQuality(sInj, er, decayFrac, fRec, cls);

        return new VortexFlowDiagnostics
        {
            CorePressureDepressionPa = coreDep,
            WallPressureRisePa = wallRise,
            SwirlDecayFractionAlongChamber = decayFrac,
            RemainingSwirlFractionAtStator = Math.Sqrt(Math.Min(remKeRatio, 1.0)),
            FractionSwirlForEntrainment = fEnt,
            FractionSwirlRemainingAtStator = fRem,
            FractionSwirlToAxialRecovery = fRec,
            FractionSwirlDissipated = fDiss,
            StructureClass = cls,
            StructureClassLabel = FormatClass(cls),
            VortexQualityMetric = vq,
            SwirlDecayPerStepFactorUsed = decay
        };
    }

    private static VortexStructureClass Classify(
        double injectorSwirlNumber,
        double chamberLd,
        double injectorAxialPositionRatio,
        double swirlDecayFractionAlongChamber,
        double remainingKeRatio)
    {
        double s = injectorSwirlNumber;
        double decay = swirlDecayFractionAlongChamber;
        double rem = remainingKeRatio;

        if (s > 5.2 && decay < 0.28 && chamberLd < 2.35)
            return VortexStructureClass.PossibleBreakdownOrUnstable;

        if (s > 5.8 && rem > 0.85)
            return VortexStructureClass.PossibleBreakdownOrUnstable;

        if (s >= 2.8 && chamberLd <= 2.5 && decay < 0.38 && injectorAxialPositionRatio < 0.72)
            return VortexStructureClass.ForcedVortexDominated;

        if (decay > 0.52 && s < 3.6)
            return VortexStructureClass.FreeVortexDominated;

        if (decay > 0.42 || (s < 2.4 && chamberLd > 2.8))
            return VortexStructureClass.MixedVortex;

        return VortexStructureClass.MixedVortex;
    }

    private static string FormatClass(VortexStructureClass c) => c switch
    {
        VortexStructureClass.ForcedVortexDominated => "forced-vortex dominated (heuristic)",
        VortexStructureClass.MixedVortex => "mixed vortex (heuristic)",
        VortexStructureClass.FreeVortexDominated => "free-vortex dominated (heuristic)",
        VortexStructureClass.PossibleBreakdownOrUnstable => "possible breakdown / unstable (heuristic)",
        _ => "unknown"
    };

    /// <summary>Higher = more desirable for controlled-vortex concept: moderate S, stable class, some recovery, not wiped by decay.</summary>
    private static double ComputeVortexQuality(
        double injectorSwirlNumber,
        double entrainmentRatio,
        double swirlDecayFractionAlongChamber,
        double fractionToAxialRecovery,
        VortexStructureClass cls)
    {
        double s = injectorSwirlNumber;
        double sStar = 3.15;
        double bell = Math.Exp(-0.5 * Math.Pow((s - sStar) / 1.35, 2));

        double q = 0.32 * bell
                   + 0.24 * Math.Min(entrainmentRatio / 1.35, 1.15)
                   + 0.22 * (1.0 - 0.65 * swirlDecayFractionAlongChamber)
                   + 0.22 * Math.Clamp(fractionToAxialRecovery / 0.35, 0.0, 1.0);

        if (cls == VortexStructureClass.PossibleBreakdownOrUnstable)
            q *= 0.52;
        if (s < 1.15)
            q *= 0.68;

        return Math.Clamp(q, 0.0, 1.0);
    }
}
