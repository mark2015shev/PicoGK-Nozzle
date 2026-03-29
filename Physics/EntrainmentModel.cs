using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// Reduced-order entrainment: primary driver is ambient−local static pressure deficit at the capture boundary
/// (Bernoulli-style entry speed), scaled by a bounded lumped mixing effectiveness from swirl, L/D, and optional Re.
/// A small shear-based increment remains for numerical robustness when ΔP→0.
/// </summary>
public sealed class EntrainmentModel
{
    /// <summary>Base mixing-effectiveness scale η_mix,0 [-], typically O(0.05–0.1).</summary>
    public double Coefficient { get; set; } = 0.07;

    /// <summary>
    /// η_mix = η_0 · f(S) · f(L/D) · f(Re). S = <see cref="SwirlMath.SwirlCorrelationForEntrainment"/> (bounded).
    /// </summary>
    public double ComputeCoefficient(
        double swirlCorrelationInput,
        double chamberLdRatio,
        double reynoldsApprox,
        bool useReynoldsFactor)
    {
        double s = Math.Clamp(Math.Abs(swirlCorrelationInput), 0.0, 25.0);
        double ld = Math.Max(chamberLdRatio, 0.0);
        double fS = 1.0
            + ChamberPhysicsCoefficients.EntrainmentSwirlGainK
            * Math.Tanh(s / Math.Max(ChamberPhysicsCoefficients.EntrainmentSwirlGainS0, 0.05));
        double fLd = 1.0
            + ChamberPhysicsCoefficients.EntrainmentLdGain
            * Math.Tanh(ld / Math.Max(ChamberPhysicsCoefficients.EntrainmentLdRef, 0.1));
        double fRe = 1.0;
        if (useReynoldsFactor && reynoldsApprox > 10.0)
        {
            double ratio = reynoldsApprox / Math.Max(ChamberPhysicsCoefficients.EntrainmentReRef, 1.0);
            fRe = 1.0
                + ChamberPhysicsCoefficients.EntrainmentReGain
                * Math.Tanh(Math.Log10(Math.Max(ratio, 1e-3)));
        }

        return Coefficient * fS * fLd * fRe;
    }

    /// <summary>
    /// Pressure-deficit-driven entrainment increment over axial step Δx:
    /// Δṁ = η_mix · ρ_amb · A_capture · V_ent · (Δx/L_section), with
    /// V_ent = C_d · √(max(0, P_amb − P_local_bulk + ΔP_augment) · 2/ρ_amb), bounded by <paramref name="maxEntrySpeedMps"/>.
    /// </summary>
    public static double ComputeCapturePressureDrivenMassIncrement(
        double ambientPressurePa,
        double ambientDensityKgM3,
        double localBulkStaticPa,
        double captureAreaM2,
        double dischargeCoefficient,
        double axialStepM,
        double sectionLengthM,
        double lumpedMixingEffectiveness,
        double staticPressureDeficitAugmentationPa,
        double maxEntrySpeedMps)
    {
        double pAmb = Math.Max(ambientPressurePa, 1.0);
        double rhoAmb = Math.Max(ambientDensityKgM3, 1e-9);
        double pLoc = Math.Min(Math.Max(localBulkStaticPa, 1.0), pAmb * 0.99999);
        double deltaP = Math.Max(0.0, pAmb - pLoc + Math.Max(0.0, staticPressureDeficitAugmentationPa));
        double cd = Math.Clamp(dischargeCoefficient, 0.2, 1.0);
        double vEnt = cd * Math.Sqrt(2.0 * deltaP / rhoAmb);
        vEnt = Math.Min(vEnt, Math.Max(maxEntrySpeedMps, 0.0));
        double aCap = Math.Max(captureAreaM2, 0.0);
        double l = Math.Max(sectionLengthM, 1e-9);
        double dx = Math.Max(axialStepM, 0.0);
        double eta = Math.Clamp(lumpedMixingEffectiveness, 0.04, 1.25);
        return eta * rhoAmb * aCap * vEnt * (dx / l);
    }

    /// <summary>
    /// Optional shear-entrainment increment [kg/s] over Δx: η_mix · ρ_amb · |V|_jet · perimeter · Δx (bounded auxiliary term).
    /// </summary>
    public double ComputeShearAugmentedMassIncrement(
        double mixingEffectiveness,
        double ambientDensityKgM3,
        double localVelocityMagnitudeMps,
        double exposedPerimeterM,
        double axialStepM)
    {
        double eta = Math.Max(mixingEffectiveness, 0.0);
        double rho = Math.Max(ambientDensityKgM3, 1e-9);
        double v = Math.Max(Math.Abs(localVelocityMagnitudeMps), 0.0);
        double p = Math.Max(exposedPerimeterM, 0.0);
        double dx = Math.Max(axialStepM, 0.0);
        return eta * rho * v * p * dx;
    }

    /// <summary>
    /// Legacy duct helper: dṁ/dx = η · ρ_amb · V_jet · P (used only by obsolete <see cref="FlowMarcher.Solve"/>).
    /// </summary>
    public double ComputeEntrainedMassPerLength(
        double ceEffective,
        double ambientDensityKgM3,
        double localJetVelocityMps,
        double exposedPerimeterM)
    {
        double ce = Math.Max(ceEffective, 0.0);
        double rho = Math.Max(ambientDensityKgM3, 1e-9);
        double v = Math.Max(Math.Abs(localJetVelocityMps), 0.0);
        double p = Math.Max(exposedPerimeterM, 0.0);
        return ce * rho * v * p;
    }
}
