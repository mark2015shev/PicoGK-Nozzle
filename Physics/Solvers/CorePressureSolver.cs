using PicoGK_Run.Physics;

namespace PicoGK_Run.Physics.Solvers;

/// <summary>
/// Stage 3 — explicit radial equilibrium wrapper: dp/dr ≈ ρ V_θ²/r (piecewise forced core + free-vortex shell).
/// See <see cref="RadialVortexPressureModel"/> for formulas; this class adds an audit string for reports.
/// </summary>
public static class CorePressureSolver
{
    public const string FormulaSummary =
        "Mixed model: forced v_θ=Ωr (r≤r_core), free v_θ=Γ/r (r>r_core), Ω=Γ/r_core², Γ≈|V_t|·R_wall; "
        + "Δp_wall-axis ≈ 0.5ρΩ²r_core² + 0.5ρΓ²(1/r_core²−1/R²) capped; core depression from blend vs ρV_t². "
        + "Not CFD — see RadialVortexPressureModel.Compute.";

    public static RadialVortexPressureResult Solve(
        double densityKgM3,
        double representativeTangentialVelocityMps,
        double chamberRadiusM,
        double coreRadiusFraction,
        double capPa) =>
        RadialVortexPressureModel.Compute(
            densityKgM3,
            representativeTangentialVelocityMps,
            chamberRadiusM,
            coreRadiusFraction,
            capPa);
}
