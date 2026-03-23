using System;

namespace PicoGK_Run.Physics.Solvers;

/// <summary>
/// Stage 4 — swirl-aware potential ambient inflow scale (not a replacement for compressible march caps).
/// Uses core vs ambient pressure drive, capture area, and swirl amplification of the intake tendency.
/// </summary>
public static class SwirlAmbientEntrainmentSolver
{
    /// <summary>
    /// Order-of-magnitude ṁ_amb potential [kg/s] for reporting and coupling checks.
    /// ṁ ~ ρ A √(2 max(P_amb−P_core,0)/ρ) · (1 + k_s tanh(S)) · gain.
    /// </summary>
    public static double ComputeSwirlDrivenAmbientPotentialKgS(
        double ambientPressurePa,
        double coreStaticPressurePa,
        double captureAreaM2,
        double ambientDensityKgM3,
        double swirlNumberAbs,
        double gain = 0.22)
    {
        double rho = Math.Max(ambientDensityKgM3, 1e-9);
        double dp = Math.Max(ambientPressurePa - coreStaticPressurePa, 0.0);
        double aCap = Math.Max(captureAreaM2, 0.0);
        double vScale = Math.Sqrt(2.0 * dp / rho);
        double swirlAmp = 1.0 + 0.35 * Math.Tanh(Math.Max(swirlNumberAbs, 0.0));
        return rho * aCap * vScale * gain * swirlAmp;
    }
}
