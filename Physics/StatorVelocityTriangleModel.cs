using System;

namespace PicoGK_Run.Physics;

/// <summary>Inlet velocity triangle: β_flow = atan2(V_t, V_x), metal angle from vane setting [deg].</summary>
public readonly record struct StatorInletTriangle(
    double AxialVelocityMps,
    double TangentialVelocityMps,
    double MeridionalSpeedMps,
    double FlowAngleDeg,
    double MetalAngleDeg,
    double IncidenceDeg);

/// <summary>Ideal turning vs modeled outlet tangential estimate (reduced-order).</summary>
public readonly record struct StatorTurningResult(
    double IdealDeltaBetaDeg,
    double ModeledOutletTangentialVelocityMps,
    double TurningEfficiency01);

/// <summary>Stagnation-pressure loss split for reporting (same total as <see cref="StatorLossResult"/>).</summary>
public readonly record struct StatorLossBreakdown(
    double IncidenceLossPa,
    double TurningLossPa,
    double TotalStagnationPressureLossPa);

/// <summary>Velocity-triangle stator bookkeeping (not CFD blade row).</summary>
public static class StatorVelocityTriangleModel
{
    /// <summary>
    /// Convention: β measured from axial toward tangential in the chamber-fixed frame; metal angle is vane camber/metal reference [deg].
    /// Incidence = β_flow − β_metal.
    /// </summary>
    public static StatorInletTriangle BuildInlet(
        double axialVelocityMps,
        double tangentialVelocityMps,
        double statorVaneMetalAngleDeg)
    {
        double vx = Math.Max(Math.Abs(axialVelocityMps), 1e-9);
        double vt = tangentialVelocityMps;
        double vm = Math.Sqrt(vx * vx + vt * vt);
        double betaFlowRad = Math.Atan2(vt, Math.CopySign(vx, axialVelocityMps));
        double betaFlowDeg = betaFlowRad * (180.0 / Math.PI);
        double metal = statorVaneMetalAngleDeg;
        double inc = betaFlowDeg - metal;
        return new StatorInletTriangle(
            AxialVelocityMps: axialVelocityMps,
            TangentialVelocityMps: vt,
            MeridionalSpeedMps: vm,
            FlowAngleDeg: betaFlowDeg,
            MetalAngleDeg: metal,
            IncidenceDeg: inc);
    }

    /// <summary>Outlet V_t estimate from retained swirl fraction after ideal turning toward axial (first-order).</summary>
    public static StatorTurningResult ComputeTurning(
        in StatorInletTriangle inlet,
        double fractionOfTangentialRetained,
        double statorRecoveryEfficiency)
    {
        double f = Math.Clamp(fractionOfTangentialRetained, 0.0, 1.0);
        double eta = Math.Clamp(statorRecoveryEfficiency, 0.0, 0.95);
        double vtIn = inlet.TangentialVelocityMps;
        double idealDeltaBeta = inlet.FlowAngleDeg * (1.0 - f);
        double vtOut = vtIn * f;
        double turnEff = Math.Abs(vtIn) > 1e-9 ? 1.0 - Math.Abs(vtOut / vtIn) : 0.0;
        turnEff = Math.Clamp(turnEff * eta, 0.0, 1.0);
        return new StatorTurningResult(idealDeltaBeta, vtOut, turnEff);
    }
}
