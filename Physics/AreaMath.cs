using System;

namespace PicoGK_Run.Physics;

public static class AreaMath
{
    public static double CircleAreaMM2(double diameterMm)
    {
        double radius = 0.5 * diameterMm;
        return Math.PI * radius * radius;
    }

    public static double CircleDiameterFromAreaMm2(double areaMm2)
    {
        if (areaMm2 <= 0.0) throw new ArgumentOutOfRangeException(nameof(areaMm2));
        return 2.0 * Math.Sqrt(areaMm2 / Math.PI);
    }

    public static double ToSquareMeters(double areaMm2) => areaMm2 * 1e-6;
}

