using System;

namespace PicoGK_Run.Physics.Solvers;

/// <summary>
/// Stage 2 — swirl chamber bulk metrics (separate from autotune scoring): tangential momentum flux, injector S, chamber L/D.
/// </summary>
public static class SwirlChamberSolver
{
    public static double TangentialMomentumFluxKgMps(double coreMassFlowKgS, double tangentialVelocityMps) =>
        Math.Max(coreMassFlowKgS, 0.0) * tangentialVelocityMps;

    /// <summary>Chamber swirl proxy using injector plane components (design intent, not CFD).</summary>
    public static double InjectorSwirlNumber(double vtMps, double vaMps) =>
        SwirlMath.InjectorSwirlNumber(vtMps, vaMps);

    public static double ChamberSlendernessLD(double lengthMm, double diameterMm) =>
        lengthMm / Math.Max(diameterMm, 1e-6);
}
