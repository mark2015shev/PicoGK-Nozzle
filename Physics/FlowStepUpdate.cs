namespace PicoGK_Run.Physics;

/// <summary>Per-step entrainment bookkeeping (SI march).</summary>
public readonly struct FlowStepUpdate
{
    public FlowStepUpdate(
        double deltaSecondaryMassFlowKgS,
        double requestedEntrainmentDeltaKgS,
        double ambientChokedMassFlowLimitKgS)
    {
        DeltaSecondaryMassFlowKgS = deltaSecondaryMassFlowKgS;
        RequestedEntrainmentDeltaKgS = requestedEntrainmentDeltaKgS;
        AmbientChokedMassFlowLimitKgS = ambientChokedMassFlowLimitKgS;
    }

    /// <summary>Δṁ_secondary this step [kg/s] (increment to entrained stream).</summary>
    public double DeltaSecondaryMassFlowKgS { get; }

    /// <summary>Correlation demand before compressible intake cap [kg/s].</summary>
    public double RequestedEntrainmentDeltaKgS { get; }

    /// <summary>Choked mass-flow ceiling through capture at (P₀,T₀)_ambient [kg/s] (reference).</summary>
    public double AmbientChokedMassFlowLimitKgS { get; }
}
