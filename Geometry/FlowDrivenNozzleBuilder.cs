using System;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Geometry;

/// <summary>
/// Converts SI <see cref="NozzleDesignResult"/> into <see cref="NozzleDesignInputs"/> (mm) for existing voxel builders.
/// Non-flow template fields (injector angles, stator, wall, expander proportions) are copied from <paramref name="template"/>.
/// </summary>
public static class FlowDrivenNozzleBuilder
{
    /// <summary>mm per meter.</summary>
    private const double MetersToMm = 1000.0;

    public static NozzleDesignInputs BuildDesignInputs(NozzleDesignResult physics, NozzleDesignInputs template)
    {
        double inletDiamMm = 2.0 * physics.SuggestedInletRadiusM * MetersToMm;
        double exitDiamMm = 2.0 * physics.SuggestedOutletRadiusM * MetersToMm;
        double mixingLenMm = physics.SuggestedMixingLengthM * MetersToMm;

        double chamberDiamMm = Math.Max(
            Math.Max(inletDiamMm, exitDiamMm) * 1.06,
            exitDiamMm * 1.04);

        return new NozzleDesignInputs
        {
            InletDiameterMm = Math.Max(inletDiamMm, 1.0),
            SwirlChamberDiameterMm = Math.Max(chamberDiamMm, 1.0),
            SwirlChamberLengthMm = Math.Max(mixingLenMm, 1.0),
            InjectorAxialPositionRatio = template.InjectorAxialPositionRatio,
            TotalInjectorAreaMm2 = template.TotalInjectorAreaMm2,
            InjectorCount = template.InjectorCount,
            InjectorWidthMm = template.InjectorWidthMm,
            InjectorHeightMm = template.InjectorHeightMm,
            InjectorYawAngleDeg = template.InjectorYawAngleDeg,
            InjectorPitchAngleDeg = template.InjectorPitchAngleDeg,
            InjectorRollAngleDeg = template.InjectorRollAngleDeg,
            ExpanderLengthMm = template.ExpanderLengthMm,
            ExpanderHalfAngleDeg = template.ExpanderHalfAngleDeg,
            ExitDiameterMm = Math.Max(exitDiamMm, 1.0),
            StatorVaneAngleDeg = template.StatorVaneAngleDeg,
            StatorVaneCount = template.StatorVaneCount,
            WallThicknessMm = template.WallThicknessMm
        };
    }
}
