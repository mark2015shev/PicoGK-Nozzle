using System;

namespace PicoGK_Run.Parameters;

/// <summary>
/// Single source of truth for nozzle <b>hard-body</b> skeleton (mm / deg). Injector port layout (count, slot size, yaw/pitch)
/// remains on <see cref="NozzleDesignInputs"/> and is merged by <see cref="NozzleGeometryGenomeMapper"/> from a template.
/// </summary>
public sealed record NozzleGeometryGenome
{
    // --- Tier A: primary physics (search first) ---
    public required double InletDiameterMm { get; init; }
    public required double SwirlChamberDiameterMm { get; init; }
    public required double SwirlChamberLengthMm { get; init; }
    public required double InjectorAxialPositionRatio { get; init; }
    public required double ExpanderHalfAngleDeg { get; init; }
    public required double ExpanderLengthMm { get; init; }
    public required double ExitDiameterMm { get; init; }
    public required double StatorVaneAngleDeg { get; init; }

    // --- Tier B: secondary recovery / fit (search after Tier A stabilizes) ---
    public required double StatorAxialLengthMm { get; init; }
    public required double StatorHubDiameterMm { get; init; }
    public int? StatorVaneCount { get; init; }
    public double? StatorChordMm { get; init; }

    // --- Tier C: manufacturing / cosmetic (usually fixed; not autotuned initially) ---
    public double? WallThicknessMm { get; init; }
    public double? InletLipLengthMm { get; init; }
    public double? InletContractionLengthMm { get; init; }

    public static NozzleGeometryGenome FromDesignInputs(NozzleDesignInputs d) => new()
    {
        InletDiameterMm = d.InletDiameterMm,
        SwirlChamberDiameterMm = d.SwirlChamberDiameterMm,
        SwirlChamberLengthMm = d.SwirlChamberLengthMm,
        InjectorAxialPositionRatio = d.InjectorAxialPositionRatio,
        ExpanderHalfAngleDeg = d.ExpanderHalfAngleDeg,
        ExpanderLengthMm = d.ExpanderLengthMm,
        ExitDiameterMm = d.ExitDiameterMm,
        StatorVaneAngleDeg = d.StatorVaneAngleDeg,
        StatorAxialLengthMm = d.StatorAxialLengthMm,
        StatorHubDiameterMm = d.StatorHubDiameterMm,
        StatorVaneCount = d.StatorVaneCount,
        StatorChordMm = d.StatorBladeChordMm,
        WallThicknessMm = d.WallThicknessMm,
        InletLipLengthMm = null,
        InletContractionLengthMm = null
    };

    /// <summary>L∞ norm over Tier A fields, normalized by typical scales (for diversity / logging).</summary>
    public static double TierADistance(NozzleGeometryGenome a, NozzleGeometryGenome b)
    {
        static double q(double x, double y, double s) => Math.Abs(x - y) / Math.Max(s, 1e-6);
        return Math.Max(
            Math.Max(
                Math.Max(q(a.InletDiameterMm, b.InletDiameterMm, 50), q(a.SwirlChamberDiameterMm, b.SwirlChamberDiameterMm, 55)),
                Math.Max(q(a.SwirlChamberLengthMm, b.SwirlChamberLengthMm, 80), q(a.InjectorAxialPositionRatio, b.InjectorAxialPositionRatio, 0.35))),
            Math.Max(
                Math.Max(q(a.ExpanderHalfAngleDeg, b.ExpanderHalfAngleDeg, 3), q(a.ExpanderLengthMm, b.ExpanderLengthMm, 80)),
                Math.Max(q(a.ExitDiameterMm, b.ExitDiameterMm, 40), q(a.StatorVaneAngleDeg, b.StatorVaneAngleDeg, 20))));
    }
}
