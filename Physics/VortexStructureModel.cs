using System;

namespace PicoGK_Run.Physics;

/// <summary>Stability / usefulness classification for controlled-vortex nozzle (heuristic).</summary>
public enum VortexStabilityClassification
{
    StableLow,
    StableUseful,
    StrongSwirl,
    BreakdownRisk,
    LikelyUnstable
}

/// <summary>Extended vortex diagnostics beyond legacy <see cref="VortexStructureClass"/>.</summary>
public sealed class VortexStructureDiagnosticsResult
{
    public double InjectorSwirlNumberSimple { get; init; }
    /// <summary>S_flux ≈ K * |Vt|/|Va| with explicit K (uniform profile assumption).</summary>
    public double SwirlNumberFluxStyle { get; init; }
    public double FluxGeometryFactorKUsed { get; init; }
    public double BreakdownRiskScore { get; init; }
    public VortexStabilityClassification Classification { get; init; }
    public string ClassificationLabel { get; init; } = "";
    /// <summary>0–1 tuning quality: entrainment + recoverable swirl + radial structure − risks.</summary>
    public double CompositeVortexQuality { get; init; }
    public string Notes { get; init; } = "";
}

/// <summary>Heuristic vortex structure / stability — not CFD.</summary>
public static class VortexStructureModel
{
    public static VortexStructureDiagnosticsResult Compute(
        double vtInjector,
        double vaInjector,
        double chamberLd,
        double injectorAxialPositionRatio,
        double expanderHalfAngleDeg,
        double corePressureDropPa,
        double radialDeltaPa,
        double remainingSwirlFractionAtStator,
        double entrainmentRatio,
        double mixedAxialVelocityPreStatorMps,
        double swirlDecayFractionAlongChamber)
    {
        double va = Math.Max(Math.Abs(vaInjector), 1e-6);
        double vt = Math.Abs(vtInjector);
        double sSimple = vt / va;
        double k = ChamberPhysicsCoefficients.FluxSwirlGeometryFactorK;
        double sFlux = k * sSimple;

        double machAx = Math.Clamp(Math.Abs(mixedAxialVelocityPreStatorMps) / 340.0, 0.0, 0.95);
        double depN = Math.Clamp(corePressureDropPa / 25_000.0, 0.0, 2.5);
        double radN = Math.Clamp(radialDeltaPa / 35_000.0, 0.0, 2.2);

        double breakdown = Math.Clamp(
            0.32 * Math.Tanh((sSimple - 4.0) / 2.2)
            + 0.22 * Math.Tanh((4.0 - chamberLd) / 2.0)
            + 0.18 * Math.Tanh(depN - 0.55)
            + 0.14 * Math.Tanh((remainingSwirlFractionAtStator - 0.82) / 0.2)
            + 0.14 * Math.Tanh((expanderHalfAngleDeg - 10.5) / 6.0),
            0.0,
            1.0);

        VortexStabilityClassification cls = Classify(sSimple, breakdown, chamberLd, entrainmentRatio, machAx);

        double w1 = ChamberPhysicsCoefficients.StructureQualityWCoreDrop;
        double w2 = ChamberPhysicsCoefficients.StructureQualityWEntrainment;
        double w3 = ChamberPhysicsCoefficients.StructureQualityWRecoverableSwirl;
        double w4 = ChamberPhysicsCoefficients.StructureQualityWBreakdown;
        double w5 = ChamberPhysicsCoefficients.StructureQualityWExcessDecay;
        double w6 = ChamberPhysicsCoefficients.StructureQualityWLowAxial;

        double nCore = Math.Clamp(depN / 1.1, 0.0, 1.2);
        double nEr = Math.Clamp(entrainmentRatio / 1.35, 0.0, 1.3);
        double nRem = Math.Clamp(remainingSwirlFractionAtStator, 0.0, 1.0);
        double nRad = Math.Clamp(radN / 1.05, 0.0, 1.2);
        double excessDecay = Math.Clamp(swirlDecayFractionAlongChamber, 0.0, 1.0);

        double q = w1 * nCore * 0.55
                   + w2 * nEr
                   + w3 * nRem * 0.9
                   + 0.12 * nRad
                   - w4 * breakdown
                   - w5 * excessDecay
                   - w6 * Math.Clamp((0.28 - machAx) / 0.28, 0.0, 1.0);

        if (cls == VortexStabilityClassification.LikelyUnstable || cls == VortexStabilityClassification.BreakdownRisk)
            q -= 0.18;

        q = Math.Clamp(q, 0.0, 1.0);

        return new VortexStructureDiagnosticsResult
        {
            InjectorSwirlNumberSimple = sSimple,
            SwirlNumberFluxStyle = sFlux,
            FluxGeometryFactorKUsed = k,
            BreakdownRiskScore = breakdown,
            Classification = cls,
            ClassificationLabel = Label(cls),
            CompositeVortexQuality = q,
            Notes = "S_flux uses uniform u_x, u_theta assumption; breakdown risk is heuristic only."
        };
    }

    private static VortexStabilityClassification Classify(
        double sSimple,
        double breakdownRisk,
        double ld,
        double er,
        double machAxial)
    {
        if (breakdownRisk > 0.72 || (sSimple > 6.2 && ld < 2.0))
            return VortexStabilityClassification.LikelyUnstable;
        if (breakdownRisk > 0.48 || sSimple > 5.4)
            return VortexStabilityClassification.BreakdownRisk;
        if (sSimple >= 3.0 && er > 0.35 && machAxial > 0.12)
            return VortexStabilityClassification.StrongSwirl;
        if (sSimple >= 1.35 && breakdownRisk < 0.38)
            return VortexStabilityClassification.StableUseful;
        return VortexStabilityClassification.StableLow;
    }

    private static string Label(VortexStabilityClassification c) => c switch
    {
        VortexStabilityClassification.StableLow => "stable low swirl (heuristic)",
        VortexStabilityClassification.StableUseful => "stable useful vortex (heuristic)",
        VortexStabilityClassification.StrongSwirl => "strong controlled swirl (heuristic)",
        VortexStabilityClassification.BreakdownRisk => "vortex breakdown risk (heuristic)",
        VortexStabilityClassification.LikelyUnstable => "likely unstable / heavy recirculation risk (heuristic)",
        _ => "unknown"
    };
}
