namespace PicoGK_Run.Physics.Solvers;

/// <summary>Stage 4 — swirl-aware ambient potential vs march-integrated actual (compressible path).</summary>
public static class AmbientInflowSolver
{
    public static double PotentialMassFlowKgS(
        double ambientPressurePa,
        double coreStaticPressurePa,
        double captureAreaM2,
        double ambientDensityKgM3,
        double injectorPlaneFluxSwirlNumberAbs,
        double gain = 0.22) =>
        SwirlAmbientEntrainmentSolver.ComputeSwirlDrivenAmbientPotentialKgS(
            ambientPressurePa,
            coreStaticPressurePa,
            captureAreaM2,
            ambientDensityKgM3,
            injectorPlaneFluxSwirlNumberAbs,
            gain);
}
