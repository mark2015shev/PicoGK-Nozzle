using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// First-order swirl energy recovery into static pressure rise and bounded axial kinetic gain — not CFD.
/// Prefer <see cref="Apply(StatorRecoverySiInput, double, double)"/> so stator entry is explicitly tied to SI march state.
/// </summary>
public readonly record struct StatorRecoverySiInput(
    double TangentialVelocityMps,
    double AxialVelocityMps,
    double StaticDensityKgM3,
    double StaticTemperatureK);

public sealed class StatorRecoveryModel
{
    /// <summary>SI stator entry: mixed tangential speed, axial speed, and statics at chamber exit plane.</summary>
    public StatorRecoveryOutput Apply(
        in StatorRecoverySiInput si,
        double etaStator,
        double fractionOfTangentialRetained)
    {
        return ApplyCore(si.TangentialVelocityMps, si.StaticDensityKgM3, etaStator, fractionOfTangentialRetained);
    }

    /// <summary>Legacy three-argument entry; same physics as <see cref="Apply(in StatorRecoverySiInput, double, double)"/>.</summary>
    public StatorRecoveryOutput Apply(
        double tangentialVelocityInMps,
        double densityKgM3,
        double etaStator,
        double fractionOfTangentialRetained)
    {
        return ApplyCore(tangentialVelocityInMps, densityKgM3, etaStator, fractionOfTangentialRetained);
    }

    /// <summary>Recovered specific energy ≈ η · 0.5 · (Vt_in² - Vt_out²); split between pressure and axial velocity.</summary>
    private static StatorRecoveryOutput ApplyCore(
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
        double fractionToPressure = ChamberPhysicsCoefficients.StatorRecoveryFractionToPressure;
        fractionToPressure = Math.Clamp(fractionToPressure, 0.35, 0.85);
        double dP = rho * eRecover * fractionToPressure;
        double dPCap = ChamberPhysicsCoefficients.StatorRecoveryPressureRiseCapFactor * rho * deltaKe * 2.0;
        dP = Math.Min(dP, dPCap);
        double dvAxial = Math.Sqrt(Math.Max(0.0, 2.0 * eRecover * (1.0 - fractionToPressure)));
        double dvCap = ChamberPhysicsCoefficients.StatorRecoveryAxialGainCapPerDeltaVt * Math.Abs(vtIn - vtOut);
        dvAxial = Math.Min(dvAxial, dvCap);
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
