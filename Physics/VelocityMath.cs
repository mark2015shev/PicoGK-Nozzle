using System;

namespace PicoGK_Run.Physics;

public static class VelocityMath
{
    /// <summary>
    /// v = mdot / (rho * A). Area is in m^2.
    /// </summary>
    public static double FromMassFlow(double massFlowKgPerSec, double densityKgPerM3, double areaM2)
    {
        if (densityKgPerM3 <= 0.0) throw new ArgumentOutOfRangeException(nameof(densityKgPerM3));
        if (areaM2 <= 0.0) throw new ArgumentOutOfRangeException(nameof(areaM2));
        return massFlowKgPerSec / (densityKgPerM3 * areaM2);
    }
}

