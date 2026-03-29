using System;

namespace PicoGK_Run.Physics;

/// <summary>Reduced-order swirling diffuser / expander recovery — not CFD.</summary>
public sealed class SwirlDiffuserRecoveryResult
{
    /// <summary>Cp = Δp_recovery / (0.5 ρ u_ref²) before separation attenuation.</summary>
    public double EstimatedPressureRecoveryCoefficient { get; init; }

    public double SeparationRiskScore { get; init; }

    /// <summary>Attenuated recovery efficiency (0–1) after separation risk.</summary>
    public double EffectivePressureRecoveryEfficiency { get; init; }

    /// <summary>Δp_recovery ≈ Cp · q̄ · (1 − k_sep·SeparationRisk) [Pa] (reduced-order).</summary>
    public double ExpanderPressureRecoveryPa { get; init; }

    /// <summary>F_ax ≈ Δp_recovery · A_proj,ax on diverging annulus projection [N].</summary>
    public double ExpanderWallAxialForceFromPressureN { get; init; }

    /// <summary>Same as <see cref="EffectivePressureRecoveryEfficiency"/> — momentum redirection bookkeeping.</summary>
    public double MomentumRedirection01 { get; init; }

    public double AxialProjectedAreaM2 { get; init; }

    public string Notes { get; init; } = "";
}

public static class SwirlDiffuserRecoveryModel
{
    /// <param name="swirlCorrelationNumber">Prefer flux swirl S from SI march; |V_t|/|V_a| is legacy correlation only.</param>
    public static SwirlDiffuserRecoveryResult Compute(
        double halfAngleDeg,
        double expanderLengthMm,
        double chamberDiameterMm,
        double exitDiameterMm,
        double rhoKgM3,
        double axialVelocityRefMps,
        double swirlCorrelationNumber,
        double mixedTangentialPreExpanderMps)
    {
        double rho = Math.Max(rhoKgM3, 1e-6);
        double u = Math.Max(Math.Abs(axialVelocityRefMps), 1e-6);
        double dyn = 0.5 * rho * u * u;

        double ld = expanderLengthMm / Math.Max(exitDiameterMm - chamberDiameterMm, 5.0);
        ld = Math.Clamp(ld, 0.15, 6.0);
        double ar = exitDiameterMm / Math.Max(chamberDiameterMm, 1e-6);

        double angleRad = halfAngleDeg * (Math.PI / 180.0);
        double baseCp = 0.18 * Math.Tanh(ld / 2.2) * (1.0 - 0.35 * Math.Tanh((halfAngleDeg - 7.0) / 8.0));
        baseCp *= Math.Clamp(1.0 - 0.12 * Math.Tanh((ar - 1.45) / 0.9), 0.55, 1.0);

        double s = swirlCorrelationNumber;
        double swirlHelp = ChamberPhysicsCoefficients.DiffuserSwirlHelpMax
                           * Math.Exp(-0.5 * Math.Pow((s - 2.8) / 1.6, 2));
        double swirlHurt = 0.14 * Math.Tanh((s - 5.2) / 2.0);

        double cp = Math.Clamp(baseCp + swirlHelp - swirlHurt, 0.0, ChamberPhysicsCoefficients.DiffuserCpMax);

        double sepAngle = Math.Tanh((halfAngleDeg - ChamberPhysicsCoefficients.DiffuserSeparationAngleRefDeg) / 5.5);
        double sepAx = Math.Tanh((35.0 - u) / 28.0);
        double sepSwirlLow = Math.Tanh((1.25 - s) / 1.8);
        double sepSwirlHigh = Math.Tanh((s - 5.8) / 2.2);
        double sep = Math.Clamp(0.32 * sepAngle + 0.28 * sepAx + 0.22 * sepSwirlLow + 0.18 * sepSwirlHigh, 0.0, 1.0);

        double eff = Math.Clamp(cp * (1.0 - 0.55 * sep), 0.0, 1.0);

        double rCh = chamberDiameterMm * 0.5e-3;
        double rEx = exitDiameterMm * 0.5e-3;
        double halfRad = angleRad;
        double ring = Math.PI * Math.Max(rEx * rEx - rCh * rCh, 0.0);
        double aProj = Math.Max(ring * Math.Sin(Math.Max(halfRad, 0.03)), 1e-9);

        double dpRecovery = cp * dyn * eff;
        double fWall = PressureForceMath.ExpanderOverPressureAxialForce(dpRecovery, aProj);

        string notes =
            "Reduced-order: Cp(L/D, angle, AR) with swirl modifiers; separation risk attenuates recovery; pressure-force estimate F=Δp·A_proj,ax; not CFD.";

        return new SwirlDiffuserRecoveryResult
        {
            EstimatedPressureRecoveryCoefficient = cp,
            SeparationRiskScore = sep,
            EffectivePressureRecoveryEfficiency = eff,
            ExpanderPressureRecoveryPa = dpRecovery,
            ExpanderWallAxialForceFromPressureN = fWall,
            MomentumRedirection01 = eff,
            AxialProjectedAreaM2 = aProj,
            Notes = notes
        };
    }
}
