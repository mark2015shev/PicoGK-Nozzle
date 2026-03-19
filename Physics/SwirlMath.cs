using System;

namespace PicoGK_Run.Physics;

public static class SwirlMath
{
    /// <summary>
    /// Crude non-dimensional swirl strength estimate in [0, 1+].
    /// Uses injector angle (in-plane) and jet-to-core velocity ratio.
    /// </summary>
    public static double EstimateSwirlStrength(double injectorAngleDeg, double jetVelocityMps, double coreVelocityMps)
    {
        if (coreVelocityMps <= 1e-9) return 0.0;

        double a = injectorAngleDeg * (Math.PI / 180.0);
        double tangentialFrac = Math.Abs(Math.Sin(a)); // 0 at axial, 1 at tangential
        double vRatio = Math.Clamp(jetVelocityMps / coreVelocityMps, 0.0, 5.0);
        return tangentialFrac * vRatio;
    }
}

