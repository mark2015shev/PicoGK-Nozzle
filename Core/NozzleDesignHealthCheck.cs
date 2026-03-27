using System;
using System.Collections.Generic;
using PicoGK_Run.Geometry;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Core;

/// <summary>
/// First-order design checks before trusting geometry or SI results. Fails loudly with clear strings; not CFD.
/// </summary>
public static class NozzleDesignHealthCheck
{
    public static IReadOnlyList<string> Validate(
        NozzleDesignInputs d,
        NozzleCriticalRatiosSnapshot r,
        SiFlowDiagnostics? si)
    {
        var list = new List<string>();

        void Add(string s) => list.Add(s);

        if (d.InletDiameterMm <= 0) Add("DESIGN ERROR: InletDiameterMm must be positive.");
        if (d.SwirlChamberDiameterMm <= 0) Add("DESIGN ERROR: SwirlChamberDiameterMm must be positive.");
        if (d.SwirlChamberLengthMm <= 0) Add("DESIGN ERROR: SwirlChamberLengthMm must be positive.");
        if (d.ExpanderLengthMm <= 0) Add("DESIGN ERROR: ExpanderLengthMm must be positive.");
        if (d.ExitDiameterMm <= 0) Add("DESIGN ERROR: ExitDiameterMm must be positive.");
        if (d.WallThicknessMm <= 0) Add("DESIGN ERROR: WallThicknessMm must be positive.");
        if (d.InjectorCount < 1) Add("DESIGN ERROR: InjectorCount must be at least 1.");

        double rSt = NozzleGeometryMetrics.ExpanderEndInnerRadiusMm(d);
        double hubD = d.StatorHubDiameterMm > 0.5 ? d.StatorHubDiameterMm : 0.28 * d.SwirlChamberDiameterMm;
        if (hubD * 0.5 >= 0.92 * rSt)
            Add($"STATOR HUB: hub radius ({0.5 * hubD:F1} mm) is large vs stator casing inner R ({rSt:F1} mm) — span may be tiny; check StatorHubDiameterMm vs expander exit.");

        double slotMm2 = d.InjectorWidthMm * d.InjectorHeightMm * Math.Max(d.InjectorCount, 1);
        if (d.TotalInjectorAreaMm2 > 1e-6 && slotMm2 > 1e-6)
        {
            double rel = Math.Abs(d.TotalInjectorAreaMm2 - slotMm2) / d.TotalInjectorAreaMm2;
            if (rel > 0.08)
                Add($"INJECTOR AREA: TotalInjectorAreaMm2 ({d.TotalInjectorAreaMm2:F1}) differs from Count×W×H ({slotMm2:F1}) by {rel:P0} — check slot vs total area intent.");
        }

        // R1
        if (r.CaptureToChamberAreaRatio < 0.35)
            Add($"R1 CAPTURE (σ=A_in/A_ch={r.CaptureToChamberAreaRatio:F2}): inlet is small vs chamber — entrainment capture may be starved unless geometry lifts entrance ID.");
        if (r.CaptureToChamberAreaRatio > 2.5)
            Add($"R1 CAPTURE (σ={r.CaptureToChamberAreaRatio:F2}): inlet >> chamber on paper — verify lip/flare and wall thickness; capture rule may force cylindrical entrance.");

        // R2 — governing flux S from SI; skip template |Vt|/|Va| for ~90° tangential injectors (ratio is non-physical).
        if (r.InjectorPlaneFluxSwirlNumber is { } sFlux)
        {
            if (sFlux < 0.12)
                Add($"R2 FLUX SWIRL (S={sFlux:F3}): low Ġθ/(R·ġx) at injector — weak swirl transport in the SI model.");
            if (sFlux > 18.0)
                Add($"R2 FLUX SWIRL (S={sFlux:F3}): very high — check entrainment caps and breakdown risk in diagnostics.");
        }
        else if (Math.Abs(d.InjectorYawAngleDeg - 90.0) > 8.0)
        {
            if (r.InjectorSwirlNumber < 0.35)
                Add($"R2 SWIRL (diagnostic |Vt|/|Va|={r.InjectorSwirlNumber:F2}): low — weak tangential component vs axial at template yaw.");
            if (r.InjectorSwirlNumber > 5.0)
                Add($"R2 SWIRL (diagnostic |Vt|/|Va|={r.InjectorSwirlNumber:F2}): very high — check yaw/pitch.");
        }

        // R3
        if (r.ChamberSlendernessLD < 0.45)
            Add($"R3 L/D (Λ={r.ChamberSlendernessLD:F2}): very short chamber vs diameter — mixing and entrainment length may be insufficient.");
        if (r.ChamberSlendernessLD > 2.8)
            Add($"R3 L/D (Λ={r.ChamberSlendernessLD:F2}): long chamber — swirl decay and losses rise; confirm intent.");

        // R4a expander angle
        if (r.ExpanderHalfAngleDeg > 11.0)
            Add($"R4 EXPANDER: half-angle {r.ExpanderHalfAngleDeg:F1}° is aggressive for internal swirling flow — separation risk (validate experimentally or CFD).");
        if (r.ExpanderHalfAngleDeg > 14.0)
            Add($"R4 EXPANDER: half-angle {r.ExpanderHalfAngleDeg:F1}° is very high — strongly consider reducing angle or shortening length.");

        // R4b geometry consistency
        if (r.ExpanderExitToTargetRadiusMismatchRatio > 0.55)
            Add($"R4 SIZING: expander cone ends at R={r.ExpanderEndInnerRadiusMm:F1} mm vs exit target R={r.ExitTargetInnerRadiusMm:F1} mm (mismatch / R_ch = {r.ExpanderExitToTargetRadiusMismatchRatio:F2}) — exit taper carries large area change; confirm one expander mode (angle vs end radius) is authoritative.");

        // R4c stator vs injector
        if (r.StatorToInjectorYawMismatchDeg > 55.0)
            Add($"R4 STATOR: |stator vane − injector yaw| = {r.StatorToInjectorYawMismatchDeg:F1}° — poor alignment may waste swirl recovery (first-order check only).");

        // Solved flow
        if (si != null)
        {
            if (si.AnyEntrainmentStepChoked)
                Add("SI FLOW: at least one entrainment step hit choked intake — model capped ambient pull; real hardware may differ.");
            if (si.SumRequestedEntrainmentIncrementsKgS > 1e-9)
            {
                double sf = si.EntrainmentShortfallSumKgS / si.SumRequestedEntrainmentIncrementsKgS;
                if (sf > 0.12)
                    Add($"SI FLOW: entrainment shortfall {sf:P0} of requested (sum of steps) — suction / capture area may be undersized in the model.");
            }
        }

        if (r.SolvedEntrainmentRatio is { } er && er < 0.05)
            Add($"SOLVED ENTRAINMENT RATIO ṁ_amb/ṁ_core = {er:F3} is very low — concept relies on secondary mass flow; review capture, swirl, and correlation.");

        return list;
    }
}
