using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// Axial pressure resultant from ΔP on an explicitly supplied projected area (SI).
/// </summary>
public static class PressureForceMath
{
    /// <summary>F_ax = ΔP · A_proj (Pa · m² = N). ΔP may be negative.</summary>
    public static double AxialForceFromPressureDelta(double deltaPPa, double projectedAreaM2)
    {
        double a = Math.Max(projectedAreaM2, 0.0);
        return deltaPPa * a;
    }

    /// <summary>Force from ambient vs local static on capture (P_amb - P_local) if local &lt; ambient draws forward on CV.</summary>
    public static double InletCaptureAnnulusAxialForce(double ambientPressurePa, double localStaticPressurePa, double axialProjectedCaptureAreaM2)
    {
        return AxialForceFromPressureDelta(ambientPressurePa - localStaticPressurePa, axialProjectedCaptureAreaM2);
    }

    /// <summary>Expander / diffuser-style axial push from over-pressure on sloped surface projected to axis.</summary>
    public static double ExpanderOverPressureAxialForce(double pressureRisePa, double axialProjectedAreaM2)
    {
        return AxialForceFromPressureDelta(pressureRisePa, axialProjectedAreaM2);
    }
}
