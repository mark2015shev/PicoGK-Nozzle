namespace PicoGK_Run.Physics;

/// <summary>Per-step entrainment bookkeeping (SI march).</summary>
public readonly struct FlowStepUpdate
{
    public FlowStepUpdate(
        double deltaSecondaryMassFlowKgS,
        double requestedEntrainmentDeltaKgS,
        double ambientChokedMassFlowLimitKgS,
        double ambientToMixedStaticPressureLimitedKgS,
        double swirlPassageMdotCeilingKgS = double.NaN,
        bool entrainmentCappedBySwirlPassageMach = false)
    {
        DeltaSecondaryMassFlowKgS = deltaSecondaryMassFlowKgS;
        RequestedEntrainmentDeltaKgS = requestedEntrainmentDeltaKgS;
        AmbientChokedMassFlowLimitKgS = ambientChokedMassFlowLimitKgS;
        AmbientToMixedStaticPressureLimitedKgS = ambientToMixedStaticPressureLimitedKgS;
        SwirlPassageMdotCeilingKgS = swirlPassageMdotCeilingKgS;
        EntrainmentCappedBySwirlPassageMach = entrainmentCappedBySwirlPassageMach;
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

    /// <summary>Subsonic bulk Mach ceiling from <see cref="SwirlEntranceCapacityLimits"/>; NaN if capacity cap disabled this step.</summary>
    public double SwirlPassageMdotCeilingKgS { get; }

    /// <summary>True when requested entrainment was reduced to respect swirl-passage Mach limit.</summary>
    public bool EntrainmentCappedBySwirlPassageMach { get; }
}
