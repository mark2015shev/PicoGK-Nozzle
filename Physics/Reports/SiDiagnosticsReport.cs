using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PicoGK_Run.Physics.Reports;

/// <summary>Structured SI run summary for console tools and regression JSON diffs (first-order model — not CFD).</summary>
public sealed class SiDiagnosticsReport
{
    public SourceDischargeSummary? Source { get; init; }
    public InjectorStateSummary? Injector { get; init; }
    public SwirlCapacityDualSummary? SwirlEntranceCapacity { get; init; }
    public ChamberMarchSummary? ChamberMarch { get; init; }
    public StatorRecoverySummary? StatorRecovery { get; init; }
    public ThrustBreakdownSummary? Thrust { get; init; }
    public IReadOnlyList<string> MarchInvariantWarnings { get; init; } = Array.Empty<string>();

    public static SiDiagnosticsReport FromDiagnostics(SiFlowDiagnostics si)
    {
        SourceDischargeSummary? src = si.SourceDischargeConsistency == null
            ? null
            : new SourceDischargeSummary
            {
                MassFlowKgS = si.SourceDischargeConsistency.MassFlowKgS,
                MachAtSourceExit = si.SourceDischargeConsistency.MachNumber,
                P0ImpliedPa = si.SourceDischargeConsistency.P0ImpliedFromDerivedStatePa,
                DerivedStaticPressurePa = si.SourceDischargeConsistency.DerivedStaticPressurePa,
                DerivedDensityKgM3 = si.SourceDischargeConsistency.DerivedDensityKgM3,
                TemperatureInterpretation = si.SourceDischargeConsistency.TemperatureInterpretation.ToString(),
                DerivedStatePhysicsPass = si.SourceDischargeConsistency.DerivedStatePhysicsPass,
                ChokingConsistencyPass = si.SourceDischargeConsistency.ChokingConsistencyPass,
                OverallConsistencyPass = si.SourceDischargeConsistency.OverallPass,
                SelectedThermoMode = si.SourceDischargeConsistency.SelectedMode.ToString()
            };

        InjectorStateSummary? inj = si.InjectorPressureVelocity == null
            ? null
            : new InjectorStateSummary
            {
                FluxSwirlNumber = si.InjectorPlaneFluxSwirlNumber,
                MarchInletAssignedStaticPressurePa = si.InjectorPressureVelocity.MarchInletAssignedStaticPressurePa
            };

        SwirlCapacityDualSummary? cap = si.ChamberMarch?.SwirlEntranceCapacityStations == null
            ? null
            : new SwirlCapacityDualSummary
            {
                EntranceMachRequired = si.ChamberMarch.SwirlEntranceCapacityStations.EntrancePlane.MachRequired,
                ChamberEndMachRequired = si.ChamberMarch.SwirlEntranceCapacityStations.ChamberEnd.MachRequired,
                MachAbsoluteDelta = si.ChamberMarch.SwirlEntranceCapacityStations.MachAbsoluteDelta,
                GoverningStation = si.ChamberMarch.SwirlEntranceCapacityStations.GoverningStationLabel,
                CombinedClassification = si.ChamberMarch.SwirlEntranceCapacityStations.CombinedClassification.ToString()
            };

        ChamberMarchSummary? march = new()
        {
            StepCount = si.PhysicsStepStates.Count,
            FinalMachBulk = si.MarchPhysicsClosure?.FinalMachBulk ?? double.NaN,
            FinalContinuityResidualRelative = si.MarchPhysicsClosure?.FinalContinuityResidualRelative ?? double.NaN,
            AnyEntrainmentChoked = si.AnyEntrainmentStepChoked,
            SumRequestedEntrainmentKgS = si.SumRequestedEntrainmentIncrementsKgS,
            SumActualEntrainmentKgS = si.SumActualEntrainmentIncrementsKgS
        };

        StatorRecoverySummary? st = si.HubStator == null
            ? null
            : new StatorRecoverySummary
            {
                EffectiveStatorEta = si.HubStator.EffectiveStatorEtaUsed,
                RecoveredPressureRisePa = si.StatorRecoveredPressureRisePa,
                TangentialVelocityBeforeMps = si.HubStator.SwirlTangentialVelocityBeforeMps,
                TangentialVelocityAfterMps = si.HubStator.SwirlTangentialVelocityAfterMps
            };

        ThrustBreakdownSummary? thrust = new()
        {
            NetThrustN = si.NetThrustN,
            MomentumThrustN = si.MomentumThrustN,
            PressureThrustN = si.PressureThrustN,
            ControlVolumeValid = si.ThrustControlVolumeIsValid,
            ControlVolumeInvalidReason = si.ThrustControlVolumeInvalidReason,
            MdotExitKgS = si.ThrustCvMdotExitKgS,
            VExitMps = si.ThrustCvVExitMps
        };

        return new SiDiagnosticsReport
        {
            Source = src,
            Injector = inj,
            SwirlEntranceCapacity = cap,
            ChamberMarch = march,
            StatorRecovery = st,
            Thrust = thrust,
            MarchInvariantWarnings = si.MarchInvariantWarnings
        };
    }

    public string ToReportText()
    {
        var lines = new List<string> { "=== SiDiagnosticsReport (structured) ===" };
        if (Source != null)
        {
            lines.Add("[source]");
            lines.Add($"  mdot [kg/s]: {Source.MassFlowKgS:F6}");
            lines.Add($"  Mach @ source exit: {Source.MachAtSourceExit:F4}");
            lines.Add($"  P0 implied [Pa]: {Source.P0ImpliedPa:F1}");
            lines.Add($"  P_static derived [Pa]: {Source.DerivedStaticPressurePa:F1}");
            lines.Add($"  rho derived [kg/m3]: {Source.DerivedDensityKgM3:F4}");
            lines.Add($"  T mode: {Source.TemperatureInterpretation}");
            lines.Add($"  choking @ P0_derived: {(Source.ChokingConsistencyPass ? "PASS" : "FAIL")}");
            lines.Add($"  overall consistency: {(Source.OverallConsistencyPass ? "PASS" : "FAIL")}  mode={Source.SelectedThermoMode}");
        }

        if (SwirlEntranceCapacity != null)
        {
            lines.Add("[swirl capacity — dual station]");
            lines.Add($"  entrance Mach_req: {SwirlEntranceCapacity.EntranceMachRequired:F4}");
            lines.Add($"  chamber-end Mach_req: {SwirlEntranceCapacity.ChamberEndMachRequired:F4}");
            lines.Add($"  |ΔMach|: {SwirlEntranceCapacity.MachAbsoluteDelta:F4}");
            lines.Add($"  governing: {SwirlEntranceCapacity.GoverningStation}");
            lines.Add($"  combined: {SwirlEntranceCapacity.CombinedClassification}");
        }

        if (ChamberMarch != null)
        {
            lines.Add("[chamber march]");
            lines.Add($"  steps: {ChamberMarch.StepCount}");
            lines.Add($"  final Mach (bulk): {ChamberMarch.FinalMachBulk:F4}");
            lines.Add($"  continuity residual (last): {ChamberMarch.FinalContinuityResidualRelative:F4}");
            lines.Add($"  entrainment choked (any): {ChamberMarch.AnyEntrainmentChoked}");
        }

        if (StatorRecovery != null)
        {
            lines.Add("[stator]");
            lines.Add($"  eta_effective: {StatorRecovery.EffectiveStatorEta:F3}");
            lines.Add($"  ΔP_recover [Pa]: {StatorRecovery.RecoveredPressureRisePa:F1}");
        }

        if (Thrust != null)
        {
            lines.Add("[thrust CV]");
            lines.Add($"  net [N]: {Thrust.NetThrustN:F2}");
            lines.Add($"  momentum [N]: {Thrust.MomentumThrustN:F2}");
            lines.Add($"  pressure [N]: {Thrust.PressureThrustN:F2}");
            lines.Add($"  valid: {Thrust.ControlVolumeValid}  {Thrust.ControlVolumeInvalidReason ?? ""}");
            lines.Add($"  mdot_exit [kg/s]: {Thrust.MdotExitKgS:F5}  V_exit [m/s]: {Thrust.VExitMps:F2}");
        }

        if (MarchInvariantWarnings.Count > 0)
        {
            lines.Add("[march invariants]");
            foreach (string w in MarchInvariantWarnings)
                lines.Add($"  — {w}");
        }

        lines.Add("=== end SiDiagnosticsReport ===");
        return string.Join(Environment.NewLine, lines);
    }

    public string ToJson()
    {
        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        return JsonSerializer.Serialize(this, opts);
    }
}

public sealed class SourceDischargeSummary
{
    public double MassFlowKgS { get; init; }
    public double MachAtSourceExit { get; init; }
    public double P0ImpliedPa { get; init; }
    public double DerivedStaticPressurePa { get; init; }
    public double DerivedDensityKgM3 { get; init; }
    public string TemperatureInterpretation { get; init; } = "";
    public bool DerivedStatePhysicsPass { get; init; }
    public bool ChokingConsistencyPass { get; init; }
    public bool OverallConsistencyPass { get; init; }
    public string SelectedThermoMode { get; init; } = "";
}

public sealed class InjectorStateSummary
{
    public double FluxSwirlNumber { get; init; }
    public double MarchInletAssignedStaticPressurePa { get; init; }
}

public sealed class SwirlCapacityDualSummary
{
    public double EntranceMachRequired { get; init; }
    public double ChamberEndMachRequired { get; init; }
    public double MachAbsoluteDelta { get; init; }
    public string GoverningStation { get; init; } = "";
    public string CombinedClassification { get; init; } = "";
}

public sealed class ChamberMarchSummary
{
    public int StepCount { get; init; }
    public double FinalMachBulk { get; init; }
    public double FinalContinuityResidualRelative { get; init; }
    public bool AnyEntrainmentChoked { get; init; }
    public double SumRequestedEntrainmentKgS { get; init; }
    public double SumActualEntrainmentKgS { get; init; }
}

public sealed class StatorRecoverySummary
{
    public double EffectiveStatorEta { get; init; }
    public double RecoveredPressureRisePa { get; init; }
    public double TangentialVelocityBeforeMps { get; init; }
    public double TangentialVelocityAfterMps { get; init; }
}

public sealed class ThrustBreakdownSummary
{
    public double NetThrustN { get; init; }
    public double MomentumThrustN { get; init; }
    public double PressureThrustN { get; init; }
    public bool ControlVolumeValid { get; init; }
    public string? ControlVolumeInvalidReason { get; init; }
    public double MdotExitKgS { get; init; }
    public double VExitMps { get; init; }
}
