using System;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Infrastructure;

/// <summary>Scalar physics metrics for autotune (same SI path as full run).</summary>
public sealed class FlowTunePhysicsMetrics
{
    /// <summary>Same as <see cref="ChamberFirstOrderPhysics.SwirlChamberAutotuneScore01"/> (preferred name).</summary>
    public double SwirlChamberAutotuneScore01 { get; init; }

    /// <summary>Legacy alias for <see cref="SwirlChamberAutotuneScore01"/>.</summary>
    public double VortexQualityComposite { get; init; }
    public double RadialPressureUsefulNorm { get; init; }
    public double RecoverableSwirlAtStatorNorm { get; init; }
    public double BreakdownRisk01 { get; init; }
    public double SeparationRisk01 { get; init; }
    public double TotalLoss01 { get; init; }
    public double EjectorStress01 { get; init; }
    public double LowAxialMomentum01 { get; init; }

    public double CapturePressureDeficitWeakness01 { get; init; }
    public double BidirectionalSpillRisk01 { get; init; }
    public double InletContainmentRisk01 { get; init; }

    public double MaxChamberBulkPressureConsistencyResidualRelative { get; init; }

    /// <summary>1 − L/D_dev when L/D &lt; reference (0 = well developed).</summary>
    public double ChamberDevelopmentDeficit01 { get; init; }

    public double ResidualChamberEndSwirlRatioVtOverVx { get; init; }

    public double InletSpillPressureMarginPa { get; init; }

    public double ExitDrivePressureMarginPa { get; init; }

    public static FlowTunePhysicsMetrics FromChamber(
        ChamberFirstOrderPhysics? ch,
        double mixedAxialMps,
        SiFlowDiagnostics? si = null)
    {
        if (ch == null)
        {
            return new FlowTunePhysicsMetrics();
        }

        double machAx = Math.Clamp(Math.Abs(mixedAxialMps) / 340.0, 0.0, 0.95);
        double lowAx = Math.Clamp((0.22 - machAx) / 0.22, 0.0, 1.0);

        double devRatio = ch.SwirlSegmentReport?.Containment?.ChamberDevelopmentLengthRatio ?? 1.0;
        double devDeficit = devRatio >= 1.0 ? 0.0 : Math.Clamp(1.0 - devRatio, 0.0, 1.0);
        double pCons = si?.ConservationResiduals?.MaxChamberBulkPressureConsistencyResidualRelative ?? 0.0;

        return new FlowTunePhysicsMetrics
        {
            SwirlChamberAutotuneScore01 = ch.SwirlChamberAutotuneScore01,
            VortexQualityComposite = ch.TuningCompositeQuality,
            RadialPressureUsefulNorm = ch.RadialPressureUsefulNorm,
            RecoverableSwirlAtStatorNorm = ch.RecoverableSwirlFraction01,
            BreakdownRisk01 = ch.VortexStructure.BreakdownRiskScore,
            SeparationRisk01 = ch.DiffuserRecovery.SeparationRiskScore,
            TotalLoss01 = ch.NormalizedTotalPressureLoss01,
            EjectorStress01 = ch.EjectorRegime.RegimeScore,
            LowAxialMomentum01 = lowAx,
            CapturePressureDeficitWeakness01 = ch.CapturePressureDeficitWeakness01,
            BidirectionalSpillRisk01 = ch.SwirlSegmentReport?.Spill?.BidirectionalSpillRisk01 ?? 0.0,
            InletContainmentRisk01 = ch.SwirlSegmentReport?.Containment?.InletContainmentRisk01 ?? 0.0,
            MaxChamberBulkPressureConsistencyResidualRelative = pCons,
            ChamberDevelopmentDeficit01 = devDeficit,
            ResidualChamberEndSwirlRatioVtOverVx = ch.SwirlSegmentReport?.Containment?.ResidualChamberEndSwirlRatioVtOverVx
                ?? 0.0,
            InletSpillPressureMarginPa = ch.SwirlSegmentReport?.Spill?.InletSpillPressureMarginPa ?? 0.0,
            ExitDrivePressureMarginPa = ch.SwirlSegmentReport?.Spill?.ExitDrivePressureMarginPa ?? 0.0
        };
    }
}
