using System;
using System.Collections.Generic;
using PicoGK_Run.Core;

namespace PicoGK_Run.Physics;

/// <summary>Live SI thermodynamics: only discharge-derived state governs march and injector inputs.</summary>
public enum SourceLiveThermodynamicsMode
{
    /// <summary>ρ, T_static, P_static, |V| from ṁ, A, V, T, gas; no PressureRatio authority.</summary>
    DerivedDischargeStateOnly
}

/// <summary>
/// Derived source-exit discharge from ṁ, A, V, T and ideal gas. Legacy <see cref="SourceInputs.PressureRatio"/> is diagnostic only.
/// </summary>
public sealed class SourceDischargeConsistencyReport
{
    public double MassFlowKgS { get; init; }
    public double SourceExitAreaM2 { get; init; }
    public double VelocityUsedMps { get; init; }
    public bool VelocityFromSpecifiedSourceSpeed { get; init; }
    public bool VelocityInferredFromAmbientContinuity { get; init; }
    public double TemperatureInputK { get; init; }
    public SourceTemperatureInterpretation TemperatureInterpretation { get; init; }
    public double DerivedStaticTemperatureK { get; init; }
    public double DerivedTotalTemperatureK { get; init; }
    public double DerivedDensityKgM3 { get; init; }
    public double DerivedStaticPressurePa { get; init; }
    public double MachNumber { get; init; }
    public double P0ImpliedFromDerivedStatePa { get; init; }
    public bool DerivedStatePhysicsPass { get; init; }
    public bool ChokingConsistencyPass { get; init; }
    public SourceLiveThermodynamicsMode SelectedMode { get; init; } = SourceLiveThermodynamicsMode.DerivedDischargeStateOnly;
    public IReadOnlyList<string> FailureAndWarningMessages { get; init; } = Array.Empty<string>();

    /// <summary>Live pass: derived discharge physics and choked-capacity check at derived P₀ only.</summary>
    public bool OverallPass => DerivedStatePhysicsPass && ChokingConsistencyPass;

    public IReadOnlyList<string> FormatReportLines()
    {
        var lines = new List<string>
        {
            "DERIVED SOURCE / INJECTOR STATE",
            "  live source mode:                       DerivedDischargeStateOnly",
            $"  area A [m2]:                            {SourceExitAreaM2:E6}",
            $"  mdot [kg/s]:                            {MassFlowKgS:F6}",
            $"  velocity |V| [m/s]:                     {VelocityUsedMps:F4}  (specified={(VelocityFromSpecifiedSourceSpeed ? "yes" : "no")}, inferred_from_ambient_continuity={(VelocityInferredFromAmbientContinuity ? "yes" : "no")})",
            $"  temperature input T_in [K]:           {TemperatureInputK:F2}",
            $"  temperature interpretation:            {TemperatureInterpretation}",
            "  formulas: rho = mdot / (A * |V|);  T_static = T_in or (T_total - V^2/(2*cp));  P_static = rho * R * T_static;",
            $"  derived rho [kg/m3]:                    {DerivedDensityKgM3:F6}",
            $"  derived T_static [K]:                   {DerivedStaticTemperatureK:F2}",
            $"  derived P_static [Pa]:                 {DerivedStaticPressurePa:F2}",
            $"  Mach (|V|/a(T_static)) [-]:           {MachNumber:F4}",
            $"  P0 diagnostic (isentropic from derived state) [Pa]: {P0ImpliedFromDerivedStatePa:F2}",
            $"  derived-state physics:                 {(DerivedStatePhysicsPass ? "PASS" : "FAIL")}",
            $"  choking vs mdot @ P0_derived:          {(ChokingConsistencyPass ? "PASS" : "FAIL")}",
            $"  overall live consistency:              {(OverallPass ? "PASS" : "FAIL")}"
        };

        lines.Add("  — Live SI does not use PressureRatio for P, march, thrust, or entrainment.");
        foreach (string m in FailureAndWarningMessages)
            lines.Add($"  — {m}");

        return lines;
    }
}

/// <summary>Builds <see cref="LiveDerivedSourceDischarge"/> and applies choked-capacity check at derived P₀.</summary>
public static class SourceDischargeConsistencyEvaluator
{
    /// <summary>Allowed slack on choked mdot inequality (derived P₀ only).</summary>
    public const double ChokedMassFlowRelativeSlack = 0.03;

    /// <inheritdoc cref="LiveDerivedSourceLimits.ContinuityVelocityRelativeTolerance"/>
    public static double ContinuityVelocityRelativeTolerance => LiveDerivedSourceLimits.ContinuityVelocityRelativeTolerance;

    public static SourceDischargeConsistencyReport Evaluate(
        SourceInputs source,
        GasProperties gas,
        double ambientPressurePa)
    {
        LiveDerivedSourceCoreResult core = LiveDerivedSourceDischarge.ComputeCore(source, gas);
        var messages = new List<string>(core.PhysicsMessages);

        double mChokedDerived =
            core.SourceExitAreaM2 * gas.ChokedMassFlux(core.P0ImpliedFromDerivedStatePa, core.DerivedTotalTemperatureK);

        bool chokePass = !(core.MassFlowKgS > mChokedDerived * (1.0 + ChokedMassFlowRelativeSlack));
        if (!chokePass)
        {
            messages.Add(
                $"FAIL: mdot={core.MassFlowKgS:F6} kg/s exceeds isentropic choked capacity {mChokedDerived:F6} kg/s at derived P0={core.P0ImpliedFromDerivedStatePa:F1} Pa, T_total={core.DerivedTotalTemperatureK:F2} K and A_source.");
        }

        double pAmb = Math.Max(ambientPressurePa, 1.0);
        if (source.HasLegacyPressureRatio)
        {
            messages.Add("Deprecated legacy field PressureRatio is set; not used in live SI path.");
            double p0Pr = Math.Max(pAmb * source.PressureRatio, pAmb + 1.0);
            double relP0 =
                Math.Abs(p0Pr - core.P0ImpliedFromDerivedStatePa)
                / Math.Max(core.P0ImpliedFromDerivedStatePa, 1e-9);
            double mChokedPr = core.SourceExitAreaM2 * gas.ChokedMassFlux(p0Pr, core.DerivedTotalTemperatureK);
            messages.Add(
                $"Legacy diagnostic only: P0 from P_amb·PressureRatio = {p0Pr:F1} Pa vs derived P0 = {core.P0ImpliedFromDerivedStatePa:F1} Pa (relative |Δ| = {relP0:P1}); choked mdot cap @ P0_PR = {mChokedPr:F6} kg/s (not used for pass/fail).");
        }

        return new SourceDischargeConsistencyReport
        {
            MassFlowKgS = core.MassFlowKgS,
            SourceExitAreaM2 = core.SourceExitAreaM2,
            VelocityUsedMps = core.VelocityUsedMps,
            VelocityFromSpecifiedSourceSpeed = core.VelocityFromSpecifiedSourceSpeed,
            VelocityInferredFromAmbientContinuity = core.VelocityInferredFromAmbientContinuity,
            TemperatureInputK = core.TemperatureInputK,
            TemperatureInterpretation = core.TemperatureInterpretation,
            DerivedStaticTemperatureK = core.DerivedStaticTemperatureK,
            DerivedTotalTemperatureK = core.DerivedTotalTemperatureK,
            DerivedDensityKgM3 = core.DerivedDensityKgM3,
            DerivedStaticPressurePa = core.DerivedStaticPressurePa,
            MachNumber = core.MachNumber,
            P0ImpliedFromDerivedStatePa = core.P0ImpliedFromDerivedStatePa,
            DerivedStatePhysicsPass = core.DerivedStatePhysicsPass,
            ChokingConsistencyPass = chokePass,
            SelectedMode = SourceLiveThermodynamicsMode.DerivedDischargeStateOnly,
            FailureAndWarningMessages = messages
        };
    }
}
