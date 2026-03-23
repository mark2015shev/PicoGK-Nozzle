using System;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Geometry;

/// <summary>
/// Maps the SI solve onto <see cref="NozzleDesignInputs"/> (mm) for voxel builders.
/// Primary envelope (inlet / chamber / length / exit) is taken from <paramref name="template"/> so it matches
/// the geometry used in the march; expander, stator, injector ports, and angles also follow the template.
/// </summary>
public static class FlowDrivenNozzleBuilder
{
    public static NozzleDesignInputs BuildDesignInputs(NozzleDesignResult physics, NozzleDesignInputs template)
    {
        // SI march uses template areas/lengths (AreaAt, outlet, mixing section). Keep the same envelope here so
        // voxels + reported "final design" match what was simulated — including autotuned seeds.
        _ = physics;
        return new NozzleDesignInputs
        {
            InletDiameterMm = Math.Max(template.InletDiameterMm, 1.0),
            SwirlChamberDiameterMm = Math.Max(template.SwirlChamberDiameterMm, 1.0),
            SwirlChamberLengthMm = Math.Max(template.SwirlChamberLengthMm, 1.0),
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
            ExitDiameterMm = Math.Max(template.ExitDiameterMm, 1.0),
            StatorVaneAngleDeg = template.StatorVaneAngleDeg,
            StatorVaneCount = template.StatorVaneCount,
            StatorHubDiameterMm = template.StatorHubDiameterMm,
            StatorAxialLengthMm = template.StatorAxialLengthMm,
            StatorBladeChordMm = template.StatorBladeChordMm,
            WallThicknessMm = template.WallThicknessMm
        };
    }
}
