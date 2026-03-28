using System;

namespace PicoGK_Run.Parameters;

/// <summary>
/// Deterministic derived radii / spans from <see cref="NozzleGeometryGenome"/> for builders, continuity checks, and logs.
/// Inner radii = half diameters where noted.
/// </summary>
public readonly record struct DerivedNozzleGeometryParameters(
    double ChamberInnerRadiusMm,
    double InletInnerRadiusMm,
    double ExitInnerRadiusMm,
    double StatorHubRadiusMm,
    double StatorAnnulusInnerRadiusMm,
    double StatorBladeSpanMm,
    double ExpanderEndInnerRadiusMm,
    double ExpanderAxialLengthMm,
    double EffectiveInletContractionLengthMm,
    double EffectiveInletLipLengthMm);

/// <summary>Maps genome → builder-ready numbers (deterministic, no SI physics).</summary>
public static class NozzleGeometryGenomeMapper
{
    /// <summary>
    /// Full <see cref="NozzleDesignInputs"/> for SI + voxel pipeline: genome drives skeleton; <paramref name="injectorTemplate"/> supplies port layout.
    /// </summary>
    public static NozzleDesignInputs ToDesignInputs(NozzleGeometryGenome genome, NozzleDesignInputs injectorTemplate, bool lockInjectorYawTo90Degrees)
    {
        double yaw = lockInjectorYawTo90Degrees ? 90.0 : injectorTemplate.InjectorYawAngleDeg;
        double chord = genome.StatorChordMm ?? DefaultStatorChordMm(genome);
        int vanes = genome.StatorVaneCount ?? injectorTemplate.StatorVaneCount;
        double wall = genome.WallThicknessMm ?? injectorTemplate.WallThicknessMm;

        return new NozzleDesignInputs
        {
            InletDiameterMm = genome.InletDiameterMm,
            SwirlChamberDiameterMm = genome.SwirlChamberDiameterMm,
            SwirlChamberLengthMm = genome.SwirlChamberLengthMm,
            InjectorAxialPositionRatio = genome.InjectorAxialPositionRatio,
            TotalInjectorAreaMm2 = injectorTemplate.TotalInjectorAreaMm2,
            InjectorCount = injectorTemplate.InjectorCount,
            InjectorWidthMm = injectorTemplate.InjectorWidthMm,
            InjectorHeightMm = injectorTemplate.InjectorHeightMm,
            InjectorYawAngleDeg = yaw,
            InjectorPitchAngleDeg = injectorTemplate.InjectorPitchAngleDeg,
            InjectorRollAngleDeg = injectorTemplate.InjectorRollAngleDeg,
            ExpanderLengthMm = genome.ExpanderLengthMm,
            ExpanderHalfAngleDeg = genome.ExpanderHalfAngleDeg,
            ExitDiameterMm = genome.ExitDiameterMm,
            StatorVaneAngleDeg = genome.StatorVaneAngleDeg,
            StatorVaneCount = vanes,
            StatorHubDiameterMm = genome.StatorHubDiameterMm,
            StatorAxialLengthMm = genome.StatorAxialLengthMm,
            StatorBladeChordMm = chord,
            WallThicknessMm = wall
        };
    }

    public static DerivedNozzleGeometryParameters Derive(NozzleGeometryGenome g)
    {
        double rCh = 0.5 * g.SwirlChamberDiameterMm;
        double rIn = 0.5 * g.InletDiameterMm;
        double rEx = 0.5 * g.ExitDiameterMm;
        double rHub = 0.5 * g.StatorHubDiameterMm;
        double halfRad = g.ExpanderHalfAngleDeg * (Math.PI / 180.0);
        double rExpEnd = rCh + Math.Tan(halfRad) * g.ExpanderLengthMm;
        double span = Math.Max(0.0, rCh - rHub);
        double lip = g.InletLipLengthMm ?? Math.Clamp(0.12 * g.InletDiameterMm, 1.5, 18.0);
        double contraction = g.InletContractionLengthMm ?? Math.Clamp(0.45 * Math.Max(0.0, rCh - rIn), 4.0, 45.0);

        return new DerivedNozzleGeometryParameters(
            ChamberInnerRadiusMm: rCh,
            InletInnerRadiusMm: rIn,
            ExitInnerRadiusMm: rEx,
            StatorHubRadiusMm: rHub,
            StatorAnnulusInnerRadiusMm: rCh,
            StatorBladeSpanMm: span,
            ExpanderEndInnerRadiusMm: rExpEnd,
            ExpanderAxialLengthMm: g.ExpanderLengthMm,
            EffectiveInletContractionLengthMm: contraction,
            EffectiveInletLipLengthMm: lip);
    }

    /// <summary>First-order chord when genome leaves <see cref="NozzleGeometryGenome.StatorChordMm"/> unset.</summary>
    public static double DefaultStatorChordMm(NozzleGeometryGenome g)
    {
        double rCh = 0.5 * g.SwirlChamberDiameterMm;
        double rHub = 0.5 * g.StatorHubDiameterMm;
        double span = Math.Max(0.0, rCh - rHub);
        return Math.Clamp(0.22 * span, 3.5, 22.0);
    }
}
