using System.Collections.Generic;
using System.Globalization;

namespace PicoGK_Run.Physics.Continuous;

/// <summary>Human-readable station tables for comparing reduced-order predictions to taps / CFD / thrust stand.</summary>
public static class ContinuousSolverDiagnosticsWriter
{
    public static IReadOnlyList<string> FormatStationTableLines(ContinuousNozzleSolution sol)
    {
        var lines = new List<string>
        {
            "--- Continuous reduced-order path (full nozzle stations, not CFD) ---",
            "x_mm segment ID_mm A_mm2 Dh_mm alpha_deg mdot_c mdot_e mdot p_s(bar) p_t(bar) T_s(K) rho Vx Vt Mach Re dPrad p_wall dFwall Fwall_cum",
            "---"
        };
        foreach (ContinuousPathStation row in sol.Stations)
        {
            GeometryStation g = row.Geometry;
            FlowState f = row.Flow;
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "{0:F2} {1,-5} {2:F2} {3:F2} {4:F2} {5:F4} {6:F4} {7:F4} {8:F4} {9:F4} {10:F2} {11:F3} {12:F2} {13:F2} {14:F3} {15:F0} {16:F2} {17:F4} {18:F4} {19:F4}",
                f.XMm,
                SegmentAbbr(f.Segment),
                2.0 * g.OuterGasRadiusMm,
                g.FlowAreaM2 * 1e6,
                g.HydraulicDiameterM * 1000.0,
                g.WallHalfAngleDeg,
                f.MassFlowCoreKgS,
                f.MassFlowEntrainedKgS,
                f.MassFlowTotalKgS,
                f.StaticPressurePa / 100_000.0,
                f.TotalPressurePa / 100_000.0,
                f.StaticTemperatureK,
                f.DensityKgM3,
                f.VAxialMps,
                f.VTangentialMps,
                f.Mach,
                f.Reynolds,
                f.DeltaPRadialPa / 100_000.0,
                f.WallPressurePa / 100_000.0,
                f.AxialWallForceIncrementN,
                f.CumulativeWallForceN));
        }

        return lines;
    }

    public static IReadOnlyList<string> FormatSummaryLines(ContinuousNozzleSolution sol)
    {
        var lines = new List<string> { "--- Continuous nozzle path summary ---" };
        lines.AddRange(sol.SegmentBoundaryAuditLines);
        lines.AddRange(sol.Energy.FormatSummaryLines());
        lines.AddRange(ReducedOrderClosureCoefficients.FormatCalibrationLedger());
        lines.Add("--- End continuous path ---");
        return lines;
    }

    private static string SegmentAbbr(NozzleSegmentKind k) => k switch
    {
        NozzleSegmentKind.InletCoupled => "inlet",
        NozzleSegmentKind.SwirlChamber => "chmbr",
        NozzleSegmentKind.Expander => "expnd",
        NozzleSegmentKind.Stator => "stator",
        NozzleSegmentKind.Exit => "exit",
        _ => "?"
    };
}
