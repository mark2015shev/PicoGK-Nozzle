namespace PicoGK_Run.Core;

/// <summary>
/// Physics solver outputs only.
/// </summary>
public sealed class NozzleSolvedState
{
    public double CoreMassFlowKgPerSec { get; init; }
    public double SourceAreaMm2 { get; init; }
    public double TotalInjectorAreaMm2 { get; init; }

    /// <summary>
    /// Scalar speed used for yaw/pitch decomposition (magnitude of modeled primary jet at injectors).
    /// </summary>
    public double InjectorJetVelocityMps { get; init; }

    /// <summary>HEURISTIC: V_core × (A_source/A_inj) — drives jet magnitude; not CFD.</summary>
    public double InjectorJetVelocityAreaDriverMps { get; init; }

    /// <summary>mdot/(ρ_core×A_inj) — continuity cross-check / light blend only.</summary>
    public double InjectorJetVelocityContinuityCheckMps { get; init; }

    /// <summary>Heuristic core gas density (ideal gas when T_exhaust set); used for continuity check blend.</summary>
    public double CoreGasDensityKgPerM3 { get; init; }

    public double TangentialVelocityComponentMps { get; init; }
    public double AxialVelocityComponentMps { get; init; }

    /// <summary>
    /// Tangential-to-axial speed ratio of the injection vector (|Vt|/|Va|).
    /// Not a CFD swirl number.
    /// </summary>
    public double InjectorSwirlNumber { get; init; }

    /// <summary>
    /// Chamber-decayed swirl metric for stator heuristic, <b>after</b> inlet + expander pressure
    /// recovery debits on the shared swirl/pressure budget (no double counting).
    /// </summary>
    public double ChamberSwirlNumberForStator { get; init; }

    /// <summary>
    /// HEURISTIC — NOT CFD: magnitude of low-pressure tendency near inlet/core from swirl + entrainment
    /// (suction / capture recovery — not a free-energy source). Not CFD-calibrated.
    /// </summary>
    public double InletSuctionDeltaPPa { get; init; }

    /// <summary>
    /// Effective entrainment multiplier from inlet suction (1.0 = none). Same budget as expander recovery.
    /// </summary>
    public double InletCaptureEfficiency { get; init; }

    /// <summary>
    /// HEURISTIC axial force from inlet annulus × suction Δp; small, capped, debited from pressure budget.
    /// </summary>
    public double InletPressureThrustComponentN { get; init; }

    /// <summary>
    /// Fraction [0,1] of swirl-pressure recovery potential left after inlet debit, before expander wall term.
    /// </summary>
    public double PressureRecoveryBudgetAfterInlet { get; init; }

    /// <summary>
    /// Residual pressure/swirl recovery budget after inlet + expander taps (tangential scale vs undebited reference).
    /// </summary>
    public double RemainingPressureRecoveryBudget { get; init; }

    /// <summary>
    /// HEURISTIC — NOT CFD: estimated wall pressure rise from swirl-induced radial gradient
    /// (order dp/dr ~ rho * v_theta^2 / r), bounded. Swirl-pressure recovery — not centrifugal thrust.
    /// </summary>
    public double SwirlPressureRisePa { get; init; }

    /// <summary>
    /// HEURISTIC axial component of resultant wall force on angled expander surfaces from
    /// <see cref="SwirlPressureRisePa"/>; conservative caps. Same swirl-energy budget as stator.
    /// </summary>
    public double ExpanderWallAxialForceN { get; init; }

    /// <summary>
    /// Fraction of reference tangential kinetic energy notionally tapped for pressure recovery [0, ~0.35].
    /// Heuristic bookkeeping — not CFD-calibrated.
    /// </summary>
    public double SwirlPressureRecoveryEfficiency { get; init; }

    /// <summary>Tangential speed scale in mixed flow after pressure-recovery debit (before stator), m/s.</summary>
    public double RemainingTangentialVelocityAfterPressureRecovery { get; init; }

    /// <summary>Control-volume momentum flux term: mdot_mix * V_exit.</summary>
    public double MomentumThrustComponentN { get; init; }

    /// <summary>Sum of expander wall + inlet pressure thrust (axial CV terms), separate from momentum.</summary>
    public double PressureThrustComponentN { get; init; }

    public PressureLossBreakdown PressureLoss { get; init; }

    /// <summary>Which named loss fraction is largest (area / swirl_dissipation / short_mixing_length).</summary>
    public string DominantPressureLossContribution { get; init; } = "";

    public double AmbientAirMassFlowKgPerSec { get; init; }
    public double EntrainmentRatio { get; init; }
    public double MixedMassFlowKgPerSec { get; init; }
    public double MixedVelocityMps { get; init; }

    public double ExpansionEfficiency { get; init; }
    public double AxialRecoveryEfficiency { get; init; }
    public double ExitVelocityMps { get; init; }

    /// <summary>First-order: mdot_core * V_core (no mixed-stream pressure term).</summary>
    public double SourceOnlyThrustN { get; init; }

    /// <summary>Momentum thrust + swirl-pressure wall term (see components).</summary>
    public double FinalThrustN { get; init; }
    public double ExtraThrustN { get; init; }
    public double ThrustGainRatio { get; init; }
}
