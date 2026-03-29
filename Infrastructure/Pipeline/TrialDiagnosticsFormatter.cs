using System.Globalization;
using PicoGK_Run.Infrastructure;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Infrastructure.Pipeline;

/// <summary>Console + CSV rows for autotune trial transparency (same fields tuning and reporting can consume).</summary>
public static class TrialDiagnosticsFormatter
{
    public static string CsvHeader =>
        "trial_index,stage,chamber_bore_mm,chamber_length_mm,diffuser_half_angle_deg,injector_yaw_deg,stator_proxy_deg,exit_diameter_mm,exit_area_ratio_vs_bore2,entrainment_ratio,swirl_ratio_vt_over_va,net_thrust_n,score,top_penalty_source,pass_status,reject_reason";

    public static string FormatDataRow(
        int trialIndex,
        int stage,
        NozzleDesignInputs d,
        FlowTuneEvaluation ev,
        double? score)
    {
        var si = ev.SiDiagnostics;
        double va = Math.Max(Math.Abs(si?.FinalAxialVelocityMps ?? 0.0), 1e-9);
        double swirlRatio = Math.Abs(si?.FinalTangentialVelocityMps ?? 0.0) / va;
        double bore = Math.Max(d.SwirlChamberDiameterMm, 1e-6);
        double exitAr = d.ExitDiameterMm * d.ExitDiameterMm / (bore * bore);

        string topPen = ev.TopPenaltySource
            ?? ev.UnifiedEvaluation?.PhysicsPenalties.TopSource
            ?? string.Empty;

        bool reject = ev.HasDesignError || (ev.ConstraintBreakdown?.Reject ?? false);
        string status = reject ? "REJECT" : "PASS";
        string reason = BuildRejectReason(ev);

        string scoreStr = score.HasValue ? score.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;

        static string Csv(string? s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        var parts = new[]
        {
            trialIndex.ToString(CultureInfo.InvariantCulture),
            stage.ToString(CultureInfo.InvariantCulture),
            d.SwirlChamberDiameterMm.ToString(CultureInfo.InvariantCulture),
            d.SwirlChamberLengthMm.ToString(CultureInfo.InvariantCulture),
            d.ExpanderHalfAngleDeg.ToString(CultureInfo.InvariantCulture),
            d.InjectorYawAngleDeg.ToString(CultureInfo.InvariantCulture),
            d.StatorVaneAngleDeg.ToString(CultureInfo.InvariantCulture),
            d.ExitDiameterMm.ToString(CultureInfo.InvariantCulture),
            exitAr.ToString(CultureInfo.InvariantCulture),
            ev.EntrainmentRatio.ToString(CultureInfo.InvariantCulture),
            swirlRatio.ToString(CultureInfo.InvariantCulture),
            ev.NetThrustN.ToString(CultureInfo.InvariantCulture),
            scoreStr,
            Csv(topPen),
            status,
            Csv(reason)
        };
        return string.Join(",", parts);
    }

    public static void PrintTableHeaderToConsole()
    {
        ConsoleStatusWriter.WriteLine(CsvHeader.Replace(',', '\t'), StatusLevel.Normal);
    }

    private static string BuildRejectReason(FlowTuneEvaluation ev)
    {
        if (ev.HasDesignError)
            return "DESIGN_ERROR";
        if (ev.ConstraintBreakdown is { Reject: true, Reasons: { } r } && r.Count > 0)
            return string.Join(";", r);
        if (!string.IsNullOrEmpty(ev.UnifiedEvaluation?.HardRejectReason))
            return ev.UnifiedEvaluation!.HardRejectReason!;
        return string.Empty;
    }
}
