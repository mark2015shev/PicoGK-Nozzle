using System;

namespace PicoGK_Run.Physics;

/// <summary>First-order swirling diffuser / expander recovery — not CFD.</summary>
public sealed class SwirlDiffuserRecoveryResult
{
    /// <summary>Cp = (p_out - p_in) / (0.5 rho u_ref^2), heuristic.</summary>
    public double EstimatedPressureRecoveryCoefficient { get; init; }
    public double SeparationRiskScore { get; init; }
    public double EffectivePressureRecoveryEfficiency { get; init; }
    public string Notes { get; init; } = "";
}

public static class SwirlDiffuserRecoveryModel
{
    public static SwirlDiffuserRecoveryResult Compute(
        double halfAngleDeg,
        double expanderLengthMm,
        double chamberDiameterMm,
        double exitDiameterMm,
        double rhoKgM3,
        double axialVelocityRefMps,
        double injectorSwirlNumber,
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

        double s = injectorSwirlNumber;
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

        string notes =
            "Cp from angle, L/D_eff, area ratio; moderate swirl aids separation resistance in model; not CFD.";

        return new SwirlDiffuserRecoveryResult
        {
            EstimatedPressureRecoveryCoefficient = cp,
            SeparationRiskScore = sep,
            EffectivePressureRecoveryEfficiency = eff,
            Notes = notes
        };
    }
}
