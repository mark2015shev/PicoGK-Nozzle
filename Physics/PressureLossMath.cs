using System;
using PicoGK_Run.Core;

namespace PicoGK_Run.Physics;

/// <summary>
/// <b>HEURISTIC — NOT CFD-CALIBRATED.</b> Splits a single kinetic-energy loss fraction into
/// named parts. Swirl dissipation uses a <b>saturating</b> form in S (not S²-dominated) so high
/// swirl adds mixing cost without exploding the loss.
/// </summary>
public static class PressureLossMath
{
    public static PressureLossBreakdown Compute(
        double injectorToSourceAreaRatio,
        double chamberLengthToDiameter,
        double injectorSwirlNumber)
    {
        // --- HEURISTIC loss terms (first-order estimate; not CFD-calibrated) ---

        // 1) Area mismatch: log-symmetric penalty around 1.0
        double areaMismatch = Math.Abs(Math.Log(Math.Max(injectorToSourceAreaRatio, 1e-6)));
        double fArea = Math.Clamp(0.04 + 0.07 * areaMismatch, 0.0, 0.28);

        // 2) Swirl-driven mixing dissipation: interpretable as extra shear when |Vt|/|Va| is large,
        //    but bounded — uses S/(1+S) + mild linear tail instead of aggressive S².
        double s = Math.Max(0.0, injectorSwirlNumber);
        double swirlDissipationShape = s / (1.0 + 0.65 * s) + 0.12 * Math.Min(s, 2.5);
        double fSwirl = Math.Clamp(0.03 + 0.085 * swirlDissipationShape, 0.0, 0.20);

        // 3) Short chamber: not enough length to smooth gradients
        double ld = Math.Clamp(chamberLengthToDiameter, 0.05, 4.0);
        double fShort = Math.Clamp(0.12 * Math.Exp(-1.4 * ld), 0.0, 0.12);

        double total = Math.Clamp(fArea + fSwirl + fShort, 0.02, 0.48);

        return new PressureLossBreakdown
        {
            FractionFromInjectorSourceAreaMismatch = fArea,
            FractionFromSwirlDissipation = fSwirl,
            FractionFromShortMixingLength = fShort,
            FractionTotal = total
        };
    }

    public static string DominantContribution(PressureLossBreakdown b)
    {
        if (b.FractionFromSwirlDissipation >= b.FractionFromInjectorSourceAreaMismatch &&
            b.FractionFromSwirlDissipation >= b.FractionFromShortMixingLength)
            return "swirl_dissipation";
        if (b.FractionFromShortMixingLength >= b.FractionFromInjectorSourceAreaMismatch)
            return "short_mixing_length";
        return "injector_source_area_mismatch";
    }
}
