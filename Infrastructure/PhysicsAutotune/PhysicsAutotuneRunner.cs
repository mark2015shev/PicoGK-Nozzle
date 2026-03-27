using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PicoGK_Run.Core;
using PicoGK_Run.Infrastructure;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Infrastructure.PhysicsAutotune;

/// <summary>Staged A→B→C search over five geometry parameters; SI-only scoring.</summary>
public static class PhysicsAutotuneRunner
{
    public static NozzleDesignAutotune.Result Run(SourceInputs source, NozzleDesignInputs template, RunConfiguration run)
    {
        var rng = new Random(run.AutotuneRandomSeed);
        var log = new StringBuilder();
        var weights = new PhysicsAutotuneScoreWeights();

        log.AppendLine("=== Physics-controlled five-parameter autotune (SI thrust authority) ===");
        log.AppendLine("Tuned [mm/deg]: chamber D, chamber L, inlet capture D, expander half-angle, stator vane angle.");
        log.AppendLine("Fixed: injector yaw = 90° (when LockInjectorYawTo90Degrees), pitch + all other template fields.");

        int nA = Math.Clamp(run.PhysicsAutotuneStageACandidates, 100, 300);
        int topB = Math.Clamp(run.PhysicsAutotuneStageBTopSeeds, 10, 20);
        int perB = Math.Clamp(run.PhysicsAutotuneStageBLocalTrialsPerSeed, 4, 12);
        int nC = Math.Clamp(run.PhysicsAutotuneStageCPolishTrials, 12, 80);
        double spanB = Math.Clamp(run.PhysicsAutotuneStageBRelativeSpan, 0.012, 0.15);
        double spanC = Math.Clamp(run.PhysicsAutotuneStageCRelativeSpan, 0.005, 0.1);

        NozzleDesignInputs baseDesign = CloneTemplateLockedYaw(template, run);
        FlowTuneEvaluation baseline = NozzleFlowCompositionRoot.EvaluateDesignForTuning(source, baseDesign, run);
        (double baselineScore, _) = PhysicsAutotuneScoring.Compute(baseline, baseline, weights);
        baseline = WithScore(baseline, baselineScore);

        var stageA = new List<(NozzleDesignInputs d, FlowTuneEvaluation ev, double sc, AutoTuneScoreBreakdown bd)>();
        NozzleDesignInputs[] candA = new NozzleDesignInputs[nA];
        for (int i = 0; i < nA; i++)
            candA[i] = ApplyGeometry(template, SampleUniform(rng), run);

        FlowTuneEvaluation[] evalA = new FlowTuneEvaluation[nA];
        EvaluateBatch(source, run, candA, evalA);

        for (int i = 0; i < nA; i++)
        {
            FlowTuneEvaluation ev = evalA[i];
            if (ev.HasDesignError)
                continue;
            var (sc, bd) = PhysicsAutotuneScoring.Compute(ev, baseline, weights);
            stageA.Add((candA[i], WithScore(ev, sc), sc, bd));
        }

        if (stageA.Count == 0)
        {
            log.AppendLine("Stage A: no valid candidates — returning baseline template.");
            return Finish(
                baseDesign,
                baseline,
                baselineScore,
                baseline,
                0,
                log,
                null,
                stageBests: new List<NozzleDesignAutotune.AutotuneStageBestSnapshot>());
        }

        var bestA = stageA.MaxBy(x => x.sc);
        log.AppendLine($"Stage A ({nA} candidates, {stageA.Count} valid): best score {bestA.sc:F4}");

        List<NozzleDesignAutotune.AutotuneStageBestSnapshot> stageBests =
        [
            new NozzleDesignAutotune.AutotuneStageBestSnapshot
            {
                Stage = 1,
                TrialsInStage = nA,
                BestScore = bestA.sc,
                BestDesign = bestA.d
            }
        ];

        List<NozzleDesignInputs> seeds = SelectDiverseTop(stageA, topB);
        var stageB = new List<(NozzleDesignInputs d, FlowTuneEvaluation ev, double sc, AutoTuneScoreBreakdown bd)>();
        int totalB = seeds.Count * perB;
        if (totalB > 0)
        {
            var candB = new NozzleDesignInputs[totalB];
            int w = 0;
            foreach (NozzleDesignInputs seed in seeds)
            {
                CandidateGeometry g0 = FromDesign(seed);
                for (int j = 0; j < perB; j++)
                    candB[w++] = ApplyGeometry(template, Perturb(rng, g0, spanB), run);
            }

            var evalB = new FlowTuneEvaluation[totalB];
            EvaluateBatch(source, run, candB, evalB);
            for (int i = 0; i < totalB; i++)
            {
                FlowTuneEvaluation ev = evalB[i];
                if (ev.HasDesignError)
                    continue;
                var (sc, bd) = PhysicsAutotuneScoring.Compute(ev, baseline, weights);
                stageB.Add((candB[i], WithScore(ev, sc), sc, bd));
            }
        }

        var bestB = stageB.Count > 0 ? stageB.MaxBy(x => x.sc) : bestA;
        if (stageB.Count > 0)
        {
            log.AppendLine($"Stage B ({stageB.Count} valid / {totalB} trials): best score {bestB.sc:F4}");
            stageBests.Add(new NozzleDesignAutotune.AutotuneStageBestSnapshot
            {
                Stage = 2,
                TrialsInStage = totalB,
                BestScore = bestB.sc,
                BestDesign = bestB.d
            });
        }

        CandidateGeometry center = FromDesign(bestB.d);
        var stageC = new List<(NozzleDesignInputs d, FlowTuneEvaluation ev, double sc, AutoTuneScoreBreakdown bd)>();
        NozzleDesignInputs[] candC = new NozzleDesignInputs[nC];
        for (int i = 0; i < nC; i++)
            candC[i] = ApplyGeometry(template, Perturb(rng, center, spanC), run);
        FlowTuneEvaluation[] evalC = new FlowTuneEvaluation[nC];
        EvaluateBatch(source, run, candC, evalC);
        for (int i = 0; i < nC; i++)
        {
            FlowTuneEvaluation ev = evalC[i];
            if (ev.HasDesignError)
                continue;
            var (sc, bd) = PhysicsAutotuneScoring.Compute(ev, baseline, weights);
            stageC.Add((candC[i], WithScore(ev, sc), sc, bd));
        }

        var bestC = stageC.Count > 0 ? stageC.MaxBy(x => x.sc) : bestB;
        log.AppendLine($"Stage C ({stageC.Count} valid / {nC} trials): best score {bestC.sc:F4}");
        stageBests.Add(new NozzleDesignAutotune.AutotuneStageBestSnapshot
        {
            Stage = 3,
            TrialsInStage = nC,
            BestScore = bestC.sc,
            BestDesign = bestC.d
        });

        int trialsUsed = nA + totalB + nC;
        AppendBestDiagnostics(log, bestC, baseline);

        var detail = new CandidatePhysicsAutotuneResult
        {
            Geometry = FromDesign(bestC.d),
            DesignUsed = bestC.d,
            Evaluation = bestC.ev,
            ScoreBreakdown = bestC.bd,
            Stage = 3
        };

        return new NozzleDesignAutotune.Result
        {
            BestSeedDesign = bestC.d,
            BestScore = bestC.sc,
            TrialsUsed = trialsUsed,
            BaselineEvaluation = baseline,
            BestEvaluation = bestC.ev,
            CoarseToFineLog = log.ToString(),
            StageBests = stageBests,
            PhysicsAutotuneBestDetail = detail
        };
    }

    private static void AppendBestDiagnostics(StringBuilder log, (NozzleDesignInputs d, FlowTuneEvaluation ev, double sc, AutoTuneScoreBreakdown bd) best, FlowTuneEvaluation baseline)
    {
        SiFlowDiagnostics? si = best.ev.SiDiagnostics;
        log.AppendLine("--- Best candidate (SI) ---");
        log.AppendLine(
            $"  Geometry: D_ch={best.d.SwirlChamberDiameterMm:F2} mm  L_ch={best.d.SwirlChamberLengthMm:F2} mm  D_in={best.d.InletDiameterMm:F2} mm  " +
            $"exp half-angle={best.d.ExpanderHalfAngleDeg:F2}°  stator={best.d.StatorVaneAngleDeg:F2}°  yaw={best.d.InjectorYawAngleDeg:F2}°");
        log.AppendLine(
            $"  Flow: ṁ_p={best.ev.CoreMassFlowKgS:F4}  ṁ_s={best.ev.AmbientAirMassFlowKgS:F4}  ṁ_tot={best.ev.CoreMassFlowKgS + best.ev.AmbientAirMassFlowKgS:F4} kg/s  ER={best.ev.EntrainmentRatio:F3}");
        if (si?.PhysicsStepStates.Count > 0)
        {
            var last = si.PhysicsStepStates[^1];
            log.AppendLine(
                $"  Chamber end: V_ax={last.VAxialMps:F2}  V_t={last.VTangentialMps:F2}  M={last.Mach:F4}  Re_D={last.Reynolds:F0}  S_flux={last.SwirlNumberFlux:F3}");
            log.AppendLine(
                $"  Pressure: P_amb={si.SwirlChamberHealth?.AmbientStaticPressurePa ?? 0:F0} Pa ({SiPressureGuards.PaToBar(si.SwirlChamberHealth?.AmbientStaticPressurePa ?? 0):F4} bar)  " +
                $"P_core≈{si.SwirlChamberHealth?.EstimatedCoreStaticPressurePa ?? 0:F0} Pa ({SiPressureGuards.PaToBar(si.SwirlChamberHealth?.EstimatedCoreStaticPressurePa ?? 0):F4} bar)  " +
                $"Δp_rad≈{last.RadialPressureDeltaPa:F0} Pa");
        }

        log.AppendLine(
            $"  Thrust: F_net={best.ev.NetThrustN:F2} N  (baseline {baseline.NetThrustN:F2} N)  choked={si?.AnyEntrainmentStepChoked ?? false}  " +
            $"sep_risk={best.ev.PhysicsMetrics.SeparationRisk01:F3}");
        log.AppendLine(
            $"  Score: {best.sc:F4}  (thrust×axial×stator×ER terms − penalties — see PhysicsAutotuneScoring)");
    }

    private static void EvaluateBatch(
        SourceInputs source,
        RunConfiguration run,
        NozzleDesignInputs[] candidates,
        FlowTuneEvaluation[] sink)
    {
        int n = candidates.Length;
        if (n == 0)
            return;
        ParallelOptions po = AutotuneParallelOptions(run);
        if (run.AutotuneUseParallelEvaluation)
            Parallel.For(0, n, po, i => sink[i] = NozzleFlowCompositionRoot.EvaluateDesignForTuning(source, candidates[i], run));
        else
        {
            for (int i = 0; i < n; i++)
                sink[i] = NozzleFlowCompositionRoot.EvaluateDesignForTuning(source, candidates[i], run);
        }
    }

    private static ParallelOptions AutotuneParallelOptions(RunConfiguration run)
    {
        var o = new ParallelOptions();
        if (run.AutotuneMaxDegreeOfParallelism > 0)
            o.MaxDegreeOfParallelism = run.AutotuneMaxDegreeOfParallelism;
        return o;
    }

    private static List<NozzleDesignInputs> SelectDiverseTop(
        List<(NozzleDesignInputs d, FlowTuneEvaluation ev, double sc, AutoTuneScoreBreakdown bd)> sortedPool,
        int want)
    {
        var ordered = sortedPool.OrderByDescending(x => x.sc).ToList();
        var picked = new List<NozzleDesignInputs>();
        const double minNorm = 0.08;
        foreach (var t in ordered)
        {
            if (picked.Count >= want)
                break;
            if (picked.Count == 0 || picked.All(p => GeometryNorm(p, t.d) >= minNorm))
                picked.Add(t.d);
        }

        foreach (var t in ordered)
        {
            if (picked.Count >= want)
                break;
            if (picked.Any(p => GeometryNorm(p, t.d) < 0.02))
                continue;
            picked.Add(t.d);
        }

        return picked.Take(want).ToList();
    }

    private static double GeometryNorm(NozzleDesignInputs a, NozzleDesignInputs b)
    {
        static double q(double x, double y, double scale) => Math.Pow((x - y) / Math.Max(scale, 1e-6), 2);
        return Math.Sqrt(
            q(a.SwirlChamberDiameterMm, b.SwirlChamberDiameterMm, 55)
            + q(a.SwirlChamberLengthMm, b.SwirlChamberLengthMm, 80)
            + q(a.InletDiameterMm, b.InletDiameterMm, 50)
            + q(a.ExpanderHalfAngleDeg, b.ExpanderHalfAngleDeg, 3)
            + q(a.StatorVaneAngleDeg, b.StatorVaneAngleDeg, 20));
    }

    private static NozzleDesignAutotune.Result Finish(
        NozzleDesignInputs best,
        FlowTuneEvaluation bestEv,
        double score,
        FlowTuneEvaluation baseline,
        int trials,
        StringBuilder log,
        CandidatePhysicsAutotuneResult? detail,
        List<NozzleDesignAutotune.AutotuneStageBestSnapshot> stageBests)
    {
        return new NozzleDesignAutotune.Result
        {
            BestSeedDesign = best,
            BestScore = score,
            TrialsUsed = trials,
            BaselineEvaluation = baseline,
            BestEvaluation = bestEv,
            CoarseToFineLog = log.ToString(),
            StageBests = stageBests,
            PhysicsAutotuneBestDetail = detail
        };
    }

    private static FlowTuneEvaluation WithScore(FlowTuneEvaluation e, double score) => new()
    {
        CandidateDesign = e.CandidateDesign,
        DrivenDesign = e.DrivenDesign,
        NetThrustN = e.NetThrustN,
        SourceOnlyThrustN = e.SourceOnlyThrustN,
        EntrainmentRatio = e.EntrainmentRatio,
        VortexQualityMetric = e.VortexQualityMetric,
        PhysicsMetrics = e.PhysicsMetrics,
        Score = score,
        HealthCount = e.HealthCount,
        HasDesignError = e.HasDesignError,
        HealthMessages = e.HealthMessages,
        AmbientAirMassFlowKgS = e.AmbientAirMassFlowKgS,
        CoreMassFlowKgS = e.CoreMassFlowKgS,
        SiDiagnostics = e.SiDiagnostics
    };

    private static NozzleDesignInputs CloneTemplateLockedYaw(NozzleDesignInputs t, RunConfiguration run) =>
        ApplyGeometry(t, FromDesign(t), run);

    private static CandidateGeometry SampleUniform(Random rng)
    {
        double u(double lo, double hi) => lo + rng.NextDouble() * (hi - lo);
        return new CandidateGeometry(
            u(PhysicsAutotuneBounds.SwirlChamberDiameterMinMm, PhysicsAutotuneBounds.SwirlChamberDiameterMaxMm),
            u(PhysicsAutotuneBounds.SwirlChamberLengthMinMm, PhysicsAutotuneBounds.SwirlChamberLengthMaxMm),
            u(PhysicsAutotuneBounds.InletCaptureDiameterMinMm, PhysicsAutotuneBounds.InletCaptureDiameterMaxMm),
            u(PhysicsAutotuneBounds.ExpanderHalfAngleMinDeg, PhysicsAutotuneBounds.ExpanderHalfAngleMaxDeg),
            u(PhysicsAutotuneBounds.StatorVaneAngleMinDeg, PhysicsAutotuneBounds.StatorVaneAngleMaxDeg));
    }

    private static CandidateGeometry Perturb(Random rng, CandidateGeometry c, double relSpan)
    {
        double pm(double x, double lo, double hi)
        {
            double f = 1.0 + (rng.NextDouble() * 2.0 - 1.0) * relSpan;
            return Math.Clamp(x * f, lo, hi);
        }

        return new CandidateGeometry(
            pm(c.SwirlChamberDiameterMm, PhysicsAutotuneBounds.SwirlChamberDiameterMinMm, PhysicsAutotuneBounds.SwirlChamberDiameterMaxMm),
            pm(c.SwirlChamberLengthMm, PhysicsAutotuneBounds.SwirlChamberLengthMinMm, PhysicsAutotuneBounds.SwirlChamberLengthMaxMm),
            pm(c.InletCaptureDiameterMm, PhysicsAutotuneBounds.InletCaptureDiameterMinMm, PhysicsAutotuneBounds.InletCaptureDiameterMaxMm),
            pm(c.ExpanderHalfAngleDeg, PhysicsAutotuneBounds.ExpanderHalfAngleMinDeg, PhysicsAutotuneBounds.ExpanderHalfAngleMaxDeg),
            pm(c.StatorVaneAngleDeg, PhysicsAutotuneBounds.StatorVaneAngleMinDeg, PhysicsAutotuneBounds.StatorVaneAngleMaxDeg));
    }

    private static CandidateGeometry FromDesign(NozzleDesignInputs d) => new(
        d.SwirlChamberDiameterMm,
        d.SwirlChamberLengthMm,
        d.InletDiameterMm,
        d.ExpanderHalfAngleDeg,
        d.StatorVaneAngleDeg);

    private static NozzleDesignInputs ApplyGeometry(NozzleDesignInputs template, CandidateGeometry g, RunConfiguration run)
    {
        double dCh = Math.Clamp(
            g.SwirlChamberDiameterMm,
            PhysicsAutotuneBounds.SwirlChamberDiameterMinMm,
            PhysicsAutotuneBounds.SwirlChamberDiameterMaxMm);
        double lCh = Math.Clamp(
            g.SwirlChamberLengthMm,
            PhysicsAutotuneBounds.SwirlChamberLengthMinMm,
            PhysicsAutotuneBounds.SwirlChamberLengthMaxMm);
        double dIn = Math.Clamp(
            g.InletCaptureDiameterMm,
            PhysicsAutotuneBounds.InletCaptureDiameterMinMm,
            PhysicsAutotuneBounds.InletCaptureDiameterMaxMm);
        dIn = Math.Clamp(dIn, dCh * 1.02, Math.Min(PhysicsAutotuneBounds.InletCaptureDiameterMaxMm, dCh * 1.65));
        double halfAng = Math.Clamp(
            g.ExpanderHalfAngleDeg,
            PhysicsAutotuneBounds.ExpanderHalfAngleMinDeg,
            PhysicsAutotuneBounds.ExpanderHalfAngleMaxDeg);
        double st = Math.Clamp(
            g.StatorVaneAngleDeg,
            PhysicsAutotuneBounds.StatorVaneAngleMinDeg,
            PhysicsAutotuneBounds.StatorVaneAngleMaxDeg);

        double yaw = run.LockInjectorYawTo90Degrees ? 90.0 : template.InjectorYawAngleDeg;

        return new NozzleDesignInputs
        {
            InletDiameterMm = dIn,
            SwirlChamberDiameterMm = dCh,
            SwirlChamberLengthMm = lCh,
            InjectorAxialPositionRatio = template.InjectorAxialPositionRatio,
            TotalInjectorAreaMm2 = template.TotalInjectorAreaMm2,
            InjectorCount = template.InjectorCount,
            InjectorWidthMm = template.InjectorWidthMm,
            InjectorHeightMm = template.InjectorHeightMm,
            InjectorYawAngleDeg = yaw,
            InjectorPitchAngleDeg = template.InjectorPitchAngleDeg,
            InjectorRollAngleDeg = template.InjectorRollAngleDeg,
            ExpanderLengthMm = template.ExpanderLengthMm,
            ExpanderHalfAngleDeg = halfAng,
            ExitDiameterMm = template.ExitDiameterMm,
            StatorVaneAngleDeg = st,
            StatorVaneCount = template.StatorVaneCount,
            StatorHubDiameterMm = template.StatorHubDiameterMm,
            StatorAxialLengthMm = template.StatorAxialLengthMm,
            StatorBladeChordMm = template.StatorBladeChordMm,
            WallThicknessMm = template.WallThicknessMm
        };
    }
}
