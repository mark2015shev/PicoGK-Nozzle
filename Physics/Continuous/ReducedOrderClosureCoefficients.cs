using System.Collections.Generic;

namespace PicoGK_Run.Physics.Continuous;

/// <summary>
/// Named empirical closures for the continuous nozzle path. Defaults are engineering placeholders —
/// calibrate vs CFD, taps, and thrust stand. First-principles terms live in the stepper formulas;
/// these scale dissipation and separation risk only.
/// </summary>
public sealed class ReducedOrderClosureCoefficients
{
    /// <summary>Wall / shear-like torque on Ġ_θ per (Δx/D_h) [-]. Source: match azimuthal decay vs CFD.</summary>
    public double SwirlWallFrictionCoeff { get; init; } = 0.14;

    /// <summary>Extra Ġ_θ decay per step from turbulent mixing [-]. Source: PIV / CFD mixing layer.</summary>
    public double MixingDecayCoeff { get; init; } = 0.04;

    /// <summary>Static pressure penalty from separation risk × dynamic head [-]. Source: diffuser literature / CFD.</summary>
    public double DiffuserSeparationLossCoeff { get; init; } = 0.38;

    /// <summary>Scales isentropic-like recovery from area growth (subsonic diffuser) [-]. Source: CFD pressure recovery.</summary>
    public double DiffuserIdealRecoveryEfficiency { get; init; } = 0.55;

    /// <summary>Maps swirl kinetic change into static pressure (swirl relief) [-]. Source: CFD static tap gradient.</summary>
    public double SwirlReliefToStaticPressureCoeff { get; init; } = 0.65;

    /// <summary>Distributed entrainment in expander: 0 = off (v1 default); future calibration [-].</summary>
    public double ExpanderEntrainmentCaptureCoeff { get; init; } = 0.0;

    /// <summary>Inlet-side coupling gain on capture pressure deficit (tied to global min inlet static) [-].</summary>
    public double InletGlobalCouplingPressureGain { get; init; } = 1.0;

    /// <summary>Static pressure loss from wall friction ∝ factor·(Δx/D_h)·q [-]. Source: pipe / diffuser correlations.</summary>
    public double DiffuserWallFrictionFactor { get; init; } = 0.032;

    public static ReducedOrderClosureCoefficients Default => new();

    /// <summary>Human-readable calibration ledger (names, units, defaults).</summary>
    public static IReadOnlyList<string> FormatCalibrationLedger()
    {
        ReducedOrderClosureCoefficients d = Default;
        return new[]
        {
            "REDUCED-ORDER CLOSURES (calibrate explicitly; not CFD truth)",
            $"  SwirlWallFrictionCoeff [-]        default={d.SwirlWallFrictionCoeff:F3}  — Ġ_θ wall torque ∝ coeff·(Δx/D_h)·|Ġ_θ|",
            $"  MixingDecayCoeff [-]              default={d.MixingDecayCoeff:F3}  — extra Ġ_θ decay per expander step",
            $"  DiffuserSeparationLossCoeff [-]   default={d.DiffuserSeparationLossCoeff:F3}  — Δp_loss ∝ coeff·S_sep·q",
            $"  DiffuserIdealRecoveryEfficiency [-] default={d.DiffuserIdealRecoveryEfficiency:F3}  — scales area–isentropic Δp",
            $"  SwirlReliefToStaticPressureCoeff [-] default={d.SwirlReliefToStaticPressureCoeff:F3}  — swirl deceleration → static",
            $"  ExpanderEntrainmentCaptureCoeff [-] default={d.ExpanderEntrainmentCaptureCoeff:F3}  — distributed ṁ_ent (0=off)",
            $"  InletGlobalCouplingPressureGain [-] default={d.InletGlobalCouplingPressureGain:F3}  — ties inlet row to global min P",
            $"  DiffuserWallFrictionFactor [-]    default={d.DiffuserWallFrictionFactor:F3}  — Δp_fric ∝ factor·(Δx/D_h)·q"
        };
    }
}
