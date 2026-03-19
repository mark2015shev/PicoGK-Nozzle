using System;

namespace PicoGK_Run.Physics;

public static class SwirlMath
{
    /// <summary>
    /// Heuristic injector velocity decomposition from yaw/pitch/roll.
    /// This is a first-order directional model, not CFD-calibrated.
    /// </summary>
    public static (double TangentialMps, double AxialMps) ResolveComponents(
        double injectorJetVelocityMps,
        double injectorYawAngleDeg,
        double injectorPitchAngleDeg,
        double injectorRollAngleDeg)
    {
        double yawRad = DegreesToRad(injectorYawAngleDeg);
        double pitchRad = DegreesToRad(injectorPitchAngleDeg);
        double rollRad = DegreesToRad(injectorRollAngleDeg);

        // Yaw controls tangential injection, pitch controls axial projection,
        // roll is treated as secondary alignment loss of useful components.
        double tangential = injectorJetVelocityMps * Math.Sin(yawRad) * Math.Cos(rollRad);
        double axial = injectorJetVelocityMps * Math.Cos(yawRad) * Math.Cos(pitchRad) * Math.Cos(rollRad);
        return (tangential, axial);
    }

    /// <summary>
    /// Unitless swirl indicator based on tangential-to-axial ratio.
    /// Not CFD-calibrated.
    /// </summary>
    public static double EstimateStrength(double tangentialVelocityMps, double axialVelocityMps)
    {
        return Math.Abs(tangentialVelocityMps) / Math.Max(Math.Abs(axialVelocityMps), 1e-6);
    }

    private static double DegreesToRad(double deg)
    {
        return deg * (Math.PI / 180.0);
    }
}

