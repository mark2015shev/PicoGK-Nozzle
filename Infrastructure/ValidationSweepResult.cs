using System.Collections.Generic;

namespace PicoGK_Run.Infrastructure;

/// <summary>
/// Aggregated sweep outcome — reduced-order consistency check, not proof of correctness or CFD agreement.
/// </summary>
public sealed record ValidationSweepResult
{
    public string SweepName { get; init; } = "";
    public string ParameterDisplayName { get; init; } = "";
    public double BaselineParameterValue { get; init; }

    public IReadOnlyList<ValidationSweepCaseResult> Cases { get; init; } = System.Array.Empty<ValidationSweepCaseResult>();

    public int BestNetThrustIndex { get; init; } = -1;
    public int BestEntrainmentRatioIndex { get; init; } = -1;
    public int BestVortexQualityIndex { get; init; } = -1;
    public int FirstHighBreakdownRiskIndex { get; init; } = -1;
    public int FirstHighSeparationRiskIndex { get; init; } = -1;
    public int BestThrustAmongLowRiskIndex { get; init; } = -1;

    public bool AnyImpossibleState { get; init; }
    public bool AnyRapidChange { get; init; }

    /// <summary>Short lines — explicitly first-order / pre-CFD.</summary>
    public IReadOnlyList<string> InterpretationLines { get; init; } = System.Array.Empty<string>();

    public string CsvPath { get; init; } = "";
}
