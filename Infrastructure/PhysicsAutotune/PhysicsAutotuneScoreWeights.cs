namespace PicoGK_Run.Infrastructure.PhysicsAutotune;

/// <summary>Explicit multiplicative weights for the five-parameter physics autotune score (readable, tunable).</summary>
public sealed class PhysicsAutotuneScoreWeights
{
    /// <summary>Exponent on normalized thrust term (F_net / F_baseline).</summary>
    public double ThrustExponent { get; init; } = 0.85;

    /// <summary>Exponent on exit axial velocity vs baseline.</summary>
    public double AxialTransportExponent { get; init; } = 0.45;

    /// <summary>Weight on stator pressure recovery vs baseline dynamic head scale.</summary>
    public double StatorRecoveryWeight { get; init; } = 0.55;

    /// <summary>Weight on useful entrainment: ER normalized, gated by chamber axial Mach.</summary>
    public double UsefulEntrainmentWeight { get; init; } = 0.40;

    public double ChokingPenaltyWeight { get; init; } = 0.35;
    public double SeparationPenaltyWeight { get; init; } = 0.28;
    public double InvalidStatePenaltyWeight { get; init; } = 2.5;
    public double TotalPressureLossPenaltyWeight { get; init; } = 0.22;
    public double ResidualSwirlPenaltyWeight { get; init; } = 0.18;
    public double LowChamberAxialPenaltyWeight { get; init; } = 0.20;
    public double EntrainmentShortfallPenaltyWeight { get; init; } = 0.15;
    public double HealthIssuePenaltyEach { get; init; } = 0.04;

    /// <summary>Scale for <see cref="PhysicsAutotuneScoring"/> swirl-entrance Mach capacity (warning / restrictive / choking).</summary>
    public double SwirlEntranceCapacityPenaltyWeight { get; init; } = 0.48;
}
