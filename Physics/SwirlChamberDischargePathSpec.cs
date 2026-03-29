namespace PicoGK_Run.Physics;

/// <summary>
/// First-order quasi-steady escape paths from the finite swirl control volume: upstream (inlet) vs downstream (expander/exit).
/// Consumes bulk chamber P and ρ from the live march — does not replace bulk thermodynamics.
/// </summary>
public readonly record struct SwirlChamberDischargePathSpec(
    double PUpstreamReferencePa,
    double PDownstreamReferencePa,
    double EffectiveUpstreamEscapeAreaM2,
    double EffectiveDownstreamEscapeAreaM2,
    double CdUpstream,
    double CdDownstream,
    double VaSplitBlendFactor)
{
    /// <summary>Scales continuity-consistent |V_a| for reporting: V_a,weighted ≈ V_a × (1 + k·(f_down − f_up)).</summary>
    public static double DefaultVaSplitBlendFactor => 0.12;

    /// <summary>
    /// Builds path areas and reference pressures from nozzle geometry. Expander acts on downstream ease (Cd_down, area scale), not as sole entrainment driver.
    /// </summary>
    public static SwirlChamberDischargePathSpec ForNozzleChamber(
        double ambientPressurePa,
        double captureAreaM2,
        double freeGasAnnulusAreaM2,
        double chamberBoreAreaM2,
        double exitPlaneAreaM2,
        double expanderHalfAngleDeg)
    {
        ambientPressurePa = System.Math.Max(ambientPressurePa, 1.0);
        double aCap = System.Math.Max(captureAreaM2, 1e-12);
        double aFree = System.Math.Max(freeGasAnnulusAreaM2, 1e-12);
        double aExit = System.Math.Max(exitPlaneAreaM2, 1e-12);
        double aBore = System.Math.Max(chamberBoreAreaM2, 1e-12);

        // Downstream ease: steeper expander → slightly lower downstream reference & larger effective downstream escape (not the only entrainment source).
        double angle = System.Math.Clamp(expanderHalfAngleDeg, 3.0, 22.0);
        double downstreamEase = System.Math.Clamp(0.82 + 0.022 * (angle - 7.0), 0.72, 1.15);

        double pUp = ambientPressurePa;
        double pDown = ambientPressurePa * System.Math.Clamp(0.988 - 0.06 * (1.0 - downstreamEase), 0.93, 0.998);

        double aGeoUp = System.Math.Min(aCap, System.Math.Min(aFree, aBore));
        double aGeoDown = System.Math.Min(aFree, System.Math.Max(aExit, 0.35 * aFree));

        const double upstreamAreaFractionOfLip = 0.14;
        const double downstreamAreaFractionOfPassage = 0.52;
        double aUp = System.Math.Max(upstreamAreaFractionOfLip * aGeoUp, 1e-10);
        double aDown = System.Math.Max(downstreamAreaFractionOfPassage * aGeoDown * downstreamEase, 1e-10);

        const double cdUp = 0.52;
        double cdDown = System.Math.Clamp(0.58 + 0.04 * (downstreamEase - 0.85), 0.45, 0.78);

        return new SwirlChamberDischargePathSpec(
            pUp,
            pDown,
            aUp,
            aDown,
            cdUp,
            cdDown,
            DefaultVaSplitBlendFactor);
    }
}
