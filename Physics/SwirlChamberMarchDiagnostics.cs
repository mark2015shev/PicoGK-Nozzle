using System.Collections.Generic;

namespace PicoGK_Run.Physics;

/// <summary>SI march duct + entrainment audit (chamber segment) — not CFD.</summary>
public sealed class SwirlChamberMarchDiagnostics
{
    public double AInletMm2 { get; init; }
    public double AChamberBoreMm2 { get; init; }
    public double AHubMm2 { get; init; }
    public double AFreeChamberMm2 { get; init; }
    public double AInjTotalMm2 { get; init; }
    public double AExitMm2 { get; init; }
    public double RatioInletToChamber { get; init; }
    public double RatioInjToChamber { get; init; }
    public double RatioFreeToChamber { get; init; }
    public double RatioExitToChamber { get; init; }
    public double DuctEffectiveAreaM2 { get; init; }
    public double EntrainmentPerimeterM { get; init; }
    public double CaptureAreaM2 { get; init; }
    public double EntrainmentCeBase { get; init; }
    public double EntrainmentCeAtFirstStep { get; init; }

    /// <summary>B_vortex multiplier applied to correlation demand (1 if swirl entrainment boost disabled in run config).</summary>
    public double EntrainmentMassDemandBoost { get; init; }
    public IReadOnlyList<string> ValidationWarnings { get; init; } = System.Array.Empty<string>();
}
