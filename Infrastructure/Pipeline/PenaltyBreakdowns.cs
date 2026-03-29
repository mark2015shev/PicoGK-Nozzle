using System;
using System.Collections.Generic;
using System.Linq;

namespace PicoGK_Run.Infrastructure.Pipeline;

/// <summary>Numeric physics-side penalties for optimization (mirrors warnings / governors / CV validity).</summary>
public sealed record PhysicsPenaltyBreakdown(
    double MassBalancePenalty,
    double MomentumBalancePenalty,
    double ContinuityResidualPenalty,
    double ChokingPenalty,
    double SeparationRiskPenalty,
    double ExcessiveSwirlPenalty,
    double GovernorClippingPenalty,
    double EntrainmentShortfallPenalty,
    double ThrustCvInvalidPenalty,
    double LowStaticPressurePenalty,
    double MachBandPenalty,
    double CapacityClassificationPenalty,
    double HealthMessagePenalty,
    double CaptureBoundaryDeficitPenalty = 0,
    double InletSpillTendencyPenalty = 0,
    double SwirlContainmentPenalty = 0)
{
    public double Sum =>
        MassBalancePenalty + MomentumBalancePenalty + ContinuityResidualPenalty + ChokingPenalty
        + SeparationRiskPenalty + ExcessiveSwirlPenalty + GovernorClippingPenalty
        + EntrainmentShortfallPenalty + ThrustCvInvalidPenalty + LowStaticPressurePenalty
        + MachBandPenalty + CapacityClassificationPenalty + HealthMessagePenalty
        + CaptureBoundaryDeficitPenalty + InletSpillTendencyPenalty + SwirlContainmentPenalty;

    public string TopSource
    {
        get
        {
            var pairs = new (string Name, double Value)[]
            {
                (nameof(MassBalancePenalty), MassBalancePenalty),
                (nameof(MomentumBalancePenalty), MomentumBalancePenalty),
                (nameof(ContinuityResidualPenalty), ContinuityResidualPenalty),
                (nameof(ChokingPenalty), ChokingPenalty),
                (nameof(SeparationRiskPenalty), SeparationRiskPenalty),
                (nameof(ExcessiveSwirlPenalty), ExcessiveSwirlPenalty),
                (nameof(GovernorClippingPenalty), GovernorClippingPenalty),
                (nameof(EntrainmentShortfallPenalty), EntrainmentShortfallPenalty),
                (nameof(ThrustCvInvalidPenalty), ThrustCvInvalidPenalty),
                (nameof(LowStaticPressurePenalty), LowStaticPressurePenalty),
                (nameof(MachBandPenalty), MachBandPenalty),
                (nameof(CapacityClassificationPenalty), CapacityClassificationPenalty),
                (nameof(HealthMessagePenalty), HealthMessagePenalty),
                (nameof(CaptureBoundaryDeficitPenalty), CaptureBoundaryDeficitPenalty),
                (nameof(InletSpillTendencyPenalty), InletSpillTendencyPenalty),
                (nameof(SwirlContainmentPenalty), SwirlContainmentPenalty)
            };
            return pairs.OrderByDescending(p => p.Value).First().Name;
        }
    }
}

/// <summary>Geometry continuity and CAD-self-consistency penalties.</summary>
public sealed record GeometryPenaltyBreakdown(
    double ContinuityIssuePenalty,
    int ContinuityIssueCount,
    double DownstreamDiameterMismatchPenalty,
    double ChamberUpstreamOvershootPenalty)
{
    public double Sum =>
        ContinuityIssuePenalty + DownstreamDiameterMismatchPenalty + ChamberUpstreamOvershootPenalty;

    public string TopSource
    {
        get
        {
            var triple = new (string Name, double Value)[]
            {
                ("GeometryContinuity", ContinuityIssuePenalty),
                (nameof(DownstreamDiameterMismatchPenalty), DownstreamDiameterMismatchPenalty),
                (nameof(ChamberUpstreamOvershootPenalty), ChamberUpstreamOvershootPenalty)
            };
            return triple.OrderByDescending(t => t.Value).First().Name;
        }
    }
}

/// <summary>Hard feasibility gates; when <see cref="Reject"/> is true the candidate should not win.</summary>
public sealed record ConstraintViolationBreakdown(
    bool Reject,
    IReadOnlyList<string> Reasons)
{
    public static ConstraintViolationBreakdown Ok { get; } = new(false, Array.Empty<string>());

    public static ConstraintViolationBreakdown FromReasons(params string[] reasons) =>
        new(reasons.Length > 0, reasons);
}
