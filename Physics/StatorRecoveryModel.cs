using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// First-order swirl energy recovery into static pressure rise and bounded axial kinetic gain — not CFD.
/// </summary>
public sealed class StatorRecoveryModel
{
    /// <summary>
    /// Recovered specific energy ≈ η · 0.5 · (Vt_in² - Vt_out²); split between pressure and axial velocity.
    /// </summary>
    public StatorRecoveryOutput Apply(
        double tangentialVelocityInMps,
        double densityKgM3,
        double etaStator,
        double fractionOfTangentialRetained)
    {
        double vtIn = tangentialVelocityInMps;
        double f = Math.Clamp(fractionOfTangentialRetained, 0.0, 1.0);
        double eta = Math.Clamp(etaStator, 0.0, 0.95);
        double vtOut = vtIn * f;
        double rho = Math.Max(densityKgM3, 1e-9);
        double deltaKe = 0.5 * (vtIn * vtIn - vtOut * vtOut);
        deltaKe = Math.Max(deltaKe, 0.0);
        double eRecover = eta * deltaKe;
        double fractionToPressure = 0.65;
        double dP = rho * eRecover * fractionToPressure;
        dP = Math.Min(dP, 0.25 * rho * deltaKe * 2.0);
        double dvAxial = Math.Sqrt(Math.Max(0.0, 2.0 * eRecover * (1.0 - fractionToPressure)));
        dvAxial = Math.Min(dvAxial, 0.35 * Math.Abs(vtIn - vtOut));
        return new StatorRecoveryOutput
        {
            RecoveredPressureRisePa = Math.Max(dP, 0.0),
            RemainingTangentialVelocityMps = vtOut,
            AxialVelocityGainMps = dvAxial
        };
    }
}

public sealed class StatorRecoveryOutput
{
    public double RecoveredPressureRisePa { get; init; }
    public double RemainingTangentialVelocityMps { get; init; }
    public double AxialVelocityGainMps { get; init; }
}
