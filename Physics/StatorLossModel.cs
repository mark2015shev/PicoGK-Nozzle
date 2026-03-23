using System;

namespace PicoGK_Run.Physics;

public sealed class StatorLossResult
{
    public double IncidenceMismatchDeg { get; init; }
    public double TurningLossK { get; init; }
    public double EstimatedTotalPressureLossPa { get; init; }
    public double RecoveryEfficiencyReduction { get; init; }
    public string Notes { get; init; } = "";
}

public static class StatorLossModel
{
    public static StatorLossResult Compute(
        double rhoKgM3,
        double axialVelocityMps,
        double tangentialVelocityMps,
        double statorVaneAngleDeg,
        double impliedSwirlAngleDeg)
    {
        double rho = Math.Max(rhoKgM3, 1e-6);
        double va = Math.Max(Math.Abs(axialVelocityMps), 1e-6);
        double vt = Math.Abs(tangentialVelocityMps);
        double mismatch = Math.Abs(statorVaneAngleDeg - impliedSwirlAngleDeg);
        double dyn = 0.5 * rho * (va * va + vt * vt);

        double kInc = Math.Tanh(mismatch / Math.Max(ChamberPhysicsCoefficients.StatorIncidenceRefDeg, 1.0));
        double kTurn = ChamberPhysicsCoefficients.StatorTurningLossK * (0.55 + 0.45 * kInc);
        double dP = Math.Min(kTurn * dyn, 0.5 * dyn * 8.0);
        double etaRed = Math.Clamp(0.12 * kInc + 0.08 * Math.Tanh(vt / va / 6.0), 0.0, 0.45);

        return new StatorLossResult
        {
            IncidenceMismatchDeg = mismatch,
            TurningLossK = kTurn,
            EstimatedTotalPressureLossPa = dP,
            RecoveryEfficiencyReduction = etaRed,
            Notes = "Incidence vs atan(Vt/Va); loss is first-order diagnostic vs blade CFD."
        };
    }
}
