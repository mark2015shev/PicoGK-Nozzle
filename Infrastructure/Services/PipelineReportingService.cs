using System.Collections.Generic;
using PicoGK;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;
using PicoGK_Run.Physics.Continuous;
using PicoGK_Run.Physics.Reports;

namespace PicoGK_Run.Infrastructure.Services;

/// <summary>Console formatting for SI / march summaries (no computational side effects on inputs).</summary>
internal static class PipelineReportingService
{
    public static void PrintSiFlowSummary(
        JetState inlet,
        NozzleDesignResult d,
        SiFlowDiagnostics si,
        SiVerbosityLevel verbosity)
    {
        ConsoleStatusWriter.WriteLine("--- SI flow-driven nozzle summary (compressible entrainment path) ---", StatusLevel.Normal);
        ConsoleStatusWriter.WriteLine($"Inlet jet axial velocity [m/s]: {inlet.VelocityMps:F2}", StatusLevel.Normal);
        ConsoleStatusWriter.WriteLine($"Estimated exit velocity [m/s]: {d.EstimatedExitVelocityMps:F2}", StatusLevel.Normal);
        ConsoleStatusWriter.WriteLine($"Estimated total mass flow [kg/s]: {d.EstimatedTotalMassFlowKgS:F4}", StatusLevel.Normal);
        ConsoleStatusWriter.WriteLine($"Estimated thrust [N]:       {d.EstimatedThrustN:F2} (momentum + pressure CV terms, first-order)", StatusLevel.Normal);
        ConsoleStatusWriter.WriteLine(
            $"Min inlet static (entrain.) [Pa]: {si.MinInletLocalStaticPressurePa:F1} ({SiPressureGuards.PaToBar(si.MinInletLocalStaticPressurePa):F4} bar)",
            StatusLevel.Normal);
        ConsoleReportColor.WriteClassifiedLine(
            $"Max entrainment Mach [-]:   {si.MaxInletMach:F3}  Choked step: {si.AnyEntrainmentStepChoked}");
        ConsoleStatusWriter.WriteLine($"Suggested inlet radius [m]: {d.SuggestedInletRadiusM:F5}", StatusLevel.Normal);
        ConsoleStatusWriter.WriteLine($"Suggested outlet radius [m]: {d.SuggestedOutletRadiusM:F5}", StatusLevel.Normal);
        ConsoleStatusWriter.WriteLine($"Suggested mixing length [m]: {d.SuggestedMixingLengthM:F5}", StatusLevel.Normal);
        if (si.ChamberMarch != null && verbosity >= SiVerbosityLevel.Normal)
        {
            SwirlChamberMarchDiagnostics m = si.ChamberMarch;
            ConsoleStatusWriter.WriteLine("--- Swirl chamber SI march geometry (aligned with CAD bore/hub annulus) ---", StatusLevel.Normal);
            ConsoleStatusWriter.WriteLine(
                $"A_inlet {m.AInletMm2:F2} | A_chamber(bore) {m.AChamberBoreMm2:F2} | A_free_ch {m.AFreeChamberMm2:F2} | A_inj {m.AInjTotalMm2:F2} | A_exit {m.AExitMm2:F2} [mm2]",
                StatusLevel.Normal);
            ConsoleStatusWriter.WriteLine(
                $"Ratios A_in/A_ch {m.RatioInletToChamber:F3} | A_inj/A_ch {m.RatioInjToChamber:F3} | A_free/A_ch {m.RatioFreeToChamber:F3} | A_exit/A_ch {m.RatioExitToChamber:F3}",
                StatusLevel.Normal);
            ConsoleStatusWriter.WriteLine(
                $"η_mix,0 {m.EntrainmentCeBase:F4} | η_mix@step1 {m.EntrainmentCeAtFirstStep:F4} | ΔP_augment(capture) {m.CaptureStaticPressureDeficitAugmentationPa:F1} Pa",
                StatusLevel.Normal);
            ConsoleStatusWriter.WriteLine(
                $"A_capture {m.CaptureAreaM2:E4} m2 | P_ent {m.EntrainmentPerimeterM:F5} m | A_duct_eff {m.DuctEffectiveAreaM2:E4} m2 (constant along chamber march)",
                StatusLevel.Normal);
            foreach (string w in m.ValidationWarnings)
                ConsoleReportColor.WriteClassifiedLine(w);
            if (m.SwirlEntranceCapacityStations != null)
            {
                foreach (string line in m.SwirlEntranceCapacityStations.FormatReportLines())
                    ConsoleReportColor.WriteClassifiedLine(line);
            }

            if (m.ChamberDischargeSplit != null)
            {
                foreach (string line in m.ChamberDischargeSplit.FormatReportLines())
                    ConsoleReportColor.WriteClassifiedLine(line);
            }
        }

        if (si.ContinuousPath != null && verbosity >= SiVerbosityLevel.Normal)
        {
            foreach (string line in ContinuousSolverDiagnosticsWriter.FormatSummaryLines(si.ContinuousPath))
            {
                ConsoleStatusWriter.WriteLine(line, StatusLevel.Normal);
                try
                {
                    Library.Log(line);
                }
                catch
                {
                }
            }

            if (verbosity >= SiVerbosityLevel.High)
            {
                foreach (string line in ContinuousSolverDiagnosticsWriter.FormatStationTableLines(si.ContinuousPath))
                {
                    ConsoleStatusWriter.WriteLine(line, StatusLevel.Normal);
                    try
                    {
                        Library.Log(line);
                    }
                    catch
                    {
                    }
                }
            }
        }

        ConsoleStatusWriter.WriteLine("---------------------------------------------------------------------", StatusLevel.Normal);
    }
}
