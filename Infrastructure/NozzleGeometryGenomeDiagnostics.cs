using System.Collections.Generic;
using PicoGK_Run.Infrastructure.PhysicsAutotune;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Infrastructure;

/// <summary>Console / Library log lines for genome-based autotune (baseline vs winner, derived geometry, tiers).</summary>
public static class NozzleGeometryGenomeDiagnostics
{
    /// <summary>Tier A L∞ distance below this is treated as “winner ≈ baseline” for messaging.</summary>
    public const double TierANearBaselineThreshold = 0.012;

    public static IReadOnlyList<string> FormatAutotuneReport(
        NozzleGeometryGenome baseline,
        NozzleGeometryGenome winner,
        RunConfiguration run,
        CandidatePhysicsAutotuneResult? detail)
    {
        var lines = new List<string>
        {
            "=== NozzleGeometryGenome (physics autotune) ===",
            "Tiers: A = primary physics (always searched in stages A–C); B = stator axial / hub / vane count / chord; C = wall / lip / contraction (not searched here).",
            run.PhysicsAutotuneStageCUnlockTierB
                ? "Stage C: Tier A polish + Tier B perturbations (PhysicsAutotuneStageCUnlockTierB=true)."
                : "Stage C: Tier A polish only; Tier B held at template/seed (set PhysicsAutotuneStageCUnlockTierB to search Tier B).",
            $"Baseline → winner Tier A L∞ distance: {NozzleGeometryGenome.TierADistance(baseline, winner):F4}  (near-baseline if < {TierANearBaselineThreshold:F3})."
        };

        if (NozzleGeometryGenome.TierADistance(baseline, winner) < TierANearBaselineThreshold)
            lines.Add("Winner genome is effectively identical to baseline on Tier A (within threshold).");

        lines.Add("--- Baseline genome ---");
        lines.AddRange(FormatGenomeLines(baseline));
        lines.Add("--- Winner genome ---");
        lines.AddRange(FormatGenomeLines(winner));

        DerivedNozzleGeometryParameters derivedW = NozzleGeometryGenomeMapper.Derive(winner);
        lines.Add("--- Derived geometry (winner) [mm] ---");
        lines.AddRange(FormatDerivedLines(derivedW));

        if (detail?.Evaluation.SiDiagnostics is { } si)
        {
            lines.Add(
                $"Entrainment / capacity: swirl-passage cap hit = {si.AnyEntrainmentLimitedBySwirlPassageCapacity}  (steps capped: {si.EntrainmentStepsLimitedBySwirlPassageCapacity}).");
        }

        lines.Add(
            "Geometry continuity / STL fit: run on final voxel pass when RunGeometryContinuityCheck is true (not evaluated per SI autotune trial).");

        return lines;
    }

    private static IEnumerable<string> FormatGenomeLines(NozzleGeometryGenome g)
    {
        yield return
            $"  Tier A: D_in={g.InletDiameterMm:F2}  D_ch={g.SwirlChamberDiameterMm:F2}  L_ch={g.SwirlChamberLengthMm:F2}  inj_ax={g.InjectorAxialPositionRatio:F3}  " +
            $"exp_L={g.ExpanderLengthMm:F2}  exp_½θ={g.ExpanderHalfAngleDeg:F2}°  D_exit={g.ExitDiameterMm:F2}  stator_θ={g.StatorVaneAngleDeg:F2}°";
        yield return
            $"  Tier B: stator_L_ax={g.StatorAxialLengthMm:F2}  hub_D={g.StatorHubDiameterMm:F2}  vanes={g.StatorVaneCount?.ToString() ?? "(template)"}  chord={g.StatorChordMm?.ToString("F2") ?? "(default)"}";
        yield return
            $"  Tier C: wall={g.WallThicknessMm?.ToString("F2") ?? "(template)"}  lip={g.InletLipLengthMm?.ToString("F2") ?? "(derived)"}  contraction={g.InletContractionLengthMm?.ToString("F2") ?? "(derived)"}";
    }

    private static IEnumerable<string> FormatDerivedLines(DerivedNozzleGeometryParameters d)
    {
        yield return $"  r_ch={d.ChamberInnerRadiusMm:F3}  r_in={d.InletInnerRadiusMm:F3}  r_exit={d.ExitInnerRadiusMm:F3}  r_hub={d.StatorHubRadiusMm:F3}";
        yield return $"  stator span={d.StatorBladeSpanMm:F3}  r_exp_end={d.ExpanderEndInnerRadiusMm:F3}  lip_eff={d.EffectiveInletLipLengthMm:F2}  contr_eff={d.EffectiveInletContractionLengthMm:F2}";
    }
}
