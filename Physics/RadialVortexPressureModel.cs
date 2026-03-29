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

/// <summary>Outputs from the reduced-order radial pressure-balance model dp/dr ≈ ρ V_θ²/r (axisymmetric, small V_r).</summary>
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

    /// <summary>P_core ≤ P_bulk and wall/core bounded vs P₀ (secondary shaping only).</summary>
    public bool ShapingInvariantsSatisfied { get; init; } = true;

    public string ShapingInvariantNote { get; init; } = "";
}

/// <summary>
/// <b>Secondary field model only:</b> reduced-order radial pressure balance — Δp_core, Δp_wall vs bulk chamber static from ρ, V_θ, and geometry.
/// Bulk <c>P_static</c> must come from the chamber march (<see cref="CompressibleFlowMath.BulkChamberThermoFromStagnationAndSpeedMagnitude"/>);
/// this class never overrides bulk pressure.
/// </summary>
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

    /// <summary>
    /// Same vortex integral as <see cref="Compute"/> but returns <b>deltas only</b> clamped so radial shaping cannot
    /// pull wall static above a P₀ ceiling or core static below ambient: P_core = P_bulk − Δ_core, P_wall = P_bulk + Δ_wall.
    /// Does not replace bulk static — use bulk from isentropic chamber march.
    /// </summary>
    public static RadialVortexPressureResult ComputeShapingRelativeToBulk(
        double bulkStaticPressurePa,
        double totalPressureCeilingPa,
        double densityKgM3,
        double representativeTangentialVelocityMps,
        double chamberRadiusM,
        double coreRadiusFraction,
        double capPa,
        double ambientPressureFloorPa)
    {
        double bulk = Math.Max(bulkStaticPressurePa, SiPressureGuards.MinStaticPressurePa);
        double p0Ceil = Math.Max(totalPressureCeilingPa, bulk);
        RadialVortexPressureResult raw = Compute(
            densityKgM3,
            representativeTangentialVelocityMps,
            chamberRadiusM,
            coreRadiusFraction,
            capPa);

        double maxWallDelta =
            Math.Max(0.0, p0Ceil * (1.0 + ChamberAerodynamicsConfiguration.WallStaticExcessOverBulkMaxFractionOfP0) - bulk);
        double wallDelta = Math.Min(Math.Max(0.0, raw.WallPressureRisePa), maxWallDelta);

        double pFloor = Math.Max(ambientPressureFloorPa, SiPressureGuards.MinStaticPressurePa);
        double maxCoreDrop = Math.Max(0.0, bulk - pFloor);
        double coreDelta = Math.Min(Math.Max(0.0, raw.CorePressureDropPa), maxCoreDrop * 0.99);

        double deltaMag = Math.Min(
            raw.EstimatedRadialPressureDeltaPa,
            wallDelta + coreDelta);

        double vtAbs = Math.Abs(representativeTangentialVelocityMps);
        double qTheta = 0.5 * Math.Max(densityKgM3, 1e-9) * vtAbs * vtAbs;
        if (qTheta >= ChamberAerodynamicsConfiguration.RadialSwirlQThetaFloorPa)
        {
            double minCore = Math.Min(
                maxCoreDrop * 0.98,
                ChamberAerodynamicsConfiguration.RadialMinimumCoreDropFractionOfSwirlQ * qTheta);
            if (coreDelta < minCore)
                coreDelta = minCore;
            double minWall = Math.Min(
                maxWallDelta,
                ChamberAerodynamicsConfiguration.RadialMinimumWallRiseFractionOfSwirlQ * qTheta);
            if (wallDelta < minWall)
                wallDelta = minWall;
            coreDelta = Math.Min(coreDelta, maxCoreDrop * 0.99);
            wallDelta = Math.Min(wallDelta, maxWallDelta);
        }

        deltaMag = Math.Min(
            raw.EstimatedRadialPressureDeltaPa,
            wallDelta + coreDelta);

        double pCoreIf = bulk - coreDelta;
        double pWallIf = bulk + wallDelta;
        double pWallCeil = p0Ceil * (1.0 + 2.0 * ChamberAerodynamicsConfiguration.WallStaticExcessOverBulkMaxFractionOfP0);
        bool ok = double.IsFinite(pCoreIf) && double.IsFinite(pWallIf)
                  && pCoreIf <= bulk + 1.0
                  && pWallIf <= pWallCeil + 1.0
                  && coreDelta >= -1e-6
                  && wallDelta >= -1e-6;
        string invNote = ok
            ? ""
            : "RADIAL SHAPING INVARIANT: core/wall vs bulk or P₀ ceiling failed — check bulk state and caps.";

        return new RadialVortexPressureResult
        {
            CoreRadiusM = raw.CoreRadiusM,
            ChamberRadiusM = raw.ChamberRadiusM,
            WallPressureRisePa = wallDelta,
            CorePressureDropPa = coreDelta,
            EstimatedRadialPressureDeltaPa = deltaMag,
            VortexType = raw.VortexType,
            Notes = raw.Notes + " Shaping deltas clamped to bulk static and P₀ ceiling (secondary field model).",
            ShapingInvariantsSatisfied = ok,
            ShapingInvariantNote = invNote
        };
    }
}
