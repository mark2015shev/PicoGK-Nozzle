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

/// <summary>Staged A→B→C SI search over <see cref="NozzleGeometryGenome"/> Tier A (Tier B optional in stage C).</summary>
public static class PhysicsAutotuneRunner
{
    public static NozzleDesignAutotune.Result Run(SourceInputs source, NozzleDesignInputs template, RunConfiguration run)
    {
        var rng = new Random(run.AutotuneRandomSeed);
        var log = new StringBuilder();
        var weights = new PhysicsAutotuneScoreWeights();

        log.AppendLine("=== Physics-controlled nozzle genome autotune (SI thrust authority) ===");
        log.AppendLine("Search object: NozzleGeometryGenome — Tier A primary physics in all stages; Tier B optional in stage C.");
        log.AppendLine("Injector port layout (area, count, slot size, pitch/roll) stays on template; merged by NozzleGeometryGenomeMapper.");

        int nA = Math.Clamp(run.PhysicsAutotuneStageACandidates, 100, 300);
        int topB = Math.Clamp(run.PhysicsAutotuneStageBTopSeeds, 10, 20);
        int perB = Math.Clamp(run.PhysicsAutotuneStageBLocalTrialsPerSeed, 4, 12);
        int nC = Math.Clamp(run.PhysicsAutotuneStageCPolishTrials, 12, 80);
        double spanB = Math.Clamp(run.PhysicsAutotuneStageBRelativeSpan, 0.012, 0.15);
        double spanC = Math.Clamp(run.PhysicsAutotuneStageCRelativeSpan, 0.005, 0.1);

        NozzleDesignInputs baseDesign = CloneTemplateLockedYaw(template, run);
        NozzleGeometryGenome baseGenome = NozzleGeometryGenome.FromDesignInputs(baseDesign);

        FlowTuneEvaluation baseline = NozzleFlowCompositionRoot.EvaluateDesignForTuning(source, baseDesign, run);
        (double baselineScore, _) = PhysicsAutotuneScoring.Compute(baseline, baseline, weights);
        baseline = WithScore(baseline, baselineScore);

        var stageA = new List<(NozzleDesignInputs d, FlowTuneEvaluation ev, double sc, AutoTuneScoreBreakdown bd)>();
        NozzleDesignInputs[] candA = new NozzleDesignInputs[nA];
        for (int i = 0; i < nA; i++)
            candA[i] = ApplyGenome(template, SampleUniformTierA(rng, baseGenome), run);

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
                baseGenome,
                stageBests: new List<NozzleDesignAutotune.AutotuneStageBestSnapshot>());
        }

        var bestA = stageA.MaxBy(x => x.sc);
        log.AppendLine($"Stage A ({nA} candidates, {stageA.Count} valid): best score {bestA.sc:F4}  [Tier A uniform]");

        List<NozzleDesignAutotune.AutotuneStageBestSnapshot> stageBests =
        [
            new NozzleDesignAutotune.AutotuneStageBestSnapshot
            {
                Stage = 1,
                TrialsInStage = nA,
                BestScore = bestA.sc,
                BestDesign = bestA.d,
                BestGenome = NozzleGeometryGenome.FromDesignInputs(bestA.d)
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
                NozzleGeometryGenome g0 = NozzleGeometryGenome.FromDesignInputs(seed);
                for (int j = 0; j < perB; j++)
                    candB[w++] = ApplyGenome(template, PerturbTierA(rng, g0, spanB), run);
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
            log.AppendLine($"Stage B ({stageB.Count} valid / {totalB} trials): best score {bestB.sc:F4}  [Tier A local refine]");
            stageBests.Add(new NozzleDesignAutotune.AutotuneStageBestSnapshot
            {
                Stage = 2,
                TrialsInStage = totalB,
                BestScore = bestB.sc,
                BestDesign = bestB.d,
                BestGenome = NozzleGeometryGenome.FromDesignInputs(bestB.d)
            });
        }

        NozzleGeometryGenome center = NozzleGeometryGenome.FromDesignInputs(bestB.d);
        var stageC = new List<(NozzleDesignInputs d, FlowTuneEvaluation ev, double sc, AutoTuneScoreBreakdown bd)>();
        NozzleDesignInputs[] candC = new NozzleDesignInputs[nC];
        bool unlockB = run.PhysicsAutotuneStageCUnlockTierB;
        for (int i = 0; i < nC; i++)
        {
            NozzleGeometryGenome g = PerturbTierA(rng, center, spanC);
            if (unlockB)
                g = PerturbTierB(rng, g, spanC);
            candC[i] = ApplyGenome(template, g, run);
        }

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
        log.AppendLine(
            $"Stage C ({stageC.Count} valid / {nC} trials): best score {bestC.sc:F4}  [polish{(unlockB ? " + Tier B" : "")}]");
        stageBests.Add(new NozzleDesignAutotune.AutotuneStageBestSnapshot
        {
            Stage = 3,
            TrialsInStage = nC,
            BestScore = bestC.sc,
            BestDesign = bestC.d,
            BestGenome = NozzleGeometryGenome.FromDesignInputs(bestC.d)
        });

        int trialsUsed = nA + totalB + nC;
        NozzleGeometryGenome winnerGenome = NozzleGeometryGenome.FromDesignInputs(bestC.d);
        AppendBestDiagnostics(log, bestC, baseline, winnerGenome, unlockB);

        var detail = new CandidatePhysicsAutotuneResult
        {
            Genome = winnerGenome,
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
            PhysicsAutotuneBestDetail = detail,
            BestGeometryGenome = winnerGenome
        };
    }

    private static void AppendBestDiagnostics(
        StringBuilder log,
        (NozzleDesignInputs d, FlowTuneEvaluation ev, double sc, AutoTuneScoreBreakdown bd) best,
        FlowTuneEvaluation baseline,
        NozzleGeometryGenome genome,
        bool stageCUnlockedTierB)
    {
        SiFlowDiagnostics? si = best.ev.SiDiagnostics;
        DerivedNozzleGeometryParameters der = NozzleGeometryGenomeMapper.Derive(genome);

        log.AppendLine("--- Best candidate (SI) — genome + derived ---");
        log.AppendLine(
            $"  Tier A: D_ch={genome.SwirlChamberDiameterMm:F2}  L_ch={genome.SwirlChamberLengthMm:F2}  D_in={genome.InletDiameterMm:F2}  " +
            $"inj_ax={genome.InjectorAxialPositionRatio:F3}  exp_L={genome.ExpanderLengthMm:F2}  exp½θ={genome.ExpanderHalfAngleDeg:F2}°  " +
            $"D_exit={genome.ExitDiameterMm:F2}  stator={genome.StatorVaneAngleDeg:F2}°  yaw={best.d.InjectorYawAngleDeg:F2}°");
        log.AppendLine(
            $"  Tier B: stator_L={genome.StatorAxialLengthMm:F2}  hub_D={genome.StatorHubDiameterMm:F2}  vanes={genome.StatorVaneCount}  chord={genome.StatorChordMm ?? NozzleGeometryGenomeMapper.DefaultStatorChordMm(genome):F2}  stageC_TierB={(stageCUnlockedTierB ? "on" : "off")}");
        log.AppendLine(
            $"  Derived: r_exp_end={der.ExpanderEndInnerRadiusMm:F3}  stator_span={der.StatorBladeSpanMm:F3}  lip_eff={der.EffectiveInletLipLengthMm:F2}  contr_eff={der.EffectiveInletContractionLengthMm:F2}");
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
            $"  Capacity: swirl-passage cap steps={si?.EntrainmentStepsLimitedBySwirlPassageCapacity ?? 0}  limited={si?.AnyEntrainmentLimitedBySwirlPassageCapacity ?? false}");
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
        const double minDist = 0.045;
        foreach (var t in ordered)
        {
            if (picked.Count >= want)
                break;
            if (picked.Count == 0 || picked.All(p => TierADist(p, t.d) >= minDist))
                picked.Add(t.d);
        }

        foreach (var t in ordered)
        {
            if (picked.Count >= want)
                break;
            if (picked.Any(p => TierADist(p, t.d) < 0.018))
                continue;
            picked.Add(t.d);
        }

        return picked.Take(want).ToList();
    }

    private static double TierADist(NozzleDesignInputs a, NozzleDesignInputs b) =>
        NozzleGeometryGenome.TierADistance(
            NozzleGeometryGenome.FromDesignInputs(a),
            NozzleGeometryGenome.FromDesignInputs(b));

    private static NozzleDesignAutotune.Result Finish(
        NozzleDesignInputs best,
        FlowTuneEvaluation bestEv,
        double score,
        FlowTuneEvaluation baseline,
        int trials,
        StringBuilder log,
        CandidatePhysicsAutotuneResult? detail,
        NozzleGeometryGenome bestGenome,
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
            PhysicsAutotuneBestDetail = detail,
            BestGeometryGenome = detail != null ? detail.Genome : bestGenome
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
        SiDiagnostics = e.SiDiagnostics,
        UnifiedEvaluation = e.UnifiedEvaluation,
        PhysicsPenalties = e.PhysicsPenalties,
        GeometryPenalties = e.GeometryPenalties,
        ConstraintBreakdown = e.ConstraintBreakdown,
        TopPenaltySource = e.TopPenaltySource
    };

    private static NozzleDesignInputs CloneTemplateLockedYaw(NozzleDesignInputs t, RunConfiguration run) =>
        ApplyGenome(t, NozzleGeometryGenome.FromDesignInputs(t), run);

    private static NozzleDesignInputs ApplyGenome(NozzleDesignInputs template, NozzleGeometryGenome g, RunConfiguration run) =>
        NozzleGeometryGenomeMapper.ToDesignInputs(ClampGenome(g), template, run.LockInjectorYawTo90Degrees);

    private static NozzleGeometryGenome SampleUniformTierA(Random rng, NozzleGeometryGenome seed)
    {
        double u(double lo, double hi) => lo + rng.NextDouble() * (hi - lo);
        return seed with
        {
            InletDiameterMm = u(PhysicsAutotuneBounds.InletCaptureDiameterMinMm, PhysicsAutotuneBounds.InletCaptureDiameterMaxMm),
            SwirlChamberDiameterMm = u(PhysicsAutotuneBounds.SwirlChamberDiameterMinMm, PhysicsAutotuneBounds.SwirlChamberDiameterMaxMm),
            SwirlChamberLengthMm = u(PhysicsAutotuneBounds.SwirlChamberLengthMinMm, PhysicsAutotuneBounds.SwirlChamberLengthMaxMm),
            InjectorAxialPositionRatio = u(PhysicsAutotuneBounds.InjectorAxialPositionRatioMin, PhysicsAutotuneBounds.InjectorAxialPositionRatioMax),
            ExpanderHalfAngleDeg = u(PhysicsAutotuneBounds.ExpanderHalfAngleMinDeg, PhysicsAutotuneBounds.ExpanderHalfAngleMaxDeg),
            ExpanderLengthMm = u(PhysicsAutotuneBounds.ExpanderLengthMinMm, PhysicsAutotuneBounds.ExpanderLengthMaxMm),
            ExitDiameterMm = u(PhysicsAutotuneBounds.ExitDiameterMinMm, PhysicsAutotuneBounds.ExitDiameterMaxMm),
            StatorVaneAngleDeg = u(PhysicsAutotuneBounds.StatorVaneAngleMinDeg, PhysicsAutotuneBounds.StatorVaneAngleMaxDeg)
        };
    }

    private static NozzleGeometryGenome PerturbTierA(Random rng, NozzleGeometryGenome c, double relSpan)
    {
        double pm(double x, double lo, double hi)
        {
            double f = 1.0 + (rng.NextDouble() * 2.0 - 1.0) * relSpan;
            return Math.Clamp(x * f, lo, hi);
        }

        return c with
        {
            InletDiameterMm = pm(c.InletDiameterMm, PhysicsAutotuneBounds.InletCaptureDiameterMinMm, PhysicsAutotuneBounds.InletCaptureDiameterMaxMm),
            SwirlChamberDiameterMm = pm(c.SwirlChamberDiameterMm, PhysicsAutotuneBounds.SwirlChamberDiameterMinMm, PhysicsAutotuneBounds.SwirlChamberDiameterMaxMm),
            SwirlChamberLengthMm = pm(c.SwirlChamberLengthMm, PhysicsAutotuneBounds.SwirlChamberLengthMinMm, PhysicsAutotuneBounds.SwirlChamberLengthMaxMm),
            InjectorAxialPositionRatio = pm(c.InjectorAxialPositionRatio, PhysicsAutotuneBounds.InjectorAxialPositionRatioMin, PhysicsAutotuneBounds.InjectorAxialPositionRatioMax),
            ExpanderHalfAngleDeg = pm(c.ExpanderHalfAngleDeg, PhysicsAutotuneBounds.ExpanderHalfAngleMinDeg, PhysicsAutotuneBounds.ExpanderHalfAngleMaxDeg),
            ExpanderLengthMm = pm(c.ExpanderLengthMm, PhysicsAutotuneBounds.ExpanderLengthMinMm, PhysicsAutotuneBounds.ExpanderLengthMaxMm),
            ExitDiameterMm = pm(c.ExitDiameterMm, PhysicsAutotuneBounds.ExitDiameterMinMm, PhysicsAutotuneBounds.ExitDiameterMaxMm),
            StatorVaneAngleDeg = pm(c.StatorVaneAngleDeg, PhysicsAutotuneBounds.StatorVaneAngleMinDeg, PhysicsAutotuneBounds.StatorVaneAngleMaxDeg)
        };
    }

    private static NozzleGeometryGenome PerturbTierB(Random rng, NozzleGeometryGenome c, double relSpan)
    {
        double pm(double x, double lo, double hi)
        {
            double f = 1.0 + (rng.NextDouble() * 2.0 - 1.0) * relSpan;
            return Math.Clamp(x * f, lo, hi);
        }

        int vc = c.StatorVaneCount ?? 12;
        int delta = rng.Next(3) - 1;
        int vn = Math.Clamp(vc + delta, PhysicsAutotuneBounds.StatorVaneCountMin, PhysicsAutotuneBounds.StatorVaneCountMax);
        double chord0 = c.StatorChordMm ?? NozzleGeometryGenomeMapper.DefaultStatorChordMm(c);
        return c with
        {
            StatorHubDiameterMm = pm(c.StatorHubDiameterMm, PhysicsAutotuneBounds.StatorHubDiameterMinMm, PhysicsAutotuneBounds.StatorHubDiameterMaxMm),
            StatorAxialLengthMm = pm(c.StatorAxialLengthMm, PhysicsAutotuneBounds.StatorAxialLengthMinMm, PhysicsAutotuneBounds.StatorAxialLengthMaxMm),
            StatorVaneCount = vn,
            StatorChordMm = pm(chord0, PhysicsAutotuneBounds.StatorChordMinMm, PhysicsAutotuneBounds.StatorChordMaxMm)
        };
    }

    private static NozzleGeometryGenome ClampGenome(NozzleGeometryGenome g)
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
            g.InletDiameterMm,
            PhysicsAutotuneBounds.InletCaptureDiameterMinMm,
            PhysicsAutotuneBounds.InletCaptureDiameterMaxMm);
        dIn = Math.Clamp(dIn, dCh * 1.02, Math.Min(PhysicsAutotuneBounds.InletCaptureDiameterMaxMm, dCh * 1.65));
        double inj = Math.Clamp(
            g.InjectorAxialPositionRatio,
            PhysicsAutotuneBounds.InjectorAxialPositionRatioMin,
            PhysicsAutotuneBounds.InjectorAxialPositionRatioMax);
        double halfAng = Math.Clamp(
            g.ExpanderHalfAngleDeg,
            PhysicsAutotuneBounds.ExpanderHalfAngleMinDeg,
            PhysicsAutotuneBounds.ExpanderHalfAngleMaxDeg);
        double expL = Math.Clamp(
            g.ExpanderLengthMm,
            PhysicsAutotuneBounds.ExpanderLengthMinMm,
            PhysicsAutotuneBounds.ExpanderLengthMaxMm);
        double exit = Math.Clamp(
            g.ExitDiameterMm,
            PhysicsAutotuneBounds.ExitDiameterMinMm,
            PhysicsAutotuneBounds.ExitDiameterMaxMm);
        double st = Math.Clamp(
            g.StatorVaneAngleDeg,
            PhysicsAutotuneBounds.StatorVaneAngleMinDeg,
            PhysicsAutotuneBounds.StatorVaneAngleMaxDeg);
        double stAx = Math.Clamp(
            g.StatorAxialLengthMm,
            PhysicsAutotuneBounds.StatorAxialLengthMinMm,
            PhysicsAutotuneBounds.StatorAxialLengthMaxMm);
        double hub = Math.Clamp(
            g.StatorHubDiameterMm,
            PhysicsAutotuneBounds.StatorHubDiameterMinMm,
            PhysicsAutotuneBounds.StatorHubDiameterMaxMm);
        hub = Math.Min(hub, dCh * 0.92);

        int? vanes = g.StatorVaneCount;
        if (vanes.HasValue)
            vanes = Math.Clamp(vanes.Value, PhysicsAutotuneBounds.StatorVaneCountMin, PhysicsAutotuneBounds.StatorVaneCountMax);

        double? chord = g.StatorChordMm;
        if (chord.HasValue)
            chord = Math.Clamp(chord.Value, PhysicsAutotuneBounds.StatorChordMinMm, PhysicsAutotuneBounds.StatorChordMaxMm);

        return g with
        {
            SwirlChamberDiameterMm = dCh,
            SwirlChamberLengthMm = lCh,
            InletDiameterMm = dIn,
            InjectorAxialPositionRatio = inj,
            ExpanderHalfAngleDeg = halfAng,
            ExpanderLengthMm = expL,
            ExitDiameterMm = exit,
            StatorVaneAngleDeg = st,
            StatorAxialLengthMm = stAx,
            StatorHubDiameterMm = hub,
            StatorVaneCount = vanes,
            StatorChordMm = chord
        };
    }
}
