using System;
using System.Collections.Generic;
using PicoGK_Run.Core;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Physics;

/// <summary>
/// First-order swirl chamber **bore** sizing from a target entrainment mass-flow budget and a nominal chamber axial velocity.
/// <para>
/// Not CFD: bulk continuity-style area sizing so the chamber is not arbitrarily smaller than needed for ṁ_mix at a chosen V_axial.
/// Annulus uses bore − hub and optional vane blockage (same meaning as SI march).
/// </para>
/// </summary>
public static class SwirlChamberSizingModel
{
    private const double Eps = 1e-12;

    /// <summary>How <see cref="NozzleDesignInputs.SwirlChamberDiameterMm"/> was chosen for reporting.</summary>
    public enum DiameterMode
    {
        /// <summary>Physics-informed synthesis was off; template/hand inputs used.</summary>
        UserTemplate = 0,

        /// <summary>Legacy jet-diameter scale + swirl/ER heuristics (no entrainment-area solve).</summary>
        SynthesisHeuristic = 1,

        /// <summary>Continuity + injector-ratio + caps from <see cref="RunConfiguration.UseDerivedSwirlChamberDiameter"/>.</summary>
        EntrainmentDerived = 2,

        /// <summary>
        /// What-if audit: <see cref="ComputeDerived"/> at <see cref="RunConfiguration.GeometrySynthesisTargetEntrainmentRatio"/> on current seed
        /// when this run did not re-synthesize (e.g. post-autotune final pass).
        /// </summary>
        ReferenceDerivedAtConfiguredTargetEr = 3
    }

    /// <summary>Immutable audit of the entrainment-derived sizing pass (and placeholders for other modes).</summary>
    public sealed class SizingDiagnostics
    {
        public DiameterMode Mode { get; init; }

        public double TargetEntrainmentRatio { get; init; }
        public double MdotCoreKgS { get; init; }
        public double MdotAmbientTargetKgS { get; init; }
        public double MdotMixTargetKgS { get; init; }
        public double RhoMixEstimateKgPerM3 { get; init; }
        public double TargetAxialVelocityMps { get; init; }
        public double AFreeTargetM2 { get; init; }
        public double BlockageFractionOfAnnulus { get; init; }
        public double HubDiameterMm { get; init; }
        public double TotalInjectorAreaMm2 { get; init; }

        /// <summary>Bore diameter after annulus solve [mm].</summary>
        public double ChamberDiameterTargetMm { get; init; }

        /// <summary>A_inj / A_bore (full circle πD²/4) after final D.</summary>
        public double InjectorToBoreAreaRatio { get; init; }

        public double DerivedChamberMaxDiameterMm { get; init; }
        public int AnnulusIterationsUsed { get; init; }
        public int InjectorConstraintIterations { get; init; }

        /// <summary>After annulus continuity solve (before A_inj/A_bore preferred floor).</summary>
        public double DiameterMmAfterContinuityAnnulus { get; init; }

        /// <summary>After enforcing preferred A_inj/A_bore (may be larger than continuity-only).</summary>
        public double DiameterMmAfterInjectorPreferredFloor { get; init; }

        /// <summary>Before global [35,260] mm clamp and after vortex/port floors.</summary>
        public double DiameterMmBeforeGlobalClamp { get; init; }

        public bool WasReducedByMaxJetDiameterCap { get; init; }

        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

        public string SummaryLine =>
            Mode switch
            {
                DiameterMode.UserTemplate => "Chamber diameter: user / template (no synthesis).",
                DiameterMode.SynthesisHeuristic => "Chamber diameter: synthesis heuristic (jet × swirl/ER scale; not entrainment-area derived).",
                DiameterMode.EntrainmentDerived => $"Chamber diameter: entrainment-derived (ER={TargetEntrainmentRatio:F3}, D={ChamberDiameterTargetMm:F2} mm, A_inj/A_bore={InjectorToBoreAreaRatio:F3}).",
                DiameterMode.ReferenceDerivedAtConfiguredTargetEr => $"Reference bore at configured target ER (audit): D={ChamberDiameterTargetMm:F2} mm — compare to actual seed bore in chamber-diameter trace.",
                _ => "Chamber diameter: unknown mode."
            };
    }

    /// <summary>
    /// Continuity: effective passage area A_eff ≥ ṁ_total / (ρ_mix V_ax,target) [m²].
    /// </summary>
    public static double ComputeAFreeTargetM2(double mdotMixKgS, double rhoMixKgPerM3, double targetAxialVelocityMps)
    {
        double v = Math.Max(targetAxialVelocityMps, 5.0);
        double rho = Math.Max(rhoMixKgPerM3, 0.05);
        return mdotMixKgS / (rho * v);
    }

    /// <summary>
    /// Bore diameter [mm] such that annulus free area (after hub and vane blockage) meets A_free_target.
    /// A_pass = (π/4)(D² − D_h²)(1 − φ).
    /// </summary>
    public static double SolveBoreDiameterMmForAnnulus(
        double aFreeTargetM2,
        double hubDiameterMm,
        double blockageFractionOfAnnulus)
    {
        double phi = Math.Clamp(blockageFractionOfAnnulus, 0.0, 0.92);
        double factor = Math.Max(1.0 - phi, 0.08);
        double dhM = Math.Max(hubDiameterMm, 0.0) * 1e-3;
        double dh2 = dhM * dhM;
        double aNeed = Math.Max(aFreeTargetM2, Eps);
        double d2 = dh2 + 4.0 * aNeed / (Math.PI * factor);
        double dM = Math.Sqrt(Math.Max(d2, dh2 + Eps));
        return dM * 1000.0;
    }

    /// <summary>
    /// Cylindrical bore per user note: A_bore = A_free / (1 − φ), D = sqrt(4 A_bore / π) — used only as initial guess when hub is estimated from D.
    /// </summary>
    public static double SolveBoreDiameterMmCylindrical(double aFreeTargetM2, double blockageFractionOfAnnulus)
    {
        double phi = Math.Clamp(blockageFractionOfAnnulus, 0.0, 0.92);
        double denom = Math.Max(1.0 - phi, 0.08);
        double aBore = aFreeTargetM2 / denom;
        return 2.0 * 1000.0 * Math.Sqrt(Math.Max(aBore, Eps) / Math.PI);
    }

    /// <summary>
    /// Full derived-diameter pass: continuity → annulus + hub iteration → A_inj/A_bore cap → max-D cap → optional vortex floor.
    /// </summary>
    public static SizingDiagnostics ComputeDerived(
        SourceInputs source,
        NozzleDesignInputs template,
        double targetEntrainmentRatio,
        RunConfiguration run,
        DiameterMode reportMode = DiameterMode.EntrainmentDerived)
    {
        var warnings = new List<string>(4);
        double mdotCore = Math.Max(source.MassFlowKgPerSec, 1e-9);
        double er = Math.Clamp(targetEntrainmentRatio, 0.05, 1.5);
        double mdotAmb = mdotCore * er;
        double mdotMix = mdotCore + mdotAmb;

        double rhoMix = run.ChamberSizingRhoMixKgPerM3 > 0.5
            ? run.ChamberSizingRhoMixKgPerM3
            : EstimateRhoMixKgPerM3(source);

        double vAxial = Math.Clamp(run.ChamberSizingTargetAxialVelocityMps, 8.0, 220.0);
        double aFree = ComputeAFreeTargetM2(mdotMix, rhoMix, vAxial);

        double phi = Math.Clamp(run.ChamberVaneBlockageFractionOfAnnulus, 0.0, 0.92);

        double aSourceMm2 = Math.Max(source.SourceOutletAreaMm2, 1.0);
        double dJetMm = 2.0 * Math.Sqrt(aSourceMm2 / Math.PI);
        double aInjMm2 = Math.Max(template.TotalInjectorAreaMm2, 1.0);

        // Hub: use template if set; else couple to bore via synthesis rule 0.28×D (iterate with annulus formula).
        double hubMm = template.StatorHubDiameterMm > 0.5
            ? template.StatorHubDiameterMm
            : 0.28 * SolveBoreDiameterMmCylindrical(aFree, phi);

        int annulusIters = 0;
        double dMm = SolveBoreDiameterMmForAnnulus(aFree, hubMm, phi);
        for (int iter = 0; iter < 4; iter++)
        {
            annulusIters++;
            double hubNext = template.StatorHubDiameterMm > 0.5
                ? template.StatorHubDiameterMm
                : Math.Clamp(0.28 * dMm, 4.0, 0.92 * dMm);
            if (Math.Abs(hubNext - hubMm) < 0.02)
                break;
            hubMm = hubNext;
            dMm = SolveBoreDiameterMmForAnnulus(aFree, hubMm, phi);
        }

        double dAfterContinuity = dMm;

        // Injector / bore area: increase D until A_inj/A_bore <= preferred (configurable).
        double pref = Math.Clamp(run.ChamberSizingInjToChamberPreferredMax, 0.35, 0.92);
        int injIters = 0;
        while (injIters < 48)
        {
            double aBoreMm2 = Math.PI * 0.25 * dMm * dMm;
            double ratio = aInjMm2 / Math.Max(aBoreMm2, Eps);
            if (ratio <= pref + 1e-6)
                break;
            // Need D such that A_inj / (π D²/4) <= pref → D >= sqrt(4 A_inj / (π pref))
            double dMinInj = 2.0 * Math.Sqrt(aInjMm2 / (Math.PI * Math.Max(pref, 0.08)));
            dMm = Math.Max(dMm, dMinInj);
            injIters++;
        }

        double dAfterInjector = dMm;
        if (dAfterInjector > dAfterContinuity + 0.15)
        {
            warnings.Add(
                $"A_inj/A_bore preferred limit (≤{pref:F2}) enlarged bore vs continuity-only annulus solve: D_cont ≈ {dAfterContinuity:F2} mm → D_after_inj_floor ≈ {dAfterInjector:F2} mm (not CFD).");
        }

        // Vortex / compactness: do not exceed max multiplier vs jet; optional floor from swirl.
        double yaw = template.InjectorYawAngleDeg;
        double pitch = template.InjectorPitchAngleDeg;
        var (vt, va) = SwirlMath.ResolveInjectorComponents(
            source.SourceVelocityMps > 1.0
                ? source.SourceVelocityMps
                : VelocityMath.FromMassFlow(mdotCore, source.AmbientDensityKgPerM3, aSourceMm2 / 1e6),
            yaw,
            pitch);
        double s = SwirlMath.InjectorSwirlNumber(vt, va);
        double dBeforeCap = dMm;
        double dMax = dJetMm * Math.Max(run.DerivedChamberMaxDiameterMultiplierVsJet, 1.05);
        bool cappedByMax = false;
        if (dMm > dMax)
        {
            cappedByMax = true;
            warnings.Add(
                $"Derived chamber diameter capped by DerivedChamberMaxDiameterMultiplierVsJet (D {dMm:F1} mm → {dMax:F1} mm). Entrainment target may not be achievable at stated V_axial without relaxing caps — validate in SI/CFD.");
            dMm = dMax;
        }

        double dMinVortex = dJetMm * Math.Clamp(run.DerivedChamberMinDiameterMultiplierVsJet + 0.12 * Math.Tanh(s * 0.5), 0.75, 1.15);
        dMm = Math.Max(dMm, dMinVortex);

        // Port lip: chamber must fit injectors (same as synthesis).
        const double injectorToBoreAreaMargin = 1.06;
        double dMinPorts = 2.0 * Math.Sqrt((aInjMm2 * injectorToBoreAreaMargin) / Math.PI);
        if (dMm < dMinPorts - 1e-3)
            warnings.Add($"Port lip / injector margin required D ≥ {dMinPorts:F2} mm (raised bore from {dMm:F2} mm).");
        dMm = Math.Max(dMm, dMinPorts);

        double dBeforeGlobalClamp = dMm;
        dMm = Math.Clamp(dMm, 35.0, 260.0);
        if (Math.Abs(dMm - dBeforeGlobalClamp) > 0.05)
            warnings.Add($"Global bore clamp [35, 260] mm adjusted diameter from {dBeforeGlobalClamp:F2} mm to {dMm:F2} mm.");

        double aBoreFinalMm2 = Math.PI * 0.25 * dMm * dMm;
        double injRatio = aInjMm2 / Math.Max(aBoreFinalMm2, Eps);

        double warnTh = Math.Clamp(run.ChamberSizingInjToChamberWarning, pref + 0.02, 0.99);
        double sevTh = Math.Clamp(run.ChamberSizingInjToChamberSevere, warnTh + 0.02, 0.999);
        if (injRatio > sevTh)
            warnings.Add($"SEVERE: A_inj/A_bore = {injRatio:F3} > severe threshold {sevTh:F2} — port blockage likely dominates bore; increase D or reduce injector area.");
        else if (injRatio > warnTh)
            warnings.Add($"WARNING: A_inj/A_bore = {injRatio:F3} > warning threshold {warnTh:F2}.");

        return new SizingDiagnostics
        {
            Mode = reportMode,
            TargetEntrainmentRatio = er,
            MdotCoreKgS = mdotCore,
            MdotAmbientTargetKgS = mdotAmb,
            MdotMixTargetKgS = mdotMix,
            RhoMixEstimateKgPerM3 = rhoMix,
            TargetAxialVelocityMps = vAxial,
            AFreeTargetM2 = aFree,
            BlockageFractionOfAnnulus = phi,
            HubDiameterMm = hubMm,
            TotalInjectorAreaMm2 = aInjMm2,
            ChamberDiameterTargetMm = dMm,
            InjectorToBoreAreaRatio = injRatio,
            DerivedChamberMaxDiameterMm = dMax,
            AnnulusIterationsUsed = annulusIters,
            InjectorConstraintIterations = injIters,
            DiameterMmAfterContinuityAnnulus = dAfterContinuity,
            DiameterMmAfterInjectorPreferredFloor = dAfterInjector,
            DiameterMmBeforeGlobalClamp = dBeforeGlobalClamp,
            WasReducedByMaxJetDiameterCap = cappedByMax,
            Warnings = warnings
        };
    }

    /// <summary>Heuristic mixed density if user did not set <see cref="RunConfiguration.ChamberSizingRhoMixKgPerM3"/>.</summary>
    public static double EstimateRhoMixKgPerM3(SourceInputs source)
    {
        double rhoAmb = Math.Max(source.AmbientDensityKgPerM3, 0.5);
        // Light weighting toward ambient for entrained stream (first-order).
        double rhoCore = rhoAmb;
        if (source.ExhaustTemperatureK is { } tEx && tEx > 200.0)
        {
            double p = Math.Max(source.AmbientPressurePa, 1000.0);
            double rGas = 287.0;
            rhoCore = p / (rGas * Math.Max(tEx, 250.0));
            rhoCore = Math.Clamp(rhoCore, 0.2, 25.0);
        }

        return Math.Clamp(0.55 * rhoAmb + 0.45 * rhoCore, 0.4, 20.0);
    }

    public static SizingDiagnostics ForHeuristicMode(
        double chamberDiameterMm,
        double targetEr,
        SourceInputs source,
        NozzleDesignInputs template,
        RunConfiguration run)
    {
        double mdotCore = Math.Max(source.MassFlowKgPerSec, 1e-9);
        double phi = Math.Clamp(run.ChamberVaneBlockageFractionOfAnnulus, 0.0, 0.92);
        return new SizingDiagnostics
        {
            Mode = DiameterMode.SynthesisHeuristic,
            TargetEntrainmentRatio = targetEr,
            MdotCoreKgS = mdotCore,
            MdotAmbientTargetKgS = mdotCore * targetEr,
            MdotMixTargetKgS = mdotCore * (1.0 + targetEr),
            RhoMixEstimateKgPerM3 = EstimateRhoMixKgPerM3(source),
            TargetAxialVelocityMps = run.ChamberSizingTargetAxialVelocityMps,
            AFreeTargetM2 = 0,
            BlockageFractionOfAnnulus = phi,
            HubDiameterMm = template.StatorHubDiameterMm,
            TotalInjectorAreaMm2 = template.TotalInjectorAreaMm2,
            ChamberDiameterTargetMm = chamberDiameterMm,
            InjectorToBoreAreaRatio = template.TotalInjectorAreaMm2 / Math.Max(Math.PI * 0.25 * chamberDiameterMm * chamberDiameterMm, Eps),
            DerivedChamberMaxDiameterMm = 0,
            AnnulusIterationsUsed = 0,
            InjectorConstraintIterations = 0,
            DiameterMmAfterContinuityAnnulus = 0,
            DiameterMmAfterInjectorPreferredFloor = 0,
            DiameterMmBeforeGlobalClamp = chamberDiameterMm,
            WasReducedByMaxJetDiameterCap = false,
            Warnings = Array.Empty<string>()
        };
    }

    public static SizingDiagnostics ForUserTemplate(NozzleDesignInputs template, SourceInputs source, RunConfiguration run)
    {
        double d = template.SwirlChamberDiameterMm;
        double phi = Math.Clamp(run.ChamberVaneBlockageFractionOfAnnulus, 0.0, 0.92);
        return new SizingDiagnostics
        {
            Mode = DiameterMode.UserTemplate,
            TargetEntrainmentRatio = 0,
            MdotCoreKgS = Math.Max(source.MassFlowKgPerSec, 0),
            MdotAmbientTargetKgS = 0,
            MdotMixTargetKgS = 0,
            RhoMixEstimateKgPerM3 = 0,
            TargetAxialVelocityMps = 0,
            AFreeTargetM2 = 0,
            BlockageFractionOfAnnulus = phi,
            HubDiameterMm = template.StatorHubDiameterMm,
            TotalInjectorAreaMm2 = template.TotalInjectorAreaMm2,
            ChamberDiameterTargetMm = d,
            InjectorToBoreAreaRatio = template.TotalInjectorAreaMm2 / Math.Max(Math.PI * 0.25 * d * d, Eps),
            DerivedChamberMaxDiameterMm = 0,
            AnnulusIterationsUsed = 0,
            InjectorConstraintIterations = 0,
            DiameterMmAfterContinuityAnnulus = 0,
            DiameterMmAfterInjectorPreferredFloor = 0,
            DiameterMmBeforeGlobalClamp = d,
            WasReducedByMaxJetDiameterCap = false,
            Warnings = Array.Empty<string>()
        };
    }
}
