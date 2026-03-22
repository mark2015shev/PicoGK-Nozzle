using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// First-order entrainment: additional mass flow rate per unit axial length [kg/(s·m)].
/// Not CFD — engineering correlation scale only. Future: shear-layer thickness, density ratio, swirl.
/// </summary>
public sealed class EntrainmentModel
{
    /// <summary>Entrainment coefficient Ce [-], typically O(0.01–0.1).</summary>
    public double Coefficient { get; set; } = 0.07;

    /// <summary>
    /// dṁ_entrained/dx = Ce · ρ_amb · V_jet · P_exposed [kg/(s·m)].
    /// </summary>
    public double ComputeEntrainedMassPerLength(
        double ambientDensityKgM3,
        double localJetVelocityMps,
        double exposedPerimeterM)
    {
        double rho = Math.Max(ambientDensityKgM3, 1e-9);
        double v = Math.Max(Math.Abs(localJetVelocityMps), 0.0);
        double p = Math.Max(exposedPerimeterM, 0.0);
        return Coefficient * rho * v * p;
    }
}
