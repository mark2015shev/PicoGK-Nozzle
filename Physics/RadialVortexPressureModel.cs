using System;

namespace PicoGK_Run.Physics;

/// <summary>Which radial v_theta profile was used for the pressure integral (first-order).</summary>
public enum RadialVortexPressureModelType
{
    /// <summary>Forced core r&lt;=r_core, free-vortex tail r&gt;r_core, matched at r_core.</summary>
    MixedPiecewise,
    ForcedDominated,
    FreeDominated
}

/// <summary>Outputs from the radial equilibrium model dp/dr = rho * v_theta^2 / r (axisymmetric, small Vr).</summary>
public sealed class RadialVortexPressureResult
{
    public double CoreRadiusM { get; init; }
    public double ChamberRadiusM { get; init; }
    /// <summary>Wall vs core reference: positive means wall higher than core [Pa].</summary>
    public double WallPressureRisePa { get; init; }
    /// <summary>Magnitude of core low-pressure tendency vs freestream reference [Pa].</summary>
    public double CorePressureDropPa { get; init; }
    /// <summary>|Δp| scale used for reporting / tuning normalization.</summary>
    public double EstimatedRadialPressureDeltaPa { get; init; }
    public RadialVortexPressureModelType VortexType { get; init; }
    public string Notes { get; init; } = "";
}

/// <summary>First-order radial pressure structure in a swirl chamber — not CFD.</summary>
public static class RadialVortexPressureModel
{
    /// <summary>
    /// Mixed model: forced v_theta = Omega*r for r&lt;=r_core; free v_theta = Gamma/r for r&gt;r_core.
    /// Omega and Gamma matched so v_theta is continuous at r_core using representative outer tangential speed.
    /// </summary>
    public static RadialVortexPressureResult Compute(
        double densityKgM3,
        double representativeTangentialVelocityMps,
        double chamberRadiusM,
        double coreRadiusFraction,
        double capPa)
    {
        double rho = Math.Max(densityKgM3, 1e-6);
        double rWall = Math.Max(chamberRadiusM, 1e-4);
        double fCore = Math.Clamp(coreRadiusFraction, 0.08, 0.45);
        double rCoreGeom = rWall * fCore;
        double rCoreMin = Math.Max(5e-4, 0.02 * rWall);
        double rCore = Math.Max(rCoreGeom, rCoreMin);

        double vtRef = Math.Min(Math.Abs(representativeTangentialVelocityMps), 420.0);
        double gamma = Math.Max(vtRef * rWall, 1e-8);
        double omega = gamma / (rCore * rCore);

        // Forced: p(r)-p(0) = 0.5 rho Omega^2 r^2
        double pForcedCore = 0.5 * rho * omega * omega * rCore * rCore;

        // Free: p(r2)-p(r1) = 0.5 rho Gamma^2 (1/r1^2 - 1/r2^2), r1=rCore, r2=rWall
        double inv1 = 1.0 / (rCore * rCore);
        double inv2 = 1.0 / (rWall * rWall);
        double pFreeShell = 0.5 * rho * gamma * gamma * Math.Max(inv1 - inv2, 0.0);

        double wallVsAxis = pForcedCore + pFreeShell;
        wallVsAxis = Math.Clamp(Math.Min(wallVsAxis, capPa), 0.0, capPa);

        double coreDrop = Math.Clamp(0.58 * wallVsAxis + 0.08 * rho * vtRef * vtRef, 0.0, capPa * 1.05);
        double deltaMag = Math.Clamp(
            Math.Sqrt(wallVsAxis * wallVsAxis + coreDrop * coreDrop),
            0.0,
            capPa * 1.1);

        string notes =
            "Mixed forced core + free outer; Omega=Gamma/r_core^2; Gamma≈Vt_ref*R_wall. Caps applied. Not CFD.";

        return new RadialVortexPressureResult
        {
            CoreRadiusM = rCore,
            ChamberRadiusM = rWall,
            WallPressureRisePa = wallVsAxis,
            CorePressureDropPa = coreDrop,
            EstimatedRadialPressureDeltaPa = deltaMag,
            VortexType = RadialVortexPressureModelType.MixedPiecewise,
            Notes = notes
        };
    }
}
