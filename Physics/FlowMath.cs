using System;

namespace PicoGK_Run.Physics;

public static class FlowMath
{
    public static double CircleAreaMM2FromDiameterMM(double diameterMM)
    {
        double r = 0.5 * diameterMM;
        return Math.PI * r * r;
    }

    public static double CircleAreaM2FromDiameterMM(double diameterMM)
        => CircleAreaMM2FromDiameterMM(diameterMM) * 1e-6;

    public static double VelocityMps(double massFlowKgPerSec, double densityKgPerM3, double areaM2)
    {
        if (densityKgPerM3 <= 0) throw new ArgumentOutOfRangeException(nameof(densityKgPerM3));
        if (areaM2 <= 0) throw new ArgumentOutOfRangeException(nameof(areaM2));
        return massFlowKgPerSec / (densityKgPerM3 * areaM2);
    }
}

