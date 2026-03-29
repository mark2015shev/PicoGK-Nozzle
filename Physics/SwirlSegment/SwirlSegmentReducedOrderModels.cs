using System;
using System.Text;

namespace PicoGK_Run.Physics.SwirlSegment;

/// <summary>Lumped entrainment estimate for one march context (pressure deficit + area).</summary>
public sealed class EntrainmentDriveSummary
{
    /// <summary>Representative P_amb − P_capture_bulk over the march [Pa].</summary>
    public double MeanCapturePressureDeficitPa { get; init; }

    /// <summary>Normalized deficit vs a reference dynamic head (for scoring) [0–1+].</summary>
    public double CapturePressureDeficitNorm01 { get; init; }

    public double TotalEntrainedMassFlowKgS { get; init; }
    public double AmbientDensityKgM3 { get; init; }
    public double EffectiveCaptureAreaM2 { get; init; }
}

/// <summary>
/// Bounded reduced-order radial static pressure structure from rotating flow (see <see cref="RadialVortexPressureModel"/>).
/// </summary>
public sealed class SwirlRadialPressureBalanceState
{
    public double CoreStaticPressurePa { get; init; }
    public double WallStaticPressurePa { get; init; }
    public double BulkStaticPressurePa { get; init; }

    /// <summary>Representative static at entrainment capture boundary (first march step inlet local) [Pa].</summary>
    public double CaptureBoundaryStaticPressurePa { get; init; }

    /// <summary>Bulk static at chamber end / pre-expander [Pa].</summary>
    public double DownstreamBoundaryStaticPressurePa { get; init; }

    public string ModelAssumptionNote { get; init; } = "";
}

/// <summary>Bidirectional lumped spill tendency from wall vs inlet-lip and downstream pressures.</summary>
public sealed class SpillTendencyEstimate
{
    public double InletSpillPressureMarginPa { get; init; }
    public double ExitDrivePressureMarginPa { get; init; }

    /// <summary>Combined 0–1 indicator when inlet spill and downstream drive compete.</summary>
    public double BidirectionalSpillRisk01 { get; init; }
}

/// <summary>Chamber size / annulus checks vs swirl containment (reduced-order).</summary>
public sealed class SwirlContainmentMetrics
{
    /// <summary>P_amb − P_core (representative) [Pa]; larger suggests more core suction vs ambient.</summary>
    public double SwirlContainmentMarginPa { get; init; }

    /// <summary>L/D divided by a reference (1.0 = nominal scale).</summary>
    public double ChamberDevelopmentLengthRatio { get; init; }

    /// <summary>A_inj / A_free_annulus [-].</summary>
    public double FreeAnnulusBlockageRatio { get; init; }

    /// <summary>0–1 higher when inlet spill margin and containment margin suggest upstream escape risk.</summary>
    public double InletContainmentRisk01 { get; init; }
}

/// <summary>Vector- and pressure-based readout of the mixed swirl segment (last march station).</summary>
public sealed class SwirlFlowDirectionState
{
    public double AxialVelocityRepresentativeMps { get; init; }
    public double TangentialVelocityRepresentativeMps { get; init; }

    /// <summary>Lumped (P_wall − P_core) / (R_wall − R_core) [Pa/m].</summary>
    public double RadialPressureGradientPaPerM { get; init; }

    public double WallStaticPressurePa { get; init; }
    public double CoreStaticPressurePa { get; init; }
    public double CaptureBoundaryStaticPressurePa { get; init; }
    public double DownstreamBoundaryStaticPressurePa { get; init; }

    public double InletSpillRisk01 { get; init; }
    public double DownstreamDriveRisk01 { get; init; }

    public bool TangentialDominatesAxial { get; init; }
    public bool AxialDownstreamTendency { get; init; }
    public bool InletReverseDriveTendency { get; init; }
    public bool RadialOutwardWallLoading { get; init; }
}

/// <summary>Expander as area growth + wall pressure axial contribution + separation (reduced-order).</summary>
public sealed class ExpanderRecoveryEstimate
{
    public double ExpanderWallAxialForceN { get; init; }
    public double ExpanderMomentumRedirection01 { get; init; }
    public double ExpanderSeparationRisk01 { get; init; }
    public double ExpanderDeltaPEffectivePa { get; init; }
}

/// <summary>Single attach point for injector/swirl-segment reduced-order diagnostics and debug text.</summary>
public sealed class SwirlSegmentReducedOrderReport
{
    public InjectorVelocityState? InjectorVelocity { get; init; }
    public EntrainmentDriveSummary? Entrainment { get; init; }
    public SwirlRadialPressureBalanceState? RadialPressureBalance { get; init; }
    public SpillTendencyEstimate? Spill { get; init; }
    public SwirlContainmentMetrics? Containment { get; init; }
    public SwirlFlowDirectionState? FlowDirection { get; init; }
    public ExpanderRecoveryEstimate? Expander { get; init; }

    public string FormatDebugBlock()
    {
        var sb = new StringBuilder();
        sb.AppendLine("--- Swirl segment (reduced-order physics snapshot) ---");
        if (InjectorVelocity != null)
        {
            InjectorVelocityState v = InjectorVelocity;
            sb.AppendLine(
                $"Injector |V|={v.VelocityMagnitudeMps:F2} m/s  Vx={v.AxialVelocityMps:F2}  Vt={v.TangentialVelocityMps:F2}  Vr={v.RadialVelocityMps:F2}  flow angle={v.FlowAngleDeg:F2}°");
        }

        if (Entrainment != null)
        {
            EntrainmentDriveSummary e = Entrainment;
            sb.AppendLine(
                $"Capture mean ΔP (ambient−local)={e.MeanCapturePressureDeficitPa:F1} Pa  ṁ_ent total≈{e.TotalEntrainedMassFlowKgS:F5} kg/s  A_cap={e.EffectiveCaptureAreaM2:E4} m²");
        }

        if (RadialPressureBalance != null)
        {
            SwirlRadialPressureBalanceState r = RadialPressureBalance;
            sb.AppendLine(
                $"Radial balance: P_core={r.CoreStaticPressurePa:F1}  P_wall={r.WallStaticPressurePa:F1}  P_cap={r.CaptureBoundaryStaticPressurePa:F1}  P_dn={r.DownstreamBoundaryStaticPressurePa:F1} Pa");
        }

        if (Spill != null)
        {
            SpillTendencyEstimate s = Spill;
            sb.AppendLine(
                $"Spill: inlet margin={s.InletSpillPressureMarginPa:F1} Pa  exit drive margin={s.ExitDrivePressureMarginPa:F1} Pa  bidirectional risk={s.BidirectionalSpillRisk01:F3}");
        }

        if (FlowDirection != null)
        {
            SwirlFlowDirectionState f = FlowDirection;
            sb.AppendLine(
                $"Flow direction: Va={f.AxialVelocityRepresentativeMps:F2}  Vt={f.TangentialVelocityRepresentativeMps:F2} m/s  dP/dr≈{f.RadialPressureGradientPaPerM:F0} Pa/m");
            sb.AppendLine(
                $"  tangential-dominant={f.TangentialDominatesAxial}  downstream axial tendency={f.AxialDownstreamTendency}  inlet reverse-drive={f.InletReverseDriveTendency}  outward wall loading={f.RadialOutwardWallLoading}");
        }

        if (Expander != null)
        {
            ExpanderRecoveryEstimate x = Expander;
            sb.AppendLine(
                $"Expander: F_wall,ax≈{x.ExpanderWallAxialForceN:F2} N  redirection={x.ExpanderMomentumRedirection01:F3}  separation risk={x.ExpanderSeparationRisk01:F3}  ΔP_eff={x.ExpanderDeltaPEffectivePa:F1} Pa");
        }

        return sb.ToString().TrimEnd();
    }
}
