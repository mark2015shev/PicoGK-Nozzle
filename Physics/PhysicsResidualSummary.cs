namespace PicoGK_Run.Physics;

/// <summary>
/// Conservation-style residual aggregates for the SI chamber march and exit control volume (reduced-order checks).
/// </summary>
public sealed class PhysicsResidualSummary
{
    /// <summary>max_x |ρ A V_a − ṁ| / max(ṁ, ε) over march steps.</summary>
    public double MaxChamberContinuityResidualRelative { get; init; }

    /// <summary>Mean of per-step continuity residual.</summary>
    public double MeanChamberContinuityResidualRelative { get; init; }

    /// <summary>
    /// Mixed axial-momentum CV: |ṁ_new V_a,new − (ṁ_old V_a,old + Δṁ V_ent)| / max(|ṁ_old V_a,old|, ε) per step; this is the max.
    /// </summary>
    public double MaxChamberAxialMomentumBudgetResidualRelative { get; init; }

    /// <summary>|Ġ_θ − ṁ r V_t| / max(|Ġ_θ|, ε) at worst march step.</summary>
    public double MaxChamberAngularMomentumFluxClosureResidualRelative { get; init; }

    /// <summary>|ṁ_exit − ρ_exit A_exit V_exit| / max(ṁ_exit, ε) at nozzle exit CV.</summary>
    public double ExitControlVolumeMassFluxResidualRelative { get; init; }
}
