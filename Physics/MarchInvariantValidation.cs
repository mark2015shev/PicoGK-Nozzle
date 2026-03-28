using System;
using System.Collections.Generic;

namespace PicoGK_Run.Physics;

/// <summary>Optional debug checks on <see cref="FlowStepState"/> after each march step.</summary>
public static class MarchInvariantValidation
{
    public static IReadOnlyList<string> CollectStep(FlowStepState s, GasProperties gas, int stepIndexOneBased)
    {
        var sink = new List<string>();
        ValidateStep(s, gas, stepIndexOneBased, sink);
        return sink;
    }

    public static void ValidateStep(
        FlowStepState s,
        GasProperties gas,
        int stepIndexOneBased,
        IList<string> sink)
    {
        double mp = s.MdotPrimaryKgS;
        double ms = s.MdotSecondaryKgS;
        double mt = s.MdotTotalKgS;
        if (mt > 1e-18)
        {
            double sum = mp + ms;
            double rel = Math.Abs(mt - sum) / mt;
            if (rel > MarchInvariantTolerances.MassSplitRelativeTolerance)
                sink.Add(
                    $"March inv step {stepIndexOneBased}: mdot_total ({mt:E}) vs mdot_p+mdot_s ({sum:E}), rel_err={rel:E3}.");
        }

        double rIdeal = s.PStaticPa / (GasProperties.R * Math.Max(s.TStaticK, 1.0));
        if (s.DensityKgM3 > 1e-18 && rIdeal > 1e-18)
        {
            double relRho = Math.Abs(s.DensityKgM3 - rIdeal) / s.DensityKgM3;
            if (relRho > MarchInvariantTolerances.IdealGasRelativeTolerance)
                sink.Add(
                    $"March inv step {stepIndexOneBased}: rho ({s.DensityKgM3:F5}) vs P/(RT) ({rIdeal:F5}), rel_err={relRho:F3}.");
        }

        double mFlux = s.DensityKgM3 * s.AreaM2 * s.VAxialMps;
        if (mt > 1e-18)
        {
            double relC = Math.Abs(mFlux - mt) / mt;
            if (relC > MarchInvariantTolerances.ContinuityRelativeTolerance)
                sink.Add(
                    $"March inv step {stepIndexOneBased}: continuity |rho*A*Va - mdot|/mdot = {relC:F3}.");
        }

        if (s.Mach > MarchInvariantTolerances.MachSanityCeiling || s.Mach < 0.0 || double.IsNaN(s.Mach))
            sink.Add($"March inv step {stepIndexOneBased}: Mach={s.Mach:F4} outside sanity [0, {MarchInvariantTolerances.MachSanityCeiling}].");

        if (s.PTotalPa > 1.0 && s.PStaticPa > s.PTotalPa * (1.0 + ChamberAerodynamicsConfiguration.StaticMustNotExceedTotalRelativeTolerance))
            sink.Add(
                $"March inv step {stepIndexOneBased}: P_static ({s.PStaticPa:F1} Pa) exceeds P_total ({s.PTotalPa:F1} Pa) beyond tolerance.");

        if (s.TotalPressureAfterLossesPa > 1.0
            && s.PStaticPa > s.TotalPressureAfterLossesPa * (1.0 + ChamberAerodynamicsConfiguration.StaticMustNotExceedTotalRelativeTolerance))
            sink.Add(
                $"March inv step {stepIndexOneBased}: P_static exceeds P₀ after step losses ({s.TotalPressureAfterLossesPa:F1} Pa) — bulk thermo inconsistent.");

        if (s.WallPressurePa > s.TotalPressureAfterLossesPa * (1.0 + 2.0 * ChamberAerodynamicsConfiguration.WallStaticExcessOverBulkMaxFractionOfP0)
            && s.TotalPressureAfterLossesPa > 1.0)
            sink.Add(
                $"March inv step {stepIndexOneBased}: P_wall ({s.WallPressurePa:F1} Pa) exceeds step P₀ ceiling band vs bulk.");

        if (!s.StepBulkPressureValid)
            sink.Add($"March inv step {stepIndexOneBased}: StepBulkPressureValid=false (chamber bulk/radial guard).");
    }
}
