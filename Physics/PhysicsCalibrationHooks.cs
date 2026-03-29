namespace PicoGK_Run.Physics;

/// <summary>
/// Named calibration scales for reduced-order closures (entrainment C_d lives in <see cref="ChamberPhysicsCoefficients"/>).
/// Central reference pressures for pressure-margin → 0–1 risk maps (not CFD).
/// </summary>
public static class PhysicsCalibrationHooks
{
    /// <summary>Scale [Pa]: wall static above inlet-lip / capture reference → inlet spill risk.</summary>
    public static double InletSpillMarginReferencePa { get; set; } = 8200.0;

    /// <summary>Gain [-]: maps normalized wall-vs-inlet margin to <see cref="SwirlSegment.SpillTendencyEstimate.InletSpillRisk01"/>.</summary>
    public static double InletSpillRiskLinearGain { get; set; } = 0.55;

    /// <summary>Scale [Pa]: |negative exit drive margin| → downstream drive risk.</summary>
    public static double ExitDriveWeaknessReferencePa { get; set; } = 9500.0;

    /// <summary>Gain [-] for downstream drive risk.</summary>
    public static double ExitDriveRiskLinearGain { get; set; } = 0.55;

    /// <summary>Above this relative continuity residual, optimization penalties ramp (march).</summary>
    public static double ChamberContinuityResidualPenaltyThreshold { get; set; } = 0.02;

    /// <summary>Weight for march continuity residual in composite penalty scaling.</summary>
    public static double ChamberContinuityResidualPenaltyWeight { get; set; } = 1.25;

    /// <summary>Scale for axial-momentum budget residual (mixed-stream control volume).</summary>
    public static double AxialMomentumBudgetResidualPenaltyWeight { get; set; } = 0.9;

    /// <summary>Scale for |Ġ_θ − ṁ r V_t| / |Ġ_θ| closure check.</summary>
    public static double AngularMomentumFluxClosurePenaltyWeight { get; set; } = 0.85;
}
