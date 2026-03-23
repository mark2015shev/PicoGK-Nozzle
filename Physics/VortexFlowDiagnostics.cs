namespace PicoGK_Run.Physics;

/// <summary>
/// First-order chamber vortex / radial pressure bookkeeping for the SI path — not CFD.
/// Intended for engineering interpretation and future calibration hooks.
/// </summary>
public sealed class VortexFlowDiagnostics
{
    /// <summary>Estimated low-pressure tendency at vortex core vs wall reference [Pa].</summary>
    public double CorePressureDepressionPa { get; init; }

    /// <summary>Estimated wall-side static pressure rise vs core reference from ∫dp/dr ~ ρ·Vθ²/r [Pa].</summary>
    public double WallPressureRisePa { get; init; }

    /// <summary>1 − (Vθ_primary,end / Vθ_injector)² — tangential energy lost on the primary stream to modeled chamber decay.</summary>
    public double SwirlDecayFractionAlongChamber { get; init; }

    /// <summary>Fraction of injector |Vθ|² scale still present in mixed flow at stator plane (pre-stator), [0,1].</summary>
    public double RemainingSwirlFractionAtStator { get; init; }

    /// <summary>Share of reference swirl budget attributed to organized entrainment coupling (heuristic).</summary>
    public double FractionSwirlForEntrainment { get; init; }

    /// <summary>Share remaining as identifiable tangential kinetic content at stator entry.</summary>
    public double FractionSwirlRemainingAtStator { get; init; }

    /// <summary>Share attributed to stator / axial recovery pathway (first-order).</summary>
    public double FractionSwirlToAxialRecovery { get; init; }

    /// <summary>Share attributed to wall / turbulent / unmodeled dissipation along the chamber.</summary>
    public double FractionSwirlDissipated { get; init; }

    /// <summary>Alias for reporting: same as <see cref="FractionSwirlToAxialRecovery"/>.</summary>
    public double EstimatedRecoveryFraction => FractionSwirlToAxialRecovery;

    public VortexStructureClass StructureClass { get; init; }

    public string StructureClassLabel { get; init; } = "";

    /// <summary>0–1 composite: moderate swirl, stable regime, useful entrainment, some recovery — not physical accuracy.</summary>
    public double VortexQualityMetric { get; init; }

    /// <summary>Per-step decay factor used in the march (for audit).</summary>
    public double SwirlDecayPerStepFactorUsed { get; init; }
}
