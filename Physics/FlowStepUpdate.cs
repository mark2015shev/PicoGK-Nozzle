namespace PicoGK_Run.Physics;

/// <summary>Per-step entrainment bookkeeping (SI march).</summary>
public readonly struct FlowStepUpdate
{
    public FlowStepUpdate(
        double deltaSecondaryMassFlowKgS,
        double requestedEntrainmentDeltaKgS,
        double ambientChokedMassFlowLimitKgS,
        double ambientToMixedStaticPressureLimitedKgS)
    {
        DeltaSecondaryMassFlowKgS = deltaSecondaryMassFlowKgS;
        RequestedEntrainmentDeltaKgS = requestedEntrainmentDeltaKgS;
        AmbientChokedMassFlowLimitKgS = ambientChokedMassFlowLimitKgS;
        AmbientToMixedStaticPressureLimitedKgS = ambientToMixedStaticPressureLimitedKgS;
    }

    /// <summary>Δṁ_secondary this step [kg/s] (increment to entrained stream).</summary>
    public double DeltaSecondaryMassFlowKgS { get; }

    /// <summary>Correlation demand before caps [kg/s].</summary>
    public double RequestedEntrainmentDeltaKgS { get; }

    /// <summary>Isentropic choked ceiling ṁ*(A_capture) at (P₀,T₀)_ambient [kg/s].</summary>
    public double AmbientChokedMassFlowLimitKgS { get; }

    /// <summary>
    /// Isentropic limit from (P₀,T₀)_ambient through A_capture to mixed static P [kg/s] (first-order back-pressure).
    /// Actual = min(demand, this, choked); geometry is encoded in A_capture.
    /// </summary>
    public double AmbientToMixedStaticPressureLimitedKgS { get; }
}
