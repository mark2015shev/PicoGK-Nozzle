namespace PicoGK_Run.Physics.Solvers;

/// <summary>
/// Explicit staged SI ledger for the swirl-vortex entrainment path (engineering audit — not CFD).
/// </summary>
public sealed class NozzlePhysicsStageResult
{
    public InjectorDischargeResult? Stage1Injector { get; init; }

    public double Stage2SwirlNumberAtInjector { get; init; }

    public double Stage3CorePressureDropPa { get; init; }
    public double Stage3WallPressureRisePa { get; init; }
    public double Stage3EstimatedCoreStaticPressurePa { get; init; }

    public double Stage4AmbientInflowPotentialKgS { get; init; }
    public double Stage4AmbientInflowActualIntegratedKgS { get; init; }

    public double Stage5MixedMassFlowKgS { get; init; }
    public double Stage5MixedAxialVelocityMps { get; init; }
    public double Stage5MixedTangentialVelocityMps { get; init; }

    public double Stage6DiffuserPressureRiseEffectivePa { get; init; }
    public double Stage6DiffuserRecoveryMultiplier { get; init; }

    public double Stage7StatorRecoveredPressureRisePa { get; init; }
    public double Stage7StatorEtaEffective { get; init; }
    public double Stage7AxialVelocityAfterMps { get; init; }

    public double FinalExitAxialVelocityMps { get; init; }
    public double FinalTotalMassFlowKgS { get; init; }
}
