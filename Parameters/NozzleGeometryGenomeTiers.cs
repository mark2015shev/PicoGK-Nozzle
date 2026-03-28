namespace PicoGK_Run.Parameters;

/// <summary>
/// Documents which genome fields belong to each autotune tier. Tier C is intentionally not searched by default.
/// </summary>
public static class NozzleGeometryGenomeTiers
{
    /// <summary>
    /// Tier A — primary physics: inlet, chamber, injector station, expander path, exit, stator metal angle.
    /// </summary>
    public const string TierADescription =
        "InletDiameterMm, SwirlChamberDiameterMm, SwirlChamberLengthMm, InjectorAxialPositionRatio, " +
        "ExpanderHalfAngleDeg, ExpanderLengthMm, ExitDiameterMm, StatorVaneAngleDeg";

    /// <summary>Tier B — recovery / fit: stator row extent, hub, vane count, chord.</summary>
    public const string TierBDescription =
        "StatorAxialLengthMm, StatorHubDiameterMm, StatorVaneCount, StatorChordMm";

    /// <summary>Tier C — manufacturing / cosmetic: wall, lip, contraction lengths, debug markers.</summary>
    public const string TierCDescription =
        "WallThicknessMm, InletLipLengthMm, InletContractionLengthMm (and non-genome CAD cosmetics)";
}
