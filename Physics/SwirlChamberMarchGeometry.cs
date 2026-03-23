using System;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Physics;

/// <summary>
/// Swirl-chamber duct properties for the SI march — aligned with CAD bore + hub annulus (mm → m).
/// March is axial over chamber length only; expander/exit are not in this 1-D segment.
/// </summary>
public static class SwirlChamberMarchGeometry
{
    public const double AirKinematicViscosityM2s = 1.5e-5;

    /// <summary>Full bore π D²/4 [mm²].</summary>
    public static double ChamberBoreAreaMm2(double swirlChamberDiameterMm)
    {
        double d = Math.Max(swirlChamberDiameterMm, 1e-6);
        return Math.PI * 0.25 * d * d;
    }

    /// <summary>Hub disk π D_hub²/4 [mm²].</summary>
    public static double HubDiskAreaMm2(double statorHubDiameterMm)
    {
        if (statorHubDiameterMm <= 0.5)
            return 0.0;
        double d = statorHubDiameterMm;
        return Math.PI * 0.25 * d * d;
    }

    /// <summary>Gas path annulus = bore − hub [mm²].</summary>
    public static double AnnulusGasAreaMm2(double swirlChamberDiameterMm, double statorHubDiameterMm) =>
        Math.Max(ChamberBoreAreaMm2(swirlChamberDiameterMm) - HubDiskAreaMm2(statorHubDiameterMm), 1e-6);

    /// <summary>Subtract fraction of annulus for vane frontal blockage (0 = none).</summary>
    public static double EffectiveGasAreaMm2(
        double swirlChamberDiameterMm,
        double statorHubDiameterMm,
        double vaneBlockageFractionOfAnnulus)
    {
        double aAnn = AnnulusGasAreaMm2(swirlChamberDiameterMm, statorHubDiameterMm);
        double sub = Math.Clamp(vaneBlockageFractionOfAnnulus, 0.0, 0.85) * aAnn;
        return Math.Max(aAnn - sub, 1e-6);
    }

    /// <summary>Wetted perimeter for entrainment shear: outer casing + inner hub trace [m].</summary>
    public static double EntrainmentPerimeterM(double swirlChamberDiameterMm, double statorHubDiameterMm)
    {
        double dChM = 1e-3 * Math.Max(swirlChamberDiameterMm, 1e-6);
        double dHubM = statorHubDiameterMm > 0.5 ? 1e-3 * statorHubDiameterMm : 0.0;
        return Math.PI * (dChM + dHubM);
    }

    /// <summary>Inlet capture lip area [mm²].</summary>
    public static double InletCaptureAreaMm2(double inletDiameterMm)
    {
        double d = Math.Max(inletDiameterMm, 1e-6);
        return Math.PI * 0.25 * d * d;
    }

    /// <summary>Exit inner target area from design [mm²].</summary>
    public static double ExitInnerAreaMm2(double exitDiameterMm)
    {
        double d = Math.Max(exitDiameterMm, 1e-6);
        return Math.PI * 0.25 * d * d;
    }

    /// <summary>Capture area [m²] for entrainment intake (RunConfiguration).</summary>
    public static double CaptureAreaM2(
        NozzleDesignInputs design,
        RunConfiguration run,
        double effectiveAnnulusGasAreaMm2)
    {
        double aInletMm2 = InletCaptureAreaMm2(design.InletDiameterMm);
        double baseMm2 = run.UseExplicitInletCapture
            ? Math.Min(aInletMm2, effectiveAnnulusGasAreaMm2)
            : aInletMm2;
        baseMm2 *= Math.Clamp(run.CaptureAreaFactor, 0.05, 4.0);
        return Math.Max(baseMm2 * 1e-6, 1e-10);
    }

    /// <summary>Simple Re_D = V·D/ν with D ≈ chamber diameter [m], ν kinematic.</summary>
    public static double ChamberReynoldsApprox(double velocityMps, double chamberDiameterMm)
    {
        double d = Math.Max(chamberDiameterMm * 1e-3, 1e-6);
        double nu = Math.Max(AirKinematicViscosityM2s, 1e-7);
        double v = Math.Max(Math.Abs(velocityMps), 0.0);
        return v * d / nu;
    }
}
