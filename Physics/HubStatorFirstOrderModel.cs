using System;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Physics;

/// <summary>
/// Hub-to-casing stator: span ratio, blockage, and a first-order multiplier on stator η — not CFD.
/// </summary>
public sealed class HubStatorRecoveryContext
{
    public double StatorOuterRadiusMm { get; init; }
    public double StatorHubRadiusMm { get; init; }
    public double SpanRatio { get; init; }
    public double BlockageAreaRatio { get; init; }
    public double AlignmentFactor { get; init; }
    public double SpanEfficiencyFactor { get; init; }
    public double BlockagePenalty01 { get; init; }
    /// <summary>f_align · SpanRatio · (1 − blockage_penalty).</summary>
    public double HubGeometryRecoveryFactor { get; init; }
}

public static class HubStatorFirstOrderModel
{
    /// <summary>
    /// Inner casing radius at stator (gas path) [mm] = expander exit inner radius.
    /// Hub diameter defaults to ~0.28× chamber OD when design hub ≤ 0.5 mm.
    /// </summary>
    public static HubStatorRecoveryContext ComputeContext(
        NozzleDesignInputs d,
        double statorOuterInnerRadiusMm,
        double statorVaneAngleDeg,
        double impliedSwirlAngleDeg)
    {
        double rOuter = Math.Max(statorOuterInnerRadiusMm, 1.0);
        double hubD = d.StatorHubDiameterMm > 0.5
            ? d.StatorHubDiameterMm
            : 0.28 * Math.Max(d.SwirlChamberDiameterMm, 1.0);
        double rHub = 0.5 * hubD;
        double maxHubR = rOuter * 0.82 - 0.8;
        rHub = Math.Clamp(rHub, 3.0, Math.Max(maxHubR, 4.0));

        double spanRatio = (rOuter - rHub) / Math.Max(rOuter, 1e-6);
        spanRatio = Math.Clamp(spanRatio, 0.0, 1.0);
        double sigma = rHub / Math.Max(rOuter, 1e-6);
        double blockage = sigma * sigma;

        double mismatch = Math.Abs(statorVaneAngleDeg - impliedSwirlAngleDeg);
        double fAlignReport = 1.0 - 0.55 * Math.Tanh(mismatch / Math.Max(ChamberPhysicsCoefficients.StatorIncidenceRefDeg, 1.0));
        fAlignReport = Math.Clamp(fAlignReport, 0.32, 1.0);

        double blockPen = Math.Clamp(
            ChamberPhysicsCoefficients.HubStatorBlockagePenaltyScale * blockage,
            0.0,
            ChamberPhysicsCoefficients.HubStatorBlockagePenaltyCap);

        // Applied multiplier: span × (1 − blockage penalty). Incidence/turning stay in StatorLoss coupling (η_factor).
        double hubGeom = spanRatio * (1.0 - blockPen);
        hubGeom = Math.Clamp(hubGeom, 0.20, 1.12);

        return new HubStatorRecoveryContext
        {
            StatorOuterRadiusMm = rOuter,
            StatorHubRadiusMm = rHub,
            SpanRatio = spanRatio,
            BlockageAreaRatio = blockage,
            AlignmentFactor = fAlignReport,
            SpanEfficiencyFactor = spanRatio,
            BlockagePenalty01 = blockPen,
            HubGeometryRecoveryFactor = hubGeom
        };
    }

    /// <summary>Builds flow audit after stator row (energy fractions are heuristic splits).</summary>
    public static HubStatorFlowDiagnostics BuildDiagnostics(
        in HubStatorRecoveryContext ctx,
        NozzleDesignInputs d,
        double etaStatorEffUsed,
        double vtBeforeMps,
        double vtAfterMps,
        double vaBeforeMps,
        double axialGainMps)
    {
        double vtIn = vtBeforeMps;
        double vtOut = vtAfterMps;
        double eRotIn = 0.5 * vtIn * vtIn;
        double frRemoved = 1.0 - Math.Abs(vtOut) / Math.Max(Math.Abs(vtIn), 1e-6);
        frRemoved = Math.Clamp(frRemoved, 0.0, 1.0);

        double va2 = vaBeforeMps + axialGainMps;
        double eAxGain = 0.5 * Math.Max(va2 * va2 - vaBeforeMps * vaBeforeMps, 0.0);
        double frAx = Math.Clamp(eAxGain / Math.Max(eRotIn, 1e-6), 0.0, 1.2);

        double frBypass = Math.Clamp((1.0 - ctx.SpanRatio) * 0.58 * (1.0 + 0.35 * ctx.BlockageAreaRatio), 0.0, 0.92);
        double frDiss = Math.Clamp(1.0 - 0.85 * frRemoved - 0.45 * frAx - 0.25 * (1.0 - frBypass), 0.0, 1.0);

        return new HubStatorFlowDiagnostics
        {
            StatorHubDiameterMm = 2.0 * ctx.StatorHubRadiusMm,
            StatorOuterInnerRadiusMm = ctx.StatorOuterRadiusMm,
            SpanRatio = ctx.SpanRatio,
            BlockageAreaRatio = ctx.BlockageAreaRatio,
            HubGeometryRecoveryFactor = ctx.HubGeometryRecoveryFactor,
            AlignmentFactor = ctx.AlignmentFactor,
            SpanEfficiencyFactor = ctx.SpanEfficiencyFactor,
            BlockagePenalty01 = ctx.BlockagePenalty01,
            EffectiveStatorEtaUsed = etaStatorEffUsed,
            SwirlTangentialVelocityBeforeMps = vtBeforeMps,
            SwirlTangentialVelocityAfterMps = vtAfterMps,
            FractionSwirlRemovedByStatorRow = frRemoved,
            FractionSwirlCoreBypassFirstOrder = frBypass,
            FractionSwirlDissipatedFirstOrder = frDiss,
            FractionSwirlToAxialMomentumFirstOrder = frAx
        };
    }
}
