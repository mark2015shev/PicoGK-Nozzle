using System;
using System.Text;

namespace PicoGK_Run.Physics.SwirlSegment;

/// <summary>Lumped entrainment estimate for one march context (pressure deficit + area).</summary>
public sealed class EntrainmentDriveSummary
{
    /// <summary>Representative P_amb − P_capture_boundary (radial model at entrainment step) over the march [Pa].</summary>
    public double MeanCapturePressureDeficitPa { get; init; }

    /// <summary>Normalized deficit vs a reference dynamic head (for scoring) [0–1+].</summary>
    public double CapturePressureDeficitNorm01 { get; init; }

    public double TotalEntrainedMassFlowKgS { get; init; }
    public double AmbientDensityKgM3 { get; init; }

    /// <summary>Capture plane area from geometry [m²] (may exceed effective entry if not bottlenecked).</summary>
    public double EffectiveCaptureAreaM2 { get; init; }

    /// <summary>Representative min(capture, annulus, bore, free) used in pressure-driven entrainment [m²].</summary>
    public double MeanEffectiveEntrainmentEntryAreaM2 { get; init; }
}

/// <summary>
/// Primary reduced-order radial static pressure field for the swirl chamber (see <see cref="RadialVortexPressureModel"/>).
/// Drives entrainment, spill/drive margins, and expander handoff — not a decorative add-on.
/// </summary>
public sealed class SwirlRadialPressureState
{
    public double CoreStaticPressurePa { get; init; }
    public double WallStaticPressurePa { get; init; }
    public double BulkStaticPressurePa { get; init; }

    /// <summary>Static at capture boundary for entrainment (radial model at inlet station) [Pa].</summary>
    public double CaptureBoundaryStaticPressurePa { get; init; }

    /// <summary>Lumped static reference at expander / downstream boundary [Pa].</summary>
    public double DownstreamBoundaryStaticPressurePa { get; init; }

    /// <summary>(P_wall − P_core) / (R_wall − R_core) representative [Pa/m].</summary>
    public double RadialPressureGradientRepresentativePaPerM { get; init; }

    public double AssumedCoreRadiusMm { get; init; }
    public double AssumedOuterRadiusMm { get; init; }

    public string ModelAssumptionNote { get; init; } = "";
}

/// <summary>Bidirectional lumped spill tendency from wall vs inlet-lip and downstream pressures.</summary>
public sealed class SpillTendencyEstimate
{
    /// <summary>P_wall,upstream − P_boundary,inlet [Pa].</summary>
    public double InletSpillPressureMarginPa { get; init; }

    /// <summary>P_wall,downstream − P_boundary,expander [Pa]; large positive ⇒ wall exceeds expander reference (blocked tendency).</summary>
    public double ExitDrivePressureMarginPa { get; init; }

    /// <summary>0–1 when inlet wall exceeds capture-boundary pressure (reduced-order spill-out tendency).</summary>
    public double InletSpillRisk01 { get; init; }

    /// <summary>0–1 when wall exceeds expander-entry reference static (weak forward pressure drive to expander).</summary>
    public double DownstreamDriveRisk01 { get; init; }

    /// <summary>Combined 0–1 indicator when inlet spill and downstream drive compete.</summary>
    public double BidirectionalSpillRisk01 { get; init; }
}

/// <summary>Chamber size / annulus checks vs swirl containment (reduced-order).</summary>
public sealed class SwirlContainmentMetrics
{
    /// <summary>P_amb − P_core (representative) [Pa]; larger suggests more core suction vs ambient.</summary>
    public double SwirlContainmentMarginPa { get; init; }

    /// <summary>Chamber L/D divided by reference L/D (development vs nominal length).</summary>
    public double ChamberDevelopmentLengthRatio { get; init; }

    /// <summary>A_inj / A_free_annulus [-].</summary>
    public double FreeAnnulusBlockageRatio { get; init; }

    /// <summary>|Vt|/max(|Vx|, floor) at chamber exit (mixed stream).</summary>
    public double ResidualChamberEndSwirlRatioVtOverVx { get; init; }

    /// <summary>0–1 higher when inlet spill margin and containment margin suggest upstream escape risk.</summary>
    public double InletContainmentRisk01 { get; init; }
}

/// <summary>End-of-chamber Ġ_θ bookkeeping (last march station).</summary>
public sealed class SwirlAngularMomentumState
{
    /// <summary>Ġ_θ = ṁ R V_θ,bulk [kg·m²/s²].</summary>
    public double AngularMomentumFluxKgM2PerS2 { get; init; }

    public double WallLossTermKgM2PerS2 { get; init; }
    public double MixingLossTermKgM2PerS2 { get; init; }
    public double EntrainmentDilutionTermKgM2PerS2 { get; init; }

    public double ResidualTangentialVelocityMps { get; init; }

    /// <summary>|Vt|/max(|Vx|, floor).</summary>
    public double ResidualSwirlRatioVtOverVx { get; init; }
}

/// <summary>Chamber exit → expander entry lumped state (SI handoff).</summary>
public sealed class ChamberExpanderInletHandoffState
{
    public double MdotTotalKgS { get; init; }
    public double AxialVelocityMps { get; init; }
    public double TangentialVelocityMps { get; init; }
    public double WallStaticPressurePa { get; init; }
    public double BulkStaticPressurePa { get; init; }
    public double ResidualSwirlRatioVtOverVx { get; init; }

    /// <summary>Same convention as <see cref="SpillTendencyEstimate.ExitDrivePressureMarginPa"/> (P_wall,ds − P_boundary,expander).</summary>
    public double DownstreamPressureDriveMarginPa { get; init; }
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

    /// <summary>Pressure recovery from pressure-force diffuser model (Δp on projected area) [Pa].</summary>
    public double ExpanderPressureRecoveryPa { get; init; }
}

/// <summary>Single attach point for injector/swirl-segment reduced-order diagnostics and debug text.</summary>
public sealed class SwirlSegmentReducedOrderReport
{
    public InjectorVelocityState? InjectorVelocity { get; init; }
    public ChamberInletState? ChamberInlet { get; init; }
    public EntrainmentDriveSummary? Entrainment { get; init; }
    public SwirlRadialPressureState? RadialPressure { get; init; }
    public SpillTendencyEstimate? Spill { get; init; }
    public SwirlContainmentMetrics? Containment { get; init; }
    public SwirlFlowDirectionState? FlowDirection { get; init; }
    public ExpanderRecoveryEstimate? Expander { get; init; }
    public SwirlAngularMomentumState? AngularMomentum { get; init; }
    public ChamberExitState? ChamberExit { get; init; }
    public ChamberExpanderInletHandoffState? ExpanderInletHandoff { get; init; }

    public string FormatDebugBlock()
    {
        var sb = new StringBuilder();
        sb.AppendLine("--- Swirl segment (reduced-order physics snapshot) ---");
        if (InjectorVelocity != null)
        {
            InjectorVelocityState v = InjectorVelocity;
            sb.AppendLine(
                $"Injector |V|={v.VelocityMagnitudeMps:F2} m/s  Vx={v.AxialVelocityMps:F2}  Vt={v.TangentialVelocityMps:F2}  Vr={v.RadialVelocityMps:F2}  β={v.FlowAngleDeg:F2}°  Vt/Vx={v.SwirlRatioVtOverVx:F3}");
        }

        if (ChamberInlet != null)
        {
            ChamberInletState c = ChamberInlet;
            sb.AppendLine(
                $"Chamber inlet (authoritative): ṁ_primary={c.MassFlowPrimaryKgS:F5} kg/s  P={c.StaticPressurePa:F1} Pa  ρ={c.StaticDensityKgM3:F4} kg/m³  Vx={c.AxialVelocityMps:F2}  Vt={c.TangentialVelocityMps:F2}  Vr={c.RadialVelocityMps:F2}  |V|={c.VelocityMagnitudeMps:F2}  β={c.FlowAngleDeg:F2}°");
        }

        if (Entrainment != null)
        {
            EntrainmentDriveSummary e = Entrainment;
            sb.AppendLine(
                $"Pressure-driven entrainment: mean ΔP_amb−P_cap={e.MeanCapturePressureDeficitPa:F1} Pa  ṁ_ent,total≈{e.TotalEntrainedMassFlowKgS:F5} kg/s  A_cap,geom={e.EffectiveCaptureAreaM2:E4} m²  A_eff,entry≈{e.MeanEffectiveEntrainmentEntryAreaM2:E4} m²");
        }

        if (RadialPressure != null)
        {
            SwirlRadialPressureState r = RadialPressure;
            sb.AppendLine(
                $"Radial pressure (primary chamber field): P_core={r.CoreStaticPressurePa:F1}  P_wall={r.WallStaticPressurePa:F1}  P_cap={r.CaptureBoundaryStaticPressurePa:F1}  P_dn={r.DownstreamBoundaryStaticPressurePa:F1} Pa");
            sb.AppendLine(
                $"  dP/dr≈{r.RadialPressureGradientRepresentativePaPerM:F0} Pa/m  r_core≈{r.AssumedCoreRadiusMm:F2} mm  R_wall≈{r.AssumedOuterRadiusMm:F2} mm");
        }

        if (AngularMomentum != null)
        {
            SwirlAngularMomentumState a = AngularMomentum;
            sb.AppendLine(
                $"Angular momentum flux Ġ_θ≈{a.AngularMomentumFluxKgM2PerS2:E4} kg·m²/s²  wall loss≈{a.WallLossTermKgM2PerS2:E4}  mixing≈{a.MixingLossTermKgM2PerS2:E4}  entrainment dilution≈{a.EntrainmentDilutionTermKgM2PerS2:E4} (last step)");
            sb.AppendLine(
                $"  residual Vt={a.ResidualTangentialVelocityMps:F2} m/s  Vt/Vx≈{a.ResidualSwirlRatioVtOverVx:F3}");
        }

        if (Spill != null)
        {
            SpillTendencyEstimate s = Spill;
            sb.AppendLine(
                $"Spill/drive margins: ΔP_inlet=P_wall,up−P_cap={s.InletSpillPressureMarginPa:F1} Pa  ΔP_exit=P_wall,ds−P_exp={s.ExitDrivePressureMarginPa:F1} Pa");
            sb.AppendLine(
                $"  inlet spill risk={s.InletSpillRisk01:F3}  downstream drive risk={s.DownstreamDriveRisk01:F3}  bidirectional={s.BidirectionalSpillRisk01:F3}");
        }

        if (Containment != null)
        {
            SwirlContainmentMetrics c = Containment;
            sb.AppendLine(
                $"Containment: ΔP_swirl margin={c.SwirlContainmentMarginPa:F1} Pa  L/D dev ratio={c.ChamberDevelopmentLengthRatio:F3}  A_inj/A_free={c.FreeAnnulusBlockageRatio:F3}  Vt/Vx exit={c.ResidualChamberEndSwirlRatioVtOverVx:F3}  inlet containment risk={c.InletContainmentRisk01:F3}");
        }

        if (FlowDirection != null)
        {
            SwirlFlowDirectionState f = FlowDirection;
            sb.AppendLine(
                $"Flow direction: Va={f.AxialVelocityRepresentativeMps:F2}  Vt={f.TangentialVelocityRepresentativeMps:F2} m/s  dP/dr≈{f.RadialPressureGradientPaPerM:F0} Pa/m");
            sb.AppendLine(
                $"  tangential-dominant={f.TangentialDominatesAxial}  downstream axial tendency={f.AxialDownstreamTendency}  inlet reverse-drive={f.InletReverseDriveTendency}  outward wall loading={f.RadialOutwardWallLoading}");
        }

        if (ChamberExit != null)
        {
            ChamberExitState x = ChamberExit;
            sb.AppendLine(
                $"Chamber exit (expander handoff): ṁ={x.MdotTotalKgS:F5} kg/s  Vx={x.AxialVelocityMps:F2}  Vt={x.TangentialVelocityMps:F2} m/s  P_static={x.StaticPressurePa:F1}  P₀={x.TotalPressurePa:F1}  P_wall={x.WallStaticPressurePa:F1} Pa");
            sb.AppendLine(
                $"  Ġ_θ={x.AngularMomentumFluxKgM2PerS2:E4} kg·m²/s²  Vt/Vx={x.ResidualSwirlRatioVtOverVx:F3}  ΔP_inlet margin={x.InletSpillPressureMarginPa:F1}  ΔP_exit margin={x.ExitDrivePressureMarginPa:F1} Pa");
        }

        if (ExpanderInletHandoff != null)
        {
            ChamberExpanderInletHandoffState h = ExpanderInletHandoff;
            sb.AppendLine(
                $"Expander inlet (legacy handoff line): ṁ={h.MdotTotalKgS:F5} kg/s  Vx={h.AxialVelocityMps:F2}  Vt={h.TangentialVelocityMps:F2} m/s  P_wall={h.WallStaticPressurePa:F1}  P_bulk={h.BulkStaticPressurePa:F1} Pa");
            sb.AppendLine(
                $"  residual Vt/Vx={h.ResidualSwirlRatioVtOverVx:F3}  exit margin={h.DownstreamPressureDriveMarginPa:F1} Pa");
        }

        if (Expander != null)
        {
            ExpanderRecoveryEstimate x = Expander;
            sb.AppendLine(
                $"Expander: F_wall,ax≈{x.ExpanderWallAxialForceN:F2} N  Δp_recovery={x.ExpanderPressureRecoveryPa:F1} Pa  redirection={x.ExpanderMomentumRedirection01:F3}  separation risk={x.ExpanderSeparationRisk01:F3}  ΔP_eff={x.ExpanderDeltaPEffectivePa:F1} Pa");
        }

        return sb.ToString().TrimEnd();
    }
}
