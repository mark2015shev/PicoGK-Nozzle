using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// Injector direction decomposition and swirl metrics.
/// Chamber entrainment / decay use <see cref="SwirlCorrelationForEntrainment"/> (bounded); flux S when |Va| is significant.
/// Injector directive <see cref="InjectorSwirlDirective"/> = |Vt|/|V| replaces explosive |Vt|/|Va| for 90° jets.
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

    /// <summary>|Vt|/|Va| — legacy only; explodes when Va→0 (e.g. 90° injector). Prefer <see cref="InjectorSwirlDirective"/>.</summary>
    public static double InjectorSwirlNumber(double tangentialVelocityMps, double axialVelocityMps)
    {
        return Math.Abs(tangentialVelocityMps) / Math.Max(Math.Abs(axialVelocityMps), 1e-6);
    }

    /// <summary>Bounded injector swirl metric: |Vt|/|V| with V = √(Va²+Vt²); in [0, 1] for a single jet direction.</summary>
    public static double InjectorSwirlDirective(double tangentialVelocityMps, double velocityMagnitudeMps)
    {
        double v = Math.Max(Math.Abs(velocityMagnitudeMps), 1e-12);
        return Math.Clamp(Math.Abs(tangentialVelocityMps) / v, 0.0, 1.0);
    }

    /// <summary>Chamber bulk swirl surrogate: |Vt|/max(|Va|, Va_floor) for diagnostics and bounded correlations.</summary>
    public static double ChamberSwirlBulkRatio(double tangentialVelocityMps, double axialVelocityMps, double vaFloorMps)
    {
        double denom = Math.Max(Math.Max(Math.Abs(axialVelocityMps), Math.Abs(vaFloorMps)), 1e-9);
        return Math.Abs(tangentialVelocityMps) / denom;
    }

    /// <summary>
    /// Entrainment / decay correlation input: flux swirl when |Va_bulk| ≥ floor; otherwise capped bulk ratio (tangential-dominated jets).
    /// </summary>
    public static double SwirlCorrelationForEntrainment(
        double angularMomentumFluxKgM2PerS2,
        double axialMomentumFluxKgM2PerS2,
        double massFlowKgS,
        double referenceRadiusM,
        double velocityMagnitudeMps,
        double bulkTangentialVelocityMps)
    {
        double md = Math.Max(massFlowKgS, 1e-18);
        double vaBulk = md > 1e-18 ? axialMomentumFluxKgM2PerS2 / md : 0.0;
        if (Math.Abs(vaBulk) >= ChamberAerodynamicsConfiguration.VaFloorForBulkSwirlMps)
        {
            double s = FluxSwirlNumber(angularMomentumFluxKgM2PerS2, axialMomentumFluxKgM2PerS2, referenceRadiusM);
            return Math.Clamp(Math.Abs(s), 0.0, 25.0);
        }

        double vmag = Math.Max(Math.Abs(velocityMagnitudeMps), 1e-9);
        double directive = InjectorSwirlDirective(bulkTangentialVelocityMps, vmag);
        return Math.Clamp(4.0 * directive, 0.0, 25.0);
    }

    /// <summary>
    /// Bounded |Vt|/|Va| for logs and CSV only (avoids blow-up when |Va|→0); not a correlation input.
    /// </summary>
    public static double InjectorSwirlNumberReportOnly(
        double tangentialVelocityMps,
        double axialVelocityMps,
        double jetSpeedReferenceMps)
    {
        double vJet = Math.Max(Math.Abs(jetSpeedReferenceMps), 1.0);
        double denom = Math.Max(Math.Max(Math.Abs(axialVelocityMps), 1e-9), 0.05 * vJet);
        return Math.Abs(tangentialVelocityMps) / denom;
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

    /// <summary>Bulk angular-momentum flux Ġ_θ = ṁ · R_ref · V_θ,bulk [kg·m²/s²].</summary>
    public static double AngularMomentumFluxFromBulk(double massFlowKgS, double referenceRadiusM, double bulkTangentialVelocityMps)
    {
        double md = Math.Max(massFlowKgS, 0.0);
        double r = Math.Max(Math.Abs(referenceRadiusM), 1e-12);
        return md * r * bulkTangentialVelocityMps;
    }

    /// <summary>V_θ,bulk = Ġ_θ / (ṁ · R_ref).</summary>
    public static double BulkTangentialVelocityFromAngularMomentumFlux(
        double angularMomentumFluxKgM2PerS2,
        double massFlowKgS,
        double referenceRadiusM)
    {
        double md = Math.Max(massFlowKgS, 1e-18);
        double r = Math.Max(Math.Abs(referenceRadiusM), 1e-12);
        return angularMomentumFluxKgM2PerS2 / (md * r);
    }

    private static double DegreesToRad(double deg) => deg * (Math.PI / 180.0);
}
