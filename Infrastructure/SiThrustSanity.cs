using System;
using System.Collections.Generic;
using System.Linq;
using PicoGK;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Infrastructure;

/// <summary>
/// Hard SI thrust / pressure sanity: net thrust uses only the exit CV; these checks catch bogus march static P
/// (huge pressure thrust with tiny momentum) and non-physical chamber absolute pressures.
/// </summary>
internal static class SiThrustSanity
{
    private static void Emit(string line)
    {
        try
        {
            Library.Log(line);
        }
        catch (Exception)
        {
            Console.WriteLine(line);
        }
    }

    /// <summary>10 bar absolute — model is ambient-fed ejector class; above this is treated as invalid.</summary>
    internal const double MaxChamberAbsoluteStaticPressurePa = 1_000_000.0;

    internal static void LogCvAndApplyAssertions(
        RunConfiguration run,
        ThrustCalculator.ThrustControlVolumeResult cv,
        double mdotExitKgS,
        double vExitMps,
        double pExitPa,
        double pAmbientPa,
        double aExitM2,
        double momentumThrustN,
        double exitPlanePressureThrustN,
        ref double netThrustN,
        ref bool thrustCvValid,
        ref string? thrustInvalidReason,
        double inletPressureForceDiagnosticN,
        double expanderPressureForceDiagnosticN,
        double chamberStaticNearInjectorPa,
        double marchInletAssignedStaticPa,
        IReadOnlyList<FlowMarchStepResult> marchSteps,
        out bool chamberPressureHardAssertionTripped)
    {
        chamberPressureHardAssertionTripped = false;
        double fMom = momentumThrustN;
        double fP = exitPlanePressureThrustN;
        double netBeforeHard = fMom + fP;

        Emit(
            $"SI thrust CV (sole authority): mdot_exit={mdotExitKgS:G9} kg/s, V_exit={vExitMps:G9} m/s, " +
            $"P_exit={pExitPa:G9} Pa, P_amb={pAmbientPa:G9} Pa, A_exit={aExitM2:G9} m²");
        Emit(
            $"SI thrust CV terms: F_mom={fMom:F3} N, F_p_exit_plane={fP:F3} N, F_net_before_hard_checks={netBeforeHard:F3} N; " +
            "other forces added to F_net: 0 " +
            $"(diagnostic-only: inlet_Σstep={inletPressureForceDiagnosticN:F3} N, expander_wall={expanderPressureForceDiagnosticN:F3} N)");

        if (marchSteps.Count > 0)
        {
            double pMin = marchSteps.Min(s => s.MixedStaticPressurePa);
            double pMax = marchSteps.Max(s => s.MixedStaticPressurePa);
            Emit($"SI march MixedStaticPressurePa min/max: {pMin:G9} / {pMax:G9}");
        }

        bool trip = false;

        if (chamberStaticNearInjectorPa > MaxChamberAbsoluteStaticPressurePa
            || marchInletAssignedStaticPa > MaxChamberAbsoluteStaticPressurePa)
        {
            chamberPressureHardAssertionTripped = true;
            trip = true;
            Emit(
                "SI HARD ASSERT: chamber / march inlet static > 10 bar abs — " +
                $"P_near_injector={chamberStaticNearInjectorPa:G9} Pa, march_inlet_assigned={marchInletAssignedStaticPa:G9} Pa");
            thrustCvValid = false;
            thrustInvalidReason = Append(thrustInvalidReason, "Chamber or march inlet static P > 10 bar abs (invalid for this SI path).");
            netThrustN = 0.0;
        }

        // Huge pressure thrust with low-speed, low-mdot mixed jet — march static is not credible as exit P for thrust.
        bool pressureDominatesLowMomentum =
            Math.Abs(fP) > 250.0
            && Math.Abs(fMom) < 500.0
            && mdotExitKgS < 5.0
            && Math.Abs(vExitMps) < 25.0;
        if (pressureDominatesLowMomentum)
        {
            trip = true;
            Emit(
                "SI HARD ASSERT: |F_p,exit| inconsistent with low momentum scale — " +
                $"|F_p|={Math.Abs(fP):F1} N, |F_mom|={Math.Abs(fMom):F1} N, mdot={mdotExitKgS:G4} kg/s, |V_exit|={Math.Abs(vExitMps):F2} m/s");
            thrustCvValid = false;
            thrustInvalidReason = Append(
                thrustInvalidReason,
                "Exit-plane pressure thrust dominates while momentum scale is low (bogus exit static from march).");
            netThrustN = 0.0;
        }

        if (run.ApplyHardSiThrustAndPressureAssertions && Math.Abs(netBeforeHard) > 5000.0)
        {
            trip = true;
            Emit($"SI HARD ASSERT (K320-class run flag): |F_net|={Math.Abs(netBeforeHard):F2} N > 5000 N");
            thrustCvValid = false;
            thrustInvalidReason = Append(
                thrustInvalidReason,
                "K320-class assertion: |net thrust| > 5000 N on first-order SI ejector baseline.");
            netThrustN = 0.0;
        }

        if (trip)
        {
            Emit(
                $"SI thrust INVALID after hard checks — F_net forced to 0; " +
                $"diagnostic F_mom={fMom:F3} N, F_p_exit={fP:F3} N (not used as authority).");
        }
    }

    private static string? Append(string? prior, string add)
    {
        if (string.IsNullOrEmpty(prior))
            return add;
        return prior + " | " + add;
    }
}
