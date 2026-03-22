namespace PicoGK_Run.Core;

/// <summary>
/// Four dimensionless groups (plus supporting numbers) that control whether a swirl–entrainment
/// nozzle is in a plausible operating envelope. <b>Heuristic guidance only — not CFD pass/fail.</b>
/// </summary>
public sealed class NozzleCriticalRatiosSnapshot
{
    /// <summary>
    /// <b>R1 — Capture openness.</b> σ = (D_inlet / D_chamber)² = A_inlet / A_chamber (nominal IDs).
    /// Larger σ → bigger inlet relative to bore (usually easier ambient capture); very small σ → narrow mouth vs chamber.
    /// </summary>
    public double CaptureToChamberAreaRatio { get; init; }

    /// <summary>
    /// <b>R2 — Swirl injection intensity.</b> S = |V_t|/|V_ax| at the injector from yaw/pitch (same as physics swirl number).
    /// Too low → weak vortex / suction tendency; very high → little axial jet, hard to feed the chamber.
    /// </summary>
    public double InjectorSwirlNumber { get; init; }

    /// <summary>
    /// <b>R3 — Chamber slenderness.</b> Λ = L_chamber / D_chamber. Mixing and entrainment length vs diameter.
    /// Very short → incomplete mixing; very long → extra friction / decay without guaranteed gain.
    /// </summary>
    public double ChamberSlendernessLD { get; init; }

    /// <summary>
    /// Port area vs chamber cross-section: A_inj_total / A_chamber (blockage / momentum entry scale).
    /// </summary>
    public double InjectorPortToChamberAreaRatio { get; init; }

    /// <summary>
    /// <b>R4a — Expander divergence.</b> Requested half-angle [deg]. Large angles risk separation in real flow.
    /// </summary>
    public double ExpanderHalfAngleDeg { get; init; }

    /// <summary>Inner radius at expander exit from geometry: R_ch + L_exp × tan(half-angle) [mm].</summary>
    public double ExpanderEndInnerRadiusMm { get; init; }

    /// <summary>Target inner radius at duct exit from <see cref="Parameters.NozzleDesignInputs.ExitDiameterMm"/>/2 [mm].</summary>
    public double ExitTargetInnerRadiusMm { get; init; }

    /// <summary>
    /// <b>R4b — Expander vs exit sizing.</b> |R_exp_end − R_exit_target| / R_chamber_inner. Large values mean the exit section must taper a lot.
    /// </summary>
    public double ExpanderExitToTargetRadiusMismatchRatio { get; init; }

    /// <summary>
    /// <b>R4c — Stator / injector alignment hint.</b> |stator vane angle − injector yaw| [deg] (first-order; real blades need blade metal angle).
    /// </summary>
    public double StatorToInjectorYawMismatchDeg { get; init; }

    /// <summary>ṁ_ambient / ṁ_core after SI march when available.</summary>
    public double? SolvedEntrainmentRatio { get; init; }
}
