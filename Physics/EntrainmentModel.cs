using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// Reduced-order entrainment: authoritative driver is pressure-driven capture (see <see cref="PressureDrivenEntrainmentPhysics"/>):
/// ΔP_capture = max(P_amb − P_capture_boundary, 0), V_ent = C_d√(2ΔP/ρ_amb), Δṁ = η_mix·ρ_amb·A_eff·V_ent·(Δx/L).
/// Shear augmentation is secondary. Legacy Ce·ρ·V·P remains only on obsolete <see cref="FlowMarcher.Solve"/>.
/// </summary>
public sealed class EntrainmentModel
{
    /// <summary>Base mixing-effectiveness scale η_mix,0 [-], typically O(0.05–0.1).</summary>
    public double Coefficient { get; set; } = 0.07;

    /// <summary>
    /// η_mix = η_0 · f(L/D) · f(Re) for spreading pressure-driven demand along the chamber length.
    /// Does not use swirl-number multipliers — swirl enters only through the bulk state in ΔP and shear.
    /// </summary>
    public double LumpedAxialMixingEffectiveness(
        double chamberLdRatio,
        double reynoldsApprox,
        bool useReynoldsFactor)
    {
        double ld = Math.Max(chamberLdRatio, 0.0);
        double ldRef = Math.Max(ChamberPhysicsCoefficients.EntrainmentLdRef, 0.1);
        double fLd = 1.0
            + ChamberPhysicsCoefficients.EntrainmentLdGain
            * Math.Min(ld / ldRef, 2.5)
            * 0.45;
        double fRe = 1.0;
        if (useReynoldsFactor && reynoldsApprox > 10.0)
        {
            double ratio = reynoldsApprox / Math.Max(ChamberPhysicsCoefficients.EntrainmentReRef, 1.0);
            double logR = Math.Log10(Math.Max(ratio, 1e-3));
            fRe = 1.0
                + ChamberPhysicsCoefficients.EntrainmentReGain
                * Math.Min(Math.Max(logR, -1.0), 1.2)
                * 0.35;
        }

        return Coefficient * fLd * fRe;
    }

    /// <summary>
    /// Pressure-deficit-driven entrainment increment over axial step Δx:
    /// Δṁ = η_mix · ρ_amb · A_eff · V_ent · (Δx/L_section), with
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
