using System;

namespace PicoGK_Run.Physics;

public static class PressureLossMath
{
    /// <summary>
    /// Simple pressure loss coefficient-like estimate (unitless).
    /// Penalizes area mismatch and swirl. This is a placeholder model.
    /// </summary>
    public static double EstimateLoss(
        double inletAreaM2,
        double chamberAreaM2,
        double exitAreaM2,
        double swirlStrength)
    {
        if (inletAreaM2 <= 0 || chamberAreaM2 <= 0 || exitAreaM2 <= 0) return 0.0;

        double a1 = chamberAreaM2 / inletAreaM2;
        double a2 = exitAreaM2 / chamberAreaM2;

        double mismatch = Math.Abs(Math.Log(a1)) + Math.Abs(Math.Log(a2));
        double swirlPenalty = 0.15 * swirlStrength * swirlStrength;
        return mismatch + swirlPenalty;
    }
}

