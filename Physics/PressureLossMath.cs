using System;
using PicoGK_Run.Core;

namespace PicoGK_Run.Physics;

/// <summary>
/// Heuristic mixing / kinetic-energy loss split into readable parts.
/// First-order, not CFD-calibrated.
/// </summary>
public static class PressureLossMath
{
    public static PressureLossBreakdown Compute(
        double injectorToSourceAreaRatio,
        double chamberLengthToDiameter,
        double injectorSwirlNumber)
    {
        // 1) Area mismatch: log-symmetric penalty around 1.0
        double areaMismatch = Math.Abs(Math.Log(Math.Max(injectorToSourceAreaRatio, 1e-6)));
        double fArea = Math.Clamp(0.04 + 0.07 * areaMismatch, 0.0, 0.28);

        // 2) Swirl-driven shear/dissipation (stronger swirl → more mixing loss)
        double fSwirl = Math.Clamp(0.05 + 0.09 * injectorSwirlNumber * injectorSwirlNumber, 0.0, 0.32);

        // 3) Short chamber: not enough length to smooth gradients
        double ld = Math.Clamp(chamberLengthToDiameter, 0.05, 4.0);
        double fShort = Math.Clamp(0.12 * Math.Exp(-1.4 * ld), 0.0, 0.12);

        double total = Math.Clamp(fArea + fSwirl + fShort, 0.02, 0.52);

        return new PressureLossBreakdown
        {
            FractionFromInjectorSourceAreaMismatch = fArea,
            FractionFromSwirlDissipation = fSwirl,
            FractionFromShortMixingLength = fShort,
            FractionTotal = total
        };
    }
}
