namespace PicoGK_Run.Physics;

/// <summary>First-order audit of hub-based stator swirl bookkeeping — not CFD.</summary>
public sealed class HubStatorFlowDiagnostics
{
    public double StatorHubDiameterMm { get; init; }
    public double StatorOuterInnerRadiusMm { get; init; }
    public double SpanRatio { get; init; }
    public double BlockageAreaRatio { get; init; }
    public double HubGeometryRecoveryFactor { get; init; }
    public double AlignmentFactor { get; init; }
    public double SpanEfficiencyFactor { get; init; }
    public double BlockagePenalty01 { get; init; }
    public double EffectiveStatorEtaUsed { get; init; }

    public double SwirlTangentialVelocityBeforeMps { get; init; }
    public double SwirlTangentialVelocityAfterMps { get; init; }
    public double FractionSwirlRemovedByStatorRow { get; init; }
    public double FractionSwirlCoreBypassFirstOrder { get; init; }
    public double FractionSwirlDissipatedFirstOrder { get; init; }
    public double FractionSwirlToAxialMomentumFirstOrder { get; init; }
}
