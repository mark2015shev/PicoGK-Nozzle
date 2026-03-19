using System;

namespace PicoGK_Run.Physics;

public static class PressureLossMath
{
    /// <summary>
    /// Heuristic mixing/pressure loss factor (0..1). Not calibrated.
    /// </summary>
    public static double EstimateLossFraction(
        double injectorToSourceAreaRatio,
        double chamberResidenceRatio,
        double swirlStrength)
    {
        // A strong injector/source area mismatch tends to increase losses.
        double areaMismatchPenalty = Math.Abs(Math.Log(Math.Max(injectorToSourceAreaRatio, 1e-6)));

        // Longer chambers can reduce abrupt-mixing losses up to a point.
        double residenceBenefit = Math.Clamp(chamberResidenceRatio, 0.2, 3.0);

        // Excessive swirl raises dissipation.
        double swirlPenalty = Math.Min(0.30, 0.08 * swirlStrength * swirlStrength);

        double raw = 0.05 + (0.06 * areaMismatchPenalty) + swirlPenalty - (0.03 * residenceBenefit);
        return Math.Clamp(raw, 0.02, 0.45);
    }
}

