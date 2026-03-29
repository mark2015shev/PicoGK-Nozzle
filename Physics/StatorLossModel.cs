using System;

namespace PicoGK_Run.Physics;

public sealed class StatorLossResult
{
    /// <summary>|β_flow − β_metal| from velocity triangle [deg].</summary>
    public double IncidenceMismatchDeg { get; init; }

    public double TurningLossK { get; init; }
    public double EstimatedTotalPressureLossPa { get; init; }
    public double RecoveryEfficiencyReduction { get; init; }
    public StatorInletTriangle InletTriangle { get; init; }
    public StatorLossBreakdown LossBreakdown { get; init; }
    public string Notes { get; init; } = "";
}

public static class StatorLossModel
{
    /// <summary>
    /// Velocity-triangle incidence (β_flow = atan2(V_t, V_x) vs vane metal angle); stagnation losses scaled by K_inc/K_turn.
    /// </summary>
    public static StatorLossResult Compute(
        double rhoKgM3,
        double axialVelocityMps,
        double tangentialVelocityMps,
        double statorVaneMetalAngleDeg)
    {
        double rho = Math.Max(rhoKgM3, 1e-6);
        StatorInletTriangle tri = StatorVelocityTriangleModel.BuildInlet(
            axialVelocityMps,
            tangentialVelocityMps,
            statorVaneMetalAngleDeg);
        double va = Math.Max(Math.Abs(axialVelocityMps), 1e-6);
        double vt = Math.Abs(tangentialVelocityMps);
        double mismatch = Math.Abs(tri.IncidenceDeg);
        double dyn = 0.5 * rho * (va * va + vt * vt);

        double kInc = Math.Tanh(mismatch / Math.Max(ChamberPhysicsCoefficients.StatorIncidenceRefDeg, 1.0));
        double kTurn = ChamberPhysicsCoefficients.StatorTurningLossK * (0.55 + 0.45 * kInc);
        double dP = Math.Min(kTurn * dyn, 0.5 * dyn * 8.0);
        double etaRed = Math.Clamp(0.12 * kInc + 0.08 * Math.Tanh(vt / va / 6.0), 0.0, 0.45);

        double wSum = Math.Max(kInc + kTurn, 1e-9);
        double dPInc = dP * (kInc / wSum);
        double dPTurn = Math.Max(0.0, dP - dPInc);

        return new StatorLossResult
        {
            IncidenceMismatchDeg = mismatch,
            TurningLossK = kTurn,
            EstimatedTotalPressureLossPa = dP,
            RecoveryEfficiencyReduction = etaRed,
            InletTriangle = tri,
            LossBreakdown = new StatorLossBreakdown(dPInc, dPTurn, dP),
            Notes =
                "Velocity-triangle stator model: incidence from β_flow−β_metal; losses split for reporting; η coupling unchanged."
        };
    }
}
