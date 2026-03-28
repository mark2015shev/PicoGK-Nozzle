using System;
using System.Collections.Generic;
using PicoGK_Run.Core;

namespace PicoGK_Run.Physics;

/// <summary>Tolerances and bounds shared by live derived-source evaluation.</summary>
public static class LiveDerivedSourceLimits
{
    public const double ContinuityVelocityRelativeTolerance = 0.02;
    public const double MinReasonableStaticPressurePa = 20.0;
    public const double MaxReasonableStaticPressurePa = 8_000_000.0;
}

/// <summary>
/// Sole live SI authority for source/injector thermodynamic state: ρ = ṁ/(A|V|), T_static from temperature mode,
/// P_static = ρ R T_static, then isentropic P₀ for diagnostics only. Does not use <see cref="SourceInputs.PressureRatio"/>.
/// </summary>
public static class LiveDerivedSourceDischarge
{
    /// <summary>Core discharge from ṁ, A, |V|, temperature interpretation, and ideal gas (SI).</summary>
    public static LiveDerivedSourceCoreResult ComputeCore(SourceInputs source, GasProperties gas)
    {
        var messages = new List<string>();
        double mdot = Math.Max(source.MassFlowKgPerSec, 0.0);
        double aM2 = Math.Max(source.SourceOutletAreaMm2 * 1e-6, 1e-18);

        double tIn = source.ExhaustTemperatureK ?? source.AmbientTemperatureK;
        tIn = Math.Max(tIn, 1.0);
        SourceTemperatureInterpretation tMode = source.TemperatureInterpretation;

        bool vFromSpec = source.SourceVelocityMps > 1e-6;
        double vUsed;
        bool vInfAmb = false;
        if (vFromSpec)
        {
            vUsed = Math.Abs(source.SourceVelocityMps);
        }
        else
        {
            double rhoAmb = Math.Max(source.AmbientDensityKgPerM3, 1e-9);
            vUsed = mdot / (rhoAmb * aM2);
            vInfAmb = true;
            messages.Add(
                "SourceVelocityMps not set (≤1e-6): |V| inferred as ṁ/(ρ_ambient·A_source) using AmbientDensityKgPerM3.");
        }

        double rhoCont = mdot / (aM2 * Math.Max(vUsed, 1e-9));
        if (vFromSpec && mdot > 1e-18 && vUsed > 1e-18)
        {
            double vBack = mdot / (rhoCont * aM2);
            double rel = Math.Abs(vBack - vUsed) / Math.Max(vUsed, 1e-9);
            if (rel > LiveDerivedSourceLimits.ContinuityVelocityRelativeTolerance)
                messages.Add(
                    $"FAIL: continuity — mdot/(rho·A) differs from specified |V| by {rel:P1} (tolerance {LiveDerivedSourceLimits.ContinuityVelocityRelativeTolerance:P0}).");
        }

        double cp = gas.SpecificHeatCp;
        double g = GasProperties.Gamma;
        double r = GasProperties.R;

        double tStatic;
        if (tMode == SourceTemperatureInterpretation.Total)
            tStatic = gas.StaticTemperatureFromTotal(tIn, vUsed);
        else
            tStatic = Math.Max(tIn, 1.0);

        double tTotalEnergy = CompressibleFlowMath.TotalTemperatureFromStatic(tStatic, vUsed, cp);

        if (tMode == SourceTemperatureInterpretation.Total && tStatic < 1.5)
            messages.Add("FAIL: T_static from total − V²/(2cp) is non-physical (T_static < 1.5 K).");

        if (tMode == SourceTemperatureInterpretation.Total && Math.Abs(tTotalEnergy - tIn) > 0.05 + 1e-6 * tIn)
            messages.Add(
                $"FAIL: total/static temperature inconsistency: T0 from energy = {tTotalEnergy:F2} K vs input total T = {tIn:F2} K.");

        double pStatic = rhoCont * r * tStatic;

        bool derivedPass = true;
        if (mdot <= 1e-12)
        {
            derivedPass = false;
            messages.Add("FAIL: mass flow is zero or negative.");
        }

        if (rhoCont <= 1e-9 || !double.IsFinite(rhoCont))
        {
            derivedPass = false;
            messages.Add("FAIL: derived density non-finite or non-positive.");
        }

        if (!double.IsFinite(pStatic) || pStatic < LiveDerivedSourceLimits.MinReasonableStaticPressurePa
                                       || pStatic > LiveDerivedSourceLimits.MaxReasonableStaticPressurePa)
        {
            derivedPass = false;
            messages.Add(
                $"FAIL: derived static pressure outside [{LiveDerivedSourceLimits.MinReasonableStaticPressurePa}, {LiveDerivedSourceLimits.MaxReasonableStaticPressurePa}] Pa or non-finite.");
        }

        double aSound = gas.SpeedOfSound(tStatic);
        double mach = aSound > 1e-9 ? vUsed / aSound : 0.0;
        if (!double.IsFinite(mach) || mach < 0.0)
        {
            derivedPass = false;
            messages.Add("FAIL: Mach number non-finite or negative.");
        }

        double p0Derived = pStatic * Math.Pow(tTotalEnergy / Math.Max(tStatic, 1.0), g / (g - 1.0));
        if (!double.IsFinite(p0Derived) || p0Derived < 1.0)
        {
            derivedPass = false;
            messages.Add("FAIL: implied P0 from derived discharge is non-finite or < 1 Pa.");
        }

        if (vFromSpec && mdot > 1e-18
                     && Math.Abs(mdot / (rhoCont * aM2) - vUsed) / vUsed
                     > LiveDerivedSourceLimits.ContinuityVelocityRelativeTolerance)
            derivedPass = false;

        foreach (string m in messages)
        {
            if (m.StartsWith("FAIL:", StringComparison.Ordinal))
                derivedPass = false;
        }

        return new LiveDerivedSourceCoreResult(
            mdot,
            aM2,
            vUsed,
            vFromSpec,
            vInfAmb,
            tIn,
            tMode,
            tStatic,
            tTotalEnergy,
            rhoCont,
            pStatic,
            mach,
            p0Derived,
            derivedPass,
            messages);
    }
}

/// <summary>Result of continuity + ideal-gas static state (live SI). P₀ is diagnostic isentropic from (P_s, T_s, |V|).</summary>
public sealed record LiveDerivedSourceCoreResult(
    double MassFlowKgS,
    double SourceExitAreaM2,
    double VelocityUsedMps,
    bool VelocityFromSpecifiedSourceSpeed,
    bool VelocityInferredFromAmbientContinuity,
    double TemperatureInputK,
    SourceTemperatureInterpretation TemperatureInterpretation,
    double DerivedStaticTemperatureK,
    double DerivedTotalTemperatureK,
    double DerivedDensityKgM3,
    double DerivedStaticPressurePa,
    double MachNumber,
    double P0ImpliedFromDerivedStatePa,
    bool DerivedStatePhysicsPass,
    IReadOnlyList<string> PhysicsMessages);
