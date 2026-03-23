namespace PicoGK_Run.Infrastructure;

/// <summary>
/// One evaluation in a parameter sweep — first-order SI diagnostics only (not CFD validation).
/// </summary>
public sealed record ValidationSweepCaseResult
{
    public string ParameterName { get; init; } = "";
    public double ParameterValue { get; init; }

    public double NetThrustN { get; init; }
    public double SourceOnlyThrustN { get; init; }
    public double ThrustGainRatio { get; init; }
    public double EntrainmentRatio { get; init; }
    public double MixedMassFlowKgS { get; init; }
    public double ExitVelocityMps { get; init; }

    public double InjectorSwirlNumber { get; init; }
    public double FluxStyleSwirlMetric { get; init; }
    public double ChamberSlendernessLD { get; init; }
    public double ChamberSwirlForStator { get; init; }
    public double ResidualExitSwirlMps { get; init; }

    public double CorePressureDropPa { get; init; }
    public double WallPressureRisePa { get; init; }
    public double RadialPressureDeltaPa { get; init; }
    public string VortexClassification { get; init; } = "";
    public double VortexQuality { get; init; }
    public double BreakdownRisk { get; init; }

    public double EffectiveInjectorVelocityMps { get; init; }
    public double EffectiveStatorEfficiency { get; init; }
    public double EffectiveDiffuserRecovery { get; init; }
    public double SeparationRisk { get; init; }
    public double TotalLossMetric01 { get; init; }
    public string EjectorRegime { get; init; } = "";

    public int HealthCount { get; init; }
    public bool HasDesignError { get; init; }
    public string KeyWarningsSummary { get; init; } = "";

    /// <summary>NaN/Inf or physically nonsense flags for this row.</summary>
    public bool ImpossibleOrInvalidState { get; init; }

    /// <summary>Adjacent-point relative jump exceeded threshold on at least one tracked metric.</summary>
    public bool RapidChangeFromPrevious { get; init; }

    /// <summary>Machine notes (sanity + discontinuity); not engineering claims.</summary>
    public string Notes { get; init; } = "";
}
