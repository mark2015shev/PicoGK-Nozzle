using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// Authoritative reduced-order capture entrainment: ambient drives mass in when capture-boundary static is below ambient.
/// Axial distribution uses η_mix(Δx/L) only; shear augmentation is secondary (see <see cref="EntrainmentModel.ComputeShearAugmentedMassIncrement"/>).
/// </summary>
public static class PressureDrivenEntrainmentPhysics
{
    /// <summary>ΔP_capture = max(P_amb − P_capture_boundary + augmentation, 0) [Pa].</summary>
    public static double CapturePressureDeficitPa(
        double ambientPressurePa,
        double captureBoundaryStaticPressurePa,
        double staticPressureDeficitAugmentationPa = 0.0)
    {
        double pAmb = Math.Max(ambientPressurePa, 1.0);
        double pCap = Math.Min(Math.Max(captureBoundaryStaticPressurePa, 1.0), pAmb * 0.99999);
        return Math.Max(0.0, pAmb - pCap + Math.Max(0.0, staticPressureDeficitAugmentationPa));
    }

    /// <summary>V_ent = C_d · √(2 ΔP / ρ_amb), bounded by <paramref name="maxEntrySpeedMps"/>.</summary>
    public static double BernoulliEntrySpeedMps(
        double deltaPCapturePa,
        double ambientDensityKgM3,
        double dischargeCoefficient,
        double maxEntrySpeedMps)
    {
        double rhoAmb = Math.Max(ambientDensityKgM3, 1e-9);
        double cd = Math.Clamp(dischargeCoefficient, 0.2, 1.0);
        double dp = Math.Max(0.0, deltaPCapturePa);
        double vEnt = cd * Math.Sqrt(2.0 * dp / rhoAmb);
        return Math.Min(vEnt, Math.Max(maxEntrySpeedMps, 0.0));
    }

    /// <summary>
    /// Δṁ = η_mix · ρ_amb · A_eff · V_ent · (Δx/L); delegates to <see cref="EntrainmentModel.ComputeCapturePressureDrivenMassIncrement"/>.
    /// </summary>
    public static double MassIncrementForStep(
        double ambientPressurePa,
        double ambientDensityKgM3,
        double captureBoundaryStaticPressurePa,
        double effectiveEntryAreaM2,
        double dischargeCoefficient,
        double axialStepM,
        double sectionLengthM,
        double lumpedMixingEffectiveness,
        double staticPressureDeficitAugmentationPa,
        double maxEntrySpeedMps)
    {
        return EntrainmentModel.ComputeCapturePressureDrivenMassIncrement(
            ambientPressurePa,
            ambientDensityKgM3,
            captureBoundaryStaticPressurePa,
            effectiveEntryAreaM2,
            dischargeCoefficient,
            axialStepM,
            sectionLengthM,
            lumpedMixingEffectiveness,
            staticPressureDeficitAugmentationPa,
            maxEntrySpeedMps);
    }
}
