namespace PicoGK_Run.Parameters;

/// <summary>
/// Symmetric random ranges for one autotune stage: multipliers use <c>u(1−spread, 1+spread)</c> on the reference design;
/// angles use <c>ref + u(−halfSpan, +halfSpan)</c> [deg]. First-order search knobs only — not CFD.
/// </summary>
public readonly struct AutotunePerturbationBand
{
    public double ChamberDiameterSpread { get; init; }
    public double ChamberLengthSpread { get; init; }
    public double InletSpread { get; init; }
    public double ExitSpread { get; init; }
    public double ExpanderAngleSpread { get; init; }
    public double ExpanderLengthSpread { get; init; }
    public double StatorAngleSpread { get; init; }
    public double InjectorAxialSpread { get; init; }
    public double InjectorYawDegHalfSpan { get; init; }
    public double InjectorPitchDegHalfSpan { get; init; }
    /// <summary>Half-width on synthesis target ER when <see cref="RunConfiguration.AutotuneUseSynthesisBaseline"/> (Stage 1 only typically).</summary>
    public double SynthesisTargetErHalfSpan { get; init; }

    public static AutotunePerturbationBand DefaultStage1Broad => new()
    {
        ChamberDiameterSpread = 0.20,
        ChamberLengthSpread = 0.16,
        InletSpread = 0.18,
        ExitSpread = 0.20,
        ExpanderAngleSpread = 0.24,
        ExpanderLengthSpread = 0.30,
        StatorAngleSpread = 0.20,
        InjectorAxialSpread = 0.24,
        InjectorYawDegHalfSpan = 14.0,
        InjectorPitchDegHalfSpan = 5.5,
        SynthesisTargetErHalfSpan = 0.20
    };

    public static AutotunePerturbationBand DefaultStage2Focused => new()
    {
        ChamberDiameterSpread = 0.10,
        ChamberLengthSpread = 0.085,
        InletSpread = 0.09,
        ExitSpread = 0.10,
        ExpanderAngleSpread = 0.12,
        ExpanderLengthSpread = 0.15,
        StatorAngleSpread = 0.10,
        InjectorAxialSpread = 0.12,
        InjectorYawDegHalfSpan = 7.0,
        InjectorPitchDegHalfSpan = 3.0,
        SynthesisTargetErHalfSpan = 0.10
    };

    public static AutotunePerturbationBand DefaultStage3Polish => new()
    {
        ChamberDiameterSpread = 0.042,
        ChamberLengthSpread = 0.036,
        InletSpread = 0.038,
        ExitSpread = 0.042,
        ExpanderAngleSpread = 0.05,
        ExpanderLengthSpread = 0.062,
        StatorAngleSpread = 0.042,
        InjectorAxialSpread = 0.05,
        InjectorYawDegHalfSpan = 3.0,
        InjectorPitchDegHalfSpan = 1.4,
        SynthesisTargetErHalfSpan = 0.04
    };
}
