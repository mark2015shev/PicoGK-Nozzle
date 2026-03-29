using System;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Geometry;

/// <summary>
/// Maps the SI solve onto <see cref="NozzleDesignInputs"/> (mm) for voxel builders.
/// By default the template drives the envelope (matches the SI march). Optional hints merge solved mixing length / outlet scale.
/// </summary>
public static class FlowDrivenNozzleBuilder
{
    public static NozzleDesignInputs BuildDesignInputs(NozzleDesignResult physics, NozzleDesignInputs template) =>
        BuildDesignInputs(physics, template, run: null);

    public static NozzleDesignInputs BuildDesignInputs(
        NozzleDesignResult physics,
        NozzleDesignInputs template,
        RunConfiguration? run)
    {
        double chamberLenMm = Math.Max(template.SwirlChamberLengthMm, 1.0);
        double exitDiamMm = Math.Max(template.ExitDiameterMm, 1.0);
        double inletDiamMm = Math.Max(template.InletDiameterMm, 1.0);

        if (run?.ApplySolvedGeometryHints == true)
        {
            double lSolveM = Math.Max(physics.SuggestedMixingLengthM, 1e-6);
            double lSolveMm = lSolveM * 1000.0;
            chamberLenMm = Math.Clamp(lSolveMm, template.SwirlChamberLengthMm * 0.88, template.SwirlChamberLengthMm * 1.18);

            double dOutMm = 2.0 * 1000.0 * Math.Max(physics.SuggestedOutletRadiusM, 1e-6);
            exitDiamMm = Math.Clamp(dOutMm, template.ExitDiameterMm * 0.92, template.ExitDiameterMm * 1.12);

            double dInMm = 2.0 * 1000.0 * Math.Max(physics.SuggestedInletRadiusM, 1e-6);
            double dCh = Math.Max(template.SwirlChamberDiameterMm, 1.0);
            inletDiamMm = Math.Clamp(dInMm, dCh * 0.95, dCh * 1.35);
        }

        return new NozzleDesignInputs
        {
            InletDiameterMm = inletDiamMm,
            SwirlChamberDiameterMm = Math.Max(template.SwirlChamberDiameterMm, 1.0),
            SwirlChamberLengthMm = chamberLenMm,
            InjectorAxialPositionRatio = template.InjectorAxialPositionRatio,
            InjectorUpstreamGuardLengthMm = template.InjectorUpstreamGuardLengthMm,
            TotalInjectorAreaMm2 = template.TotalInjectorAreaMm2,
            InjectorCount = template.InjectorCount,
            InjectorWidthMm = template.InjectorWidthMm,
            InjectorHeightMm = template.InjectorHeightMm,
            InjectorYawAngleDeg = template.InjectorYawAngleDeg,
            InjectorPitchAngleDeg = template.InjectorPitchAngleDeg,
            InjectorRollAngleDeg = template.InjectorRollAngleDeg,
            ExpanderLengthMm = template.ExpanderLengthMm,
            ExpanderHalfAngleDeg = template.ExpanderHalfAngleDeg,
            ExitDiameterMm = exitDiamMm,
            StatorVaneAngleDeg = template.StatorVaneAngleDeg,
            StatorVaneCount = template.StatorVaneCount,
            StatorHubDiameterMm = template.StatorHubDiameterMm,
            StatorAxialLengthMm = template.StatorAxialLengthMm,
            StatorBladeChordMm = template.StatorBladeChordMm,
            WallThicknessMm = template.WallThicknessMm
        };
    }
}
