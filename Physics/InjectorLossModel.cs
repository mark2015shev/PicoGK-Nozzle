using System;

namespace PicoGK_Run.Physics;

/// <summary>Discharge / turning loss at injector — first-order.</summary>
public sealed class InjectorLossResult
{
    public double DischargeCoefficient { get; init; }
    public double TurningLossCoefficientK { get; init; }
    public double EffectiveVelocityRatio { get; init; }
    public double EstimatedTotalPressureLossPa { get; init; }
    public string Notes { get; init; } = "";
}

public static class InjectorLossModel
{
    /// <summary>Δp_loss ≈ K * 0.5 * rho * V^2 with K from turning; Cd scales effective dynamic head reference.</summary>
    public static InjectorLossResult Compute(double rhoKgM3, double jetVelocityMps, double injectorYawDeg)
    {
        double rho = Math.Max(rhoKgM3, 1e-6);
        double v = Math.Max(Math.Abs(jetVelocityMps), 1e-6);
        double cd = Math.Clamp(ChamberPhysicsCoefficients.InjectorDischargeCoefficient, 0.5, 1.0);
        double kTurn = ChamberPhysicsCoefficients.InjectorTurningLossK
                       * Math.Sin(injectorYawDeg * (Math.PI / 180.0));
        kTurn = Math.Clamp(kTurn, 0.0, 0.55);
        double dyn = 0.5 * rho * v * v;
        double dP = Math.Min((kTurn + (1.0 / cd - 1.0) * 0.35) * dyn, 0.45 * dyn * 12.0);

        return new InjectorLossResult
        {
            DischargeCoefficient = cd,
            TurningLossCoefficientK = kTurn,
            EffectiveVelocityRatio = Math.Sqrt(Math.Max(1.0 - dP / Math.Max(dyn * 4.0, 1.0), 0.2)),
            EstimatedTotalPressureLossPa = dP,
            Notes =
                "Δp_loss from turning + Cd head; SI path uses EffectiveJetVelocityMps (Cd·V·√(1−K_turn)) for coupled inlet."
        };
    }

    /// <summary>
    /// Coupled effective jet speed [m/s]: V_eff = Cd·|V_in|·√max(0, 1 − K_turn) with K_turn from yaw.
    /// Assumes loss acts like a head cut on |V| (first-order; not CFD).
    /// </summary>
    public static double EffectiveJetVelocityMps(double jetVelocityMps, double rhoKgM3, double injectorYawDeg)
    {
        var loss = Compute(rhoKgM3, jetVelocityMps, injectorYawDeg);
        double v = Math.Max(Math.Abs(jetVelocityMps), 1e-6);
        double kTurn = Math.Clamp(loss.TurningLossCoefficientK, 0.0, 0.95);
        double sqrtBracket = Math.Sqrt(Math.Max(0.0, 1.0 - kTurn));
        double vEff = loss.DischargeCoefficient * v * sqrtBracket;
        if (double.IsNaN(vEff) || double.IsInfinity(vEff))
            vEff = 1e-6;
        return Math.Max(vEff, 1e-6);
    }
}
