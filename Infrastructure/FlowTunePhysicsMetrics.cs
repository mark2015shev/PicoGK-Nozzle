using System;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Infrastructure;

/// <summary>Scalar physics metrics for autotune (same SI path as full run).</summary>
public sealed class FlowTunePhysicsMetrics
{
    public double VortexQualityComposite { get; init; }
    public double RadialPressureUsefulNorm { get; init; }
    public double RecoverableSwirlAtStatorNorm { get; init; }
    public double BreakdownRisk01 { get; init; }
    public double SeparationRisk01 { get; init; }
    public double TotalLoss01 { get; init; }
    public double EjectorStress01 { get; init; }
    public double LowAxialMomentum01 { get; init; }

    public static FlowTunePhysicsMetrics FromChamber(ChamberFirstOrderPhysics? ch, double mixedAxialMps)
    {
        if (ch == null)
        {
            return new FlowTunePhysicsMetrics();
        }

        double machAx = Math.Clamp(Math.Abs(mixedAxialMps) / 340.0, 0.0, 0.95);
        double lowAx = Math.Clamp((0.22 - machAx) / 0.22, 0.0, 1.0);

        return new FlowTunePhysicsMetrics
        {
            VortexQualityComposite = ch.TuningCompositeQuality,
            RadialPressureUsefulNorm = ch.RadialPressureUsefulNorm,
            RecoverableSwirlAtStatorNorm = ch.RecoverableSwirlFraction01,
            BreakdownRisk01 = ch.VortexStructure.BreakdownRiskScore,
            SeparationRisk01 = ch.DiffuserRecovery.SeparationRiskScore,
            TotalLoss01 = ch.NormalizedTotalPressureLoss01,
            EjectorStress01 = ch.EjectorRegime.RegimeScore,
            LowAxialMomentum01 = lowAx
        };
    }
}
