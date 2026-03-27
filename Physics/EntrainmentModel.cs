using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// First-order entrainment: additional mass flow rate per unit axial length [kg/(s·m)].
/// Not CFD — engineering correlation scale only. Ce may vary with swirl, L/D, and optional Re.
/// </summary>
public sealed class EntrainmentModel
{
    /// <summary>Base entrainment coefficient Ce_base [-], typically O(0.01–0.1).</summary>
    public double Coefficient { get; set; } = 0.07;

    /// <summary>
    /// Ce = Ce_base · f(S) · f(L/D) · f(Re). Use flux swirl S = Ġ_θ/(R ṁ V_ax); legacy |V_t|/|V_a| is not recommended.
    /// </summary>
    public double ComputeCoefficient(
        double swirlCorrelationInput,
        double chamberLdRatio,
        double reynoldsApprox,
        bool useReynoldsFactor)
    {
        double s = Math.Clamp(Math.Abs(swirlCorrelationInput), 0.0, 25.0);
        double ld = Math.Max(chamberLdRatio, 0.0);
        double fS = 1.0
            + ChamberPhysicsCoefficients.EntrainmentSwirlGainK
            * Math.Tanh(s / Math.Max(ChamberPhysicsCoefficients.EntrainmentSwirlGainS0, 0.05));
        double fLd = 1.0
            + ChamberPhysicsCoefficients.EntrainmentLdGain
            * Math.Tanh(ld / Math.Max(ChamberPhysicsCoefficients.EntrainmentLdRef, 0.1));
        double fRe = 1.0;
        if (useReynoldsFactor && reynoldsApprox > 10.0)
        {
            double ratio = reynoldsApprox / Math.Max(ChamberPhysicsCoefficients.EntrainmentReRef, 1.0);
            fRe = 1.0
                + ChamberPhysicsCoefficients.EntrainmentReGain
                * Math.Tanh(Math.Log10(Math.Max(ratio, 1e-3)));
        }

        return Coefficient * fS * fLd * fRe;
    }

    /// <summary>
    /// dṁ_entrained/dx = Ce · ρ_amb · V_jet · P_exposed [kg/(s·m)].
    /// </summary>
    public double ComputeEntrainedMassPerLength(
        double ceEffective,
        double ambientDensityKgM3,
        double localJetVelocityMps,
        double exposedPerimeterM)
    {
        double ce = Math.Max(ceEffective, 0.0);
        double rho = Math.Max(ambientDensityKgM3, 1e-9);
        double v = Math.Max(Math.Abs(localJetVelocityMps), 0.0);
        double p = Math.Max(exposedPerimeterM, 0.0);
        return ce * rho * v * p;
    }
}
