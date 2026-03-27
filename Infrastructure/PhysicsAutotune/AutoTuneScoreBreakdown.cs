namespace PicoGK_Run.Infrastructure.PhysicsAutotune;

/// <summary>Term-by-term physics autotune score (all dimensionless unless noted).</summary>
public sealed class AutoTuneScoreBreakdown
{
    public double ThrustTerm { get; init; }
    public double AxialTransportTerm { get; init; }
    public double StatorRecoveryTerm { get; init; }
    public double UsefulEntrainmentTerm { get; init; }

    public double ChokingPenalty { get; init; }
    public double SeparationPenalty { get; init; }
    public double InvalidStatePenalty { get; init; }
    public double TotalPressureLossPenalty { get; init; }
    public double ResidualSwirlPenalty { get; init; }
    public double LowChamberAxialPenalty { get; init; }
    public double EntrainmentShortfallPenalty { get; init; }
    public double HealthPenalty { get; init; }

    public double PositiveProduct { get; init; }
    public double PenaltiesSum { get; init; }
    public double FinalScore { get; init; }
}
