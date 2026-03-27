using System;

namespace PicoGK_Run.Physics;

/// <summary>Swirl bookkeeping along chamber → expander → stator (velocity scales, not CFD).</summary>
public sealed class SwirlBudgetResult
{
    public double SwirlInjectedVtMps { get; init; }
    public double SwirlAfterChamberDecayVtPrimaryMps { get; init; }
    public double SwirlMixedAtChamberEndVtMps { get; init; }
    public double SwirlUsedForEntrainmentMetric { get; init; }
    public double SwirlRemainingIntoExpanderMetric { get; init; }
    public double SwirlAtStatorVtMps { get; init; }
    public double SwirlDissipatedOverallMetric { get; init; }
    public double KTotalUsed { get; init; }
    public string Notes { get; init; } = "";
}

/// <summary>
/// Exponential-style decay V_theta,primary ~ V0 * exp(-k_total * x/D) discretized to per-step factor.
/// k_total = k_wall + k_mixing + k_entrainment + k_instability (first-order).
/// </summary>
public static class SwirlDecayModel
{
    /// <summary>First-order breakdown risk [0,1]; prefer flux swirl S, not |V_t|/|V_a|, as correlation input.</summary>
    public static double PreMarchBreakdownRisk(double swirlCorrelation, double chamberLd, double injectorAxialRatio)
    {
        double s = Math.Clamp(Math.Abs(swirlCorrelation), 0.0, 25.0);
        double g1 = Math.Clamp((s - 4.2) / 2.4, 0.0, 1.0);
        double g2 = Math.Clamp((2.4 - chamberLd) / 1.6, 0.0, 1.0);
        double g3 = Math.Clamp((0.92 - injectorAxialRatio) / 0.55, 0.0, 1.0);
        return Math.Clamp(0.22 * g1 + 0.28 * g2 + 0.12 * g3, 0.0, 1.0);
    }

    public static double ComputeKTotal(
        double chamberLengthMm,
        double chamberDiameterMm,
        double swirlCorrelationForMixing,
        double entrainmentRatioHint,
        double injectorAxialPositionRatio,
        double preMarchBreakdownRisk01)
    {
        double dM = Math.Max(chamberDiameterMm * 1e-3, 1e-4);
        double ld = chamberDiameterMm > 1e-6 ? chamberLengthMm / chamberDiameterMm : 1.0;
        double er = Math.Clamp(entrainmentRatioHint, 0.0, 3.0);
        double s = Math.Clamp(Math.Abs(swirlCorrelationForMixing), 0.0, 25.0);

        double kWall = ChamberPhysicsCoefficients.DecayCWall
                       * ChamberPhysicsCoefficients.DecayFRoughness
                       * (1.0 + 0.35 * Math.Tanh(ld / 4.5));

        double kMix = ChamberPhysicsCoefficients.DecayCMix * Math.Tanh(s / 3.8);

        double kEnt = ChamberPhysicsCoefficients.DecayCEnt
                      * Math.Tanh(er / 1.5)
                      * (0.65 + 0.35 * (1.0 - injectorAxialPositionRatio));

        double kInst = ChamberPhysicsCoefficients.DecayCInstability * preMarchBreakdownRisk01;

        return Math.Clamp(kWall + kMix + kEnt + kInst, 0.02, 1.85);
    }

    /// <summary>Per-step multiplicative decay so that over n steps exp(-k * L/D) is recovered.</summary>
    public static double DecayPerStepFromK(double kTotal, double sectionLengthM, double diameterM, int stepCount)
    {
        int n = Math.Max(stepCount, 1);
        double d = Math.Max(diameterM, 1e-5);
        double l = Math.Max(sectionLengthM, 0.0);
        double exponent = -kTotal * l / d / n;
        return Math.Clamp(Math.Exp(exponent), 0.65, 0.998);
    }

    public static SwirlBudgetResult BuildBudget(
        double vtInjected,
        double decayPerStep,
        int nSteps,
        double primaryMdot,
        double totalMdotEnd,
        double vtMixedPreStator,
        double vtPostStator,
        double entrainmentRatio,
        double kTotal,
        string extraNote = "")
    {
        int n = Math.Max(nSteps, 1);
        double decay = Math.Clamp(decayPerStep, 0.5, 1.0);
        double vtPriEnd = vtInjected * Math.Pow(decay, n);
        double mEnd = Math.Max(totalMdotEnd, 1e-12);
        double mCore = Math.Max(primaryMdot, 1e-12);

        double dilutionVt = mCore / mEnd * vtPriEnd;
        double entMetric = Math.Clamp(0.35 * Math.Tanh(entrainmentRatio / 1.4) * Math.Abs(vtInjected), 0.0, Math.Abs(vtInjected));
        double dissMetric = Math.Max(0.0, Math.Abs(vtInjected) - Math.Abs(vtMixedPreStator));

        string notes =
            "Vt_primary decays per step; mixed Vt includes dilution. Entrainment metric is heuristic coupling, not CFD." +
            (string.IsNullOrEmpty(extraNote) ? "" : " " + extraNote);

        return new SwirlBudgetResult
        {
            SwirlInjectedVtMps = vtInjected,
            SwirlAfterChamberDecayVtPrimaryMps = vtPriEnd,
            SwirlMixedAtChamberEndVtMps = vtMixedPreStator,
            SwirlUsedForEntrainmentMetric = entMetric,
            SwirlRemainingIntoExpanderMetric = Math.Abs(vtMixedPreStator),
            SwirlAtStatorVtMps = vtPostStator,
            SwirlDissipatedOverallMetric = dissMetric,
            KTotalUsed = kTotal,
            Notes = notes
        };
    }
}
