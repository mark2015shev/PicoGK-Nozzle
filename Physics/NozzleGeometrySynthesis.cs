using System;
using System.Collections.Generic;
using PicoGK_Run.Core;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Physics;

/// <summary>
/// Builds a <see cref="NozzleDesignInputs"/> from turbine boundary data + a template (injector count, wall, etc.).
/// <para>
/// <b>Intent:</b> move key diameters and angles off “hand tuning” onto <b>documented first-order rules</b>
/// (jet size, swirl number, target entrainment, gentle expander, stator alignment). This is <b>not</b> CFD and
/// <b>not</b> a global numerical optimizer — it is an engineering synthesizer you can replace with search/CFD later.
/// </para>
/// <para>
/// Optional <see cref="RunConfiguration.UseDerivedSwirlChamberDiameter"/> sizes bore from target ṁ_amb/ṁ_core and a
/// nominal chamber axial velocity (<see cref="SwirlChamberSizingModel"/>), instead of a fixed jet×scale heuristic.
/// </para>
/// </summary>
public static class NozzleGeometrySynthesis
{
    /// <summary>Default target secondary/core mass-flow ratio for sizing passages (model-scale).</summary>
    public const double DefaultTargetEntrainmentRatio = 0.42;

    /// <summary>Design plus audit of how chamber diameter was chosen.</summary>
    public readonly record struct GeometrySynthesisResult(
        NozzleDesignInputs Design,
        SwirlChamberSizingModel.SizingDiagnostics ChamberSizing,
        IReadOnlyList<string>? VortexEntrainmentHints = null);

    /// <summary>
    /// Overwrites template fields that should follow physics; keeps injector count, wall thickness, roll, slot aspect when sensible.
    /// </summary>
    public static NozzleDesignInputs Synthesize(
        SourceInputs source,
        NozzleDesignInputs template,
        double targetEntrainmentRatio = DefaultTargetEntrainmentRatio,
        RunConfiguration? run = null) =>
        SynthesizeWithDiagnostics(source, template, targetEntrainmentRatio, run).Design;

    /// <summary>Same as <see cref="Synthesize"/> but returns sizing diagnostics for reporting.</summary>
    public static GeometrySynthesisResult SynthesizeWithDiagnostics(
        SourceInputs source,
        NozzleDesignInputs template,
        double targetEntrainmentRatio,
        RunConfiguration? run)
    {
        targetEntrainmentRatio = Math.Clamp(targetEntrainmentRatio, 0.05, 1.2);
        RunConfiguration runEff = run ?? new RunConfiguration();
        List<string> vortexHints = new();

        double aSourceMm2 = Math.Max(source.SourceOutletAreaMm2, 1.0);
        double dJetMm = 2.0 * Math.Sqrt(aSourceMm2 / Math.PI);
        double mdot = Math.Max(source.MassFlowKgPerSec, 1e-6);
        double vCore = source.SourceVelocityMps > 1.0
            ? source.SourceVelocityMps
            : VelocityMath.FromMassFlow(mdot, source.AmbientDensityKgPerM3, aSourceMm2 / 1e6);

        double yaw = runEff.LockInjectorYawTo90Degrees ? 90.0 : template.InjectorYawAngleDeg;
        double pitch = template.InjectorPitchAngleDeg;
        var (vt, va) = SwirlMath.ResolveInjectorComponents(vCore, yaw, pitch);
        double rFluxM = 0.5e-3 * Math.Max(template.SwirlChamberDiameterMm, 1.0);
        double fluxS = SwirlMath.FluxSwirlNumber(
            mdot * rFluxM * vt,
            mdot * Math.Max(Math.Abs(va), 1e-3),
            rFluxM);
        double swirlGeomMetric = Math.Clamp(Math.Abs(fluxS), 0.25, 12.0);

        int nInj = Math.Max(1, template.InjectorCount);
        double totalInjArea = Math.Min(aSourceMm2 * 0.995, Math.Max(template.TotalInjectorAreaMm2, aSourceMm2 * 0.85));
        const double injectorToBoreAreaMargin = 1.06;
        double dMinForPortsMm = 2.0 * Math.Sqrt((totalInjArea * injectorToBoreAreaMargin) / Math.PI);

        // Chamber bore: continuity-based annulus sizing only (A_eff ≥ ṁ_mix/(ρ V_ax); hub + blockage inside model).
        SwirlChamberSizingModel.SizingDiagnostics sizingDiag =
            SwirlChamberSizingModel.ComputeDerived(source, template, targetEntrainmentRatio, runEff);
        double dChamberMm = Math.Max(sizingDiag.ChamberDiameterTargetMm, dMinForPortsMm);

        // --- Chamber length: shorter envelope by default (compact vortex volume); still scales with D and ER.
        double lambda = 0.92 + 0.14 * (1.0 - 1.0 / (1.0 + targetEntrainmentRatio));
        lambda = Math.Clamp(lambda, 0.72, 1.12);
        double lenCap = Math.Clamp(runEff.AutotuneSwirlChamberLengthMaxMm, 40.0, 220.0);
        double lChamberMm = Math.Clamp(lambda * dChamberMm, 28.0, lenCap);
        lChamberMm = Math.Max(lChamberMm, Math.Min(95.0, 0.98 * dChamberMm));

        if (runEff.DerivedChamberTargetMinLd > 0.1
            && runEff.DerivedChamberTargetMaxLd > runEff.DerivedChamberTargetMinLd)
        {
            double ld0 = lChamberMm / Math.Max(dChamberMm, 1e-6);
            if (ld0 < runEff.DerivedChamberTargetMinLd)
                lChamberMm = Math.Min(lenCap, dChamberMm * runEff.DerivedChamberTargetMinLd);
            double ld1 = lChamberMm / Math.Max(dChamberMm, 1e-6);
            if (ld1 > runEff.DerivedChamberTargetMaxLd)
                lChamberMm = Math.Max(28.0, dChamberMm * runEff.DerivedChamberTargetMaxLd);
        }

        // --- Ejector Rule of 6: mixing length ≥ 6× primary jet equivalent diameter (tangential → axial turnover).
        if (runEff.EnforceEjectorMixingRuleOfSix)
        {
            double lMinRo6 = VortexEntrainmentPhysics.MixingLengthMinimumMmRuleOfSix(totalInjArea);
            if (lChamberMm + 1e-6 < lMinRo6)
            {
                double lPrev = lChamberMm;
                lChamberMm = Math.Min(lenCap, Math.Max(lChamberMm, lMinRo6));
                if (Math.Abs(lChamberMm - lPrev) > 0.5)
                {
                    vortexHints.Add(
                        $"VORTEX ENTRAINMENT: Rule-of-6 — SwirlChamberLengthMm {lPrev:F1} → {lChamberMm:F1} mm (≥ 6× injector equivalent ⌀ {lMinRo6 / VortexEntrainmentPhysics.EjectorMixingLengthToJetDiameterRatio:F1} mm).");
                }
            }
        }

        // --- Inlet: capture openness σ = (D_in/D_ch)² ; favor σ > 1 for ambient mouth ≥ bore (bellmouth rule in geometry).
        double sigma = 1.22 + 0.28 * targetEntrainmentRatio - 0.06 * Math.Tanh(swirlGeomMetric * 0.35);
        sigma = Math.Clamp(sigma, 1.05, 2.0);
        double dInletMm = Math.Sqrt(sigma) * dChamberMm;
        dInletMm = Math.Clamp(dInletMm, dChamberMm * 1.01, dChamberMm * 1.55);

        // --- Exit: area scales with (1+ER) and axial speed floor (bulk continuity hint, not Navier–Stokes).
        double vAxialHint = Math.Max(85.0, vCore / (1.35 + 0.22 * swirlGeomMetric));
        double areaScale = (1.0 + targetEntrainmentRatio) * (vCore / Math.Max(vAxialHint, 40.0));
        areaScale = Math.Clamp(areaScale, 1.12, 2.0);
        double dExitMm = Math.Clamp(dChamberMm * Math.Sqrt(areaScale * 0.92), dChamberMm * 1.05, dChamberMm * 1.62);

        // --- Expander: higher swirl → shallower cone to limit separation risk on rotating flow.
        double halfAngleDeg = 14.5 / (1.0 + 0.28 * swirlGeomMetric) - 0.35 * Math.Tanh(targetEntrainmentRatio - 0.35);
        halfAngleDeg = Math.Clamp(halfAngleDeg, 4.5, 11.0);
        double rCh = 0.5 * dChamberMm;
        double rExit = 0.5 * dExitMm;
        double halfRad = halfAngleDeg * (Math.PI / 180.0);
        double tanA = Math.Tan(halfRad);
        double lExpanderMm = tanA > 1e-4
            ? (rExit - rCh) / tanA
            : 80.0;
        lExpanderMm = Math.Clamp(lExpanderMm, 45.0, 200.0);

        // If geometry asks for contraction in cone (r_exit < r_ch), flatten angle / shorten — cone must diverge for this concept.
        if (rExit <= rCh + 0.5)
        {
            rExit = rCh + 8.0;
            dExitMm = 2.0 * rExit;
            lExpanderMm = Math.Clamp((rExit - rCh) / Math.Max(tanA, 0.06), 40.0, 180.0);
        }

        // --- Stator: first-order alignment — turn toward axial; scale with injector yaw and mild L/D factor.
        double ldCh = lChamberMm / Math.Max(dChamberMm, 1e-6);
        double statorDeg = yaw * (0.32 + 0.04 * Math.Tanh(ldCh - 1.0)) + 6.0 * Math.Tanh(swirlGeomMetric * 0.25);
        statorDeg = Math.Clamp(statorDeg, 18.0, 52.0);

        double slotH = Math.Max(template.InjectorHeightMm, 1.0);
        double slotW = totalInjArea / (nInj * slotH);

        var design = new NozzleDesignInputs
        {
            InletDiameterMm = dInletMm,
            SwirlChamberDiameterMm = dChamberMm,
            SwirlChamberLengthMm = lChamberMm,
            InjectorAxialPositionRatio = template.InjectorAxialPositionRatio,
            InjectorUpstreamGuardLengthMm = template.InjectorUpstreamGuardLengthMm,
            TotalInjectorAreaMm2 = totalInjArea,
            InjectorCount = template.InjectorCount,
            InjectorWidthMm = Math.Max(2.0, slotW),
            InjectorHeightMm = slotH,
            InjectorYawAngleDeg = template.InjectorYawAngleDeg,
            InjectorPitchAngleDeg = template.InjectorPitchAngleDeg,
            InjectorRollAngleDeg = template.InjectorRollAngleDeg,
            ExpanderLengthMm = lExpanderMm,
            ExpanderHalfAngleDeg = halfAngleDeg,
            ExitDiameterMm = dExitMm,
            StatorVaneAngleDeg = statorDeg,
            StatorVaneCount = Math.Max(8, template.StatorVaneCount),
            StatorHubDiameterMm = template.StatorHubDiameterMm > 0.5
                ? template.StatorHubDiameterMm
                : 0.28 * dChamberMm,
            StatorAxialLengthMm = template.StatorAxialLengthMm > 1.0
                ? template.StatorAxialLengthMm
                : Math.Max(14.0, 0.11 * dChamberMm),
            StatorBladeChordMm = template.StatorBladeChordMm > 0.5
                ? template.StatorBladeChordMm
                : Math.Max(4.0, 0.13 * dChamberMm),
            WallThicknessMm = template.WallThicknessMm
        };

        double throatAM2 = Math.PI * Math.Pow(0.5e-3 * dChamberMm, 2);
        double exitAM2 = Math.PI * Math.Pow(0.5e-3 * dExitMm, 2);
        double erAreaModel = VortexEntrainmentPhysics.CalculateEntrainmentRatio(throatAM2, exitAM2, halfRad);
        vortexHints.Add(
            $"VORTEX ENTRAINMENT: Jet-area ER estimate = {erAreaModel:F3} (k·sin(θ)·√(A_ex/A_th), θ = expander half-angle); target ER = {targetEntrainmentRatio:F3}.");

        var (vtInj, vaInj) = SwirlMath.ResolveInjectorComponents(vCore, design.InjectorYawAngleDeg, design.InjectorPitchAngleDeg);
        if (!VortexEntrainmentPhysics.TryVerifyAxialDominatesSwirl(
                Math.Abs(vaInj),
                Math.Abs(vtInj),
                halfRad,
                out string? stallHint))
            vortexHints.Add("VORTEX ENTRAINMENT: " + stallHint!);

        return new GeometrySynthesisResult(
            design,
            sizingDiag,
            vortexHints.Count > 0 ? vortexHints : null);
    }
}
