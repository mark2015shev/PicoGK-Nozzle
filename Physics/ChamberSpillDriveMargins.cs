using System;
using PicoGK_Run.Physics.SwirlSegment;

namespace PicoGK_Run.Physics;

/// <summary>
/// Pressure-margin spill and downstream-drive risks from chamber statics (reduced-order; not vortex-quality proxies).
/// </summary>
public static class ChamberSpillDriveMargins
{
    /// <param name="inletSpillPressureMarginPa">P_wall,upstream − P_boundary,inlet (positive ⇒ wall loading vs capture).</param>
    /// <param name="exitDrivePressureMarginPa">P_wall,downstream − P_boundary,expander (positive ⇒ wall above expander reference ⇒ weak forward drive).</param>
    public static SpillTendencyEstimate FromPressureMargins(
        double inletSpillPressureMarginPa,
        double exitDrivePressureMarginPa)
    {
        double mIn = inletSpillPressureMarginPa;
        double mEx = exitDrivePressureMarginPa;

        double refIn = Math.Max(PhysicsCalibrationHooks.InletSpillMarginReferencePa, 1.0);
        double refEx = Math.Max(PhysicsCalibrationHooks.ExitDriveWeaknessReferencePa, 1.0);
        double gIn = Math.Max(PhysicsCalibrationHooks.InletSpillRiskLinearGain, 0.0);
        double gEx = Math.Max(PhysicsCalibrationHooks.ExitDriveRiskLinearGain, 0.0);

        // Inlet spill risk rises when wall static exceeds capture reference (linear map, clamped).
        double inletSpillR = Math.Clamp(gIn * Math.Max(0.0, mIn) / refIn, 0.0, 1.0);

        // Downstream drive risk rises when wall exceeds expander-entry reference static (blocked / weak forward ΔP).
        double downDriveR = Math.Clamp(gEx * Math.Max(0.0, mEx) / refEx, 0.0, 1.0);

        double spillBi = Math.Clamp(0.5 * inletSpillR + 0.5 * downDriveR, 0.0, 1.0);

        return new SpillTendencyEstimate
        {
            InletSpillPressureMarginPa = mIn,
            ExitDrivePressureMarginPa = mEx,
            InletSpillRisk01 = inletSpillR,
            DownstreamDriveRisk01 = downDriveR,
            BidirectionalSpillRisk01 = spillBi
        };
    }
}
