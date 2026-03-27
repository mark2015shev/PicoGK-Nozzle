using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// Injector jet direction in the chamber cylindrical frame (x = axial downstream).
/// <b>Not CFD-calibrated.</b>
/// </summary>
public static class SwirlMath
{
    /// <summary>
    /// Decomposes scalar jet speed into tangential and axial components.
    /// <list type="bullet">
    /// <item><b>Yaw (deg):</b> rotates the jet direction from <b>pure axial (+x)</b> toward
    /// <b>positive tangential</b> (circumferential, right-hand about +x). 0° = axial; 90° = purely tangential in the local tangent direction.</item>
    /// <item><b>Pitch (deg):</b> tilts the already yawed direction toward <b>inward radial (−r)</b>
    /// (toward the chamber axis in the meridional plane). 0° = no inward lean.</item>
    /// <item><b>Roll (deg):</b> for a <b>straight</b> injector centerline, rotation about the jet axis
    /// does not change the direction vector in an axisymmetric model. Roll is <b>ignored</b> here for
    /// physics; reserve for future <b>non-axisymmetric</b> slots (clocking of a rectangular port).</item>
    /// </list>
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

    /// <summary>|Vt|/|Va| for the injection vector — design directive, not measured chamber swirl.</summary>
    public static double InjectorSwirlNumber(double tangentialVelocityMps, double axialVelocityMps)
    {
        return Math.Abs(tangentialVelocityMps) / Math.Max(Math.Abs(axialVelocityMps), 1e-6);
    }

    /// <summary>
    /// S = Ġ_θ / (R · Ġ_x); Ġ_θ = ṁ r V_θ (bulk) [kg·m²/s²], Ġ_x = ṁ V_ax [kg·m²/s²], R [m].
    /// </summary>
    public static double FluxSwirlNumber(
        double angularMomentumFluxKgM2PerS2,
        double axialMomentumFluxKgM2PerS2,
        double referenceRadiusM)
    {
        double r = Math.Max(Math.Abs(referenceRadiusM), 1e-12);
        if (Math.Abs(axialMomentumFluxKgM2PerS2) < 1e-18)
            return 0.0;
        return angularMomentumFluxKgM2PerS2 / (r * axialMomentumFluxKgM2PerS2);
    }

    private static double DegreesToRad(double deg) => deg * (Math.PI / 180.0);
}
