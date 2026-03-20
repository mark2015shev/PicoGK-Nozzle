using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// Injector jet direction heuristics. Not CFD-calibrated.
/// </summary>
public static class SwirlMath
{
    /// <summary>
    /// Decomposes injector speed into tangential (circumferential) and axial components
    /// in the chamber cylindrical frame.
    /// <para>
    /// <b>Roll:</b> For an axisymmetric point injector, rotation about the jet centerline
    /// does not change the jet direction vector; <see cref="injectorRollAngleDeg"/> is
    /// therefore ignored here and reserved for future non-axisymmetric slot orientation.
    /// </para>
    /// </summary>
    public static (double TangentialMps, double AxialMps) ResolveInjectorComponents(
        double injectorJetVelocityMps,
        double injectorYawAngleDeg,
        double injectorPitchAngleDeg)
    {
        double yawRad = DegreesToRad(injectorYawAngleDeg);
        double pitchRad = DegreesToRad(injectorPitchAngleDeg);

        double tangential = injectorJetVelocityMps * Math.Sin(yawRad);
        double axial = injectorJetVelocityMps * Math.Cos(yawRad) * Math.Cos(pitchRad);
        return (tangential, axial);
    }

    /// <summary>
    /// Tangential-to-axial ratio of the injection vector (|Vt|/|Va|).
    /// Interpretable as a dimensionless "how hard we ask the flow to swirl at the injector";
    /// not a measured swirl intensity in the chamber.
    /// </summary>
    public static double InjectorSwirlNumber(double tangentialVelocityMps, double axialVelocityMps)
    {
        return Math.Abs(tangentialVelocityMps) / Math.Max(Math.Abs(axialVelocityMps), 1e-6);
    }

    private static double DegreesToRad(double deg) => deg * (Math.PI / 180.0);
}
