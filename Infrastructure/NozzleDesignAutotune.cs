using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PicoGK_Run.Core;
using PicoGK_Run.Infrastructure.PhysicsAutotune;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Infrastructure;

/// <summary>
/// Reproducible random search on the coupled SI model (no voxels per trial).
/// <see cref="AutotuneStrategy.SingleStage"/> keeps the legacy one-pool search;
/// <see cref="AutotuneStrategy.CoarseToFine"/> runs broad → diverse top seeds → polish.
/// </summary>
public static class NozzleDesignAutotune
{
    public sealed class Result
    {
        public NozzleDesignInputs BestSeedDesign { get; init; } = null!;
        public double BestScore { get; init; }
        public int TrialsUsed { get; init; }
        public FlowTuneEvaluation BaselineEvaluation { get; init; } = null!;
        public FlowTuneEvaluation? BestEvaluation { get; init; }

        /// <summary>Human-readable stage log when <see cref="AutotuneStrategy.CoarseToFine"/> was used.</summary>
        public string? CoarseToFineLog { get; init; }

        public IReadOnlyList<AutotuneStageBestSnapshot>? StageBests { get; init; }

        /// <summary>Populated for <see cref="AutotuneStrategy.PhysicsControlledFiveParameter"/>.</summary>
        public CandidatePhysicsAutotuneResult? PhysicsAutotuneBestDetail { get; init; }

        /// <summary>Winning <see cref="NozzleGeometryGenome"/> when physics autotune ran (same skeleton as <see cref="BestSeedDesign"/>).</summary>
        public NozzleGeometryGenome? BestGeometryGenome { get; init; }
    }

    public sealed class AutotuneStageBestSnapshot
    {
        public int Stage { get; init; }
        public int TrialsInStage { get; init; }
        public double BestScore { get; init; }
        public NozzleDesignInputs BestDesign { get; init; } = null!;

        /// <summary>Genome for <see cref="BestDesign"/> when strategy is physics-controlled autotune.</summary>
        public NozzleGeometryGenome? BestGenome { get; init; }
    }

    private readonly struct Knobs
    {
        public readonly double ChamberD;
        public readonly double ChamberL;
        public readonly double Inlet;
        public readonly double Exit;
        public readonly double ExpAngle;
        public readonly double ExpLength;
        public readonly double StatorAngle;
        public readonly double AxialPositionScale;
        public readonly double SynthesisTargetEr;
        public readonly double InjectorYawDeg;
        public readonly double InjectorPitchDeg;

        public Knobs(
            double chamberD,
            double chamberL,
            double inlet,
            double exit,
            double expAngle,
            double expLength,
            double statorAngle,
            double axialPositionScale,
            double synthesisTargetEr,
            double injectorYawDeg,
            double injectorPitchDeg)
        {
            ChamberD = chamberD;
            ChamberL = chamberL;
            Inlet = inlet;
            Exit = exit;
            ExpAngle = expAngle;
            ExpLength = expLength;
            StatorAngle = statorAngle;
            AxialPositionScale = axialPositionScale;
            SynthesisTargetEr = synthesisTargetEr;
            InjectorYawDeg = injectorYawDeg;
            InjectorPitchDeg = injectorPitchDeg;
        }
    }

    private sealed record ScoredTrial(NozzleDesignInputs Design, double Score, FlowTuneEvaluation Eval);

    public static Result FindBestSeed(SourceInputs source, NozzleDesignInputs template, RunConfiguration run)
    {
        if (run.AutotuneStrategy == AutotuneStrategy.PhysicsControlledFiveParameter)
            return PhysicsAutotuneRunner.Run(source, template, run);
        return run.AutotuneStrategy == AutotuneStrategy.CoarseToFine
            ? FindBestSeedCoarseToFine(source, template, run)
            : FindBestSeedSingleStage(source, template, run);
    }

    private static Result FindBestSeedSingleStage(SourceInputs source, NozzleDesignInputs template, RunConfiguration run)
    {
        int trials = Math.Clamp(run.AutotuneTrials, 20, 5000);
        var rng = new Random(run.AutotuneRandomSeed);

        NozzleDesignInputs baseSeed = BaselineSeed(source, template, run);
        FlowTuneEvaluation baselineEval = NozzleFlowCompositionRoot.EvaluateDesignForTuning(source, baseSeed, run);

        // Single-threaded RNG + candidate construction keeps trials reproducible; SI solves are embarrassingly parallel.
        var knobs = new Knobs[trials];
        for (int i = 0; i < trials; i++)
            knobs[i] = SampleKnobsLegacy(rng, run, template);

        var candidates = new NozzleDesignInputs[trials];
        for (int i = 0; i < trials; i++)
        {
            candidates[i] = BuildCandidateFromKnobs(
                source,
                template,
                knobs[i],
                run,
                useSynthesisBase: run.AutotuneUseSynthesisBaseline);
        }

        var evals = new FlowTuneEvaluation[trials];
        EvaluateTrialBatch(source, run, candidates, evals);

        double bestScore = double.NegativeInfinity;
        NozzleDesignInputs bestSeed = baseSeed;
        FlowTuneEvaluation? bestEv = null;

        for (int i = 0; i < trials; i++)
        {
            FlowTuneEvaluation ev = evals[i];
            if (ev.HasDesignError)
                continue;

            double score = AutotuneScoring.ComputeScore(ev, baselineEval, run);
            ev = WithScore(ev, score);

            if (score > bestScore)
            {
                bestScore = score;
                bestSeed = candidates[i];
                bestEv = ev;
            }
        }

        if (bestScore <= double.NegativeInfinity)
        {
            bestScore = AutotuneScoring.ComputeScore(baselineEval, baselineEval, run);
            bestSeed = baseSeed;
            bestEv = WithScore(baselineEval, bestScore);
        }

        return new Result
        {
            BestSeedDesign = bestSeed,
            BestScore = bestScore,
            TrialsUsed = trials,
            BaselineEvaluation = baselineEval,
            BestEvaluation = bestEv
        };
    }

    private static Result FindBestSeedCoarseToFine(SourceInputs source, NozzleDesignInputs template, RunConfiguration run)
    {
        int n1 = Math.Clamp(run.AutotuneStage1Trials, 12, 8000);
        int n2 = Math.Clamp(run.AutotuneStage2Trials, 8, 8000);
        int n3 = Math.Clamp(run.AutotuneStage3Trials, 4, 4000);
        int top1 = Math.Clamp(run.AutotuneTopSeedCountStage1, 2, 12);
        int top2Keep = Math.Clamp(run.AutotuneTopSeedCountStage2, 1, 6);
        double divMin = Math.Clamp(run.AutotuneDiversityMinDistance, 0.12, 1.2);

        var rng = new Random(run.AutotuneRandomSeed);
        var log = new StringBuilder();
        log.AppendLine("=== Coarse-to-fine autotune (SI-only trials; not CFD) ===");

        NozzleDesignInputs baseSeed = BaselineSeed(source, template, run);
        FlowTuneEvaluation baselineEval = NozzleFlowCompositionRoot.EvaluateDesignForTuning(source, baseSeed, run);
        NozzleDesignInputs angleRef = CloneDesign(template);

        var stageBests = new List<AutotuneStageBestSnapshot>();
        var allStage1 = new List<ScoredTrial>();

        var knobs1 = new Knobs[n1];
        for (int i = 0; i < n1; i++)
            knobs1[i] = SampleKnobsFromBand(rng, run.AutotuneStage1Band, run, angleRef);

        var candidates1 = new NozzleDesignInputs[n1];
        for (int i = 0; i < n1; i++)
        {
            candidates1[i] = BuildCandidateFromKnobs(
                source,
                template,
                knobs1[i],
                run,
                useSynthesisBase: run.AutotuneUseSynthesisBaseline);
        }

        var evals1 = new FlowTuneEvaluation[n1];
        EvaluateTrialBatch(source, run, candidates1, evals1);

        for (int i = 0; i < n1; i++)
        {
            FlowTuneEvaluation ev = evals1[i];
            if (ev.HasDesignError)
                continue;
            double score = AutotuneScoring.ComputeScore(ev, baselineEval, run);
            ev = WithScore(ev, score);
            allStage1.Add(new ScoredTrial(candidates1[i], score, ev));
        }

        if (allStage1.Count == 0)
            return FallbackBaseline(source, baseSeed, baselineEval, run, log, stageBests);

        ScoredTrial best1 = allStage1.MaxBy(t => t.Score)!;
        stageBests.Add(new AutotuneStageBestSnapshot { Stage = 1, TrialsInStage = n1, BestScore = best1.Score, BestDesign = best1.Design });
        log.AppendLine($"Stage 1 ({n1} trials): best score {best1.Score:F4} (broad exploration).");
        LogDesignDeltaLine(log, "  Stage1 best vs SI baseline seed", baseSeed, best1.Design);

        List<ScoredTrial> sorted1 = allStage1.OrderByDescending(t => t.Score).ToList();
        List<NozzleDesignInputs> diverseSeeds = SelectDiverseSeeds(sorted1, top1, divMin);
        log.AppendLine($"  Retained {diverseSeeds.Count} diverse top seeds for stage 2 (min norm distance ≥ {divMin:F2}).");

        var allStage2 = new List<ScoredTrial>();
        int seedsC = Math.Max(1, diverseSeeds.Count);
        int basePer = n2 / seedsC;
        int rem = n2 - basePer * seedsC;
        int[] perSeed2 = new int[diverseSeeds.Count];
        for (int s = 0; s < diverseSeeds.Count; s++)
            perSeed2[s] = basePer + (s < rem ? 1 : 0);

        int total2 = 0;
        for (int s = 0; s < diverseSeeds.Count; s++)
            total2 += perSeed2[s];

        var candidates2 = new NozzleDesignInputs[total2];
        int w2 = 0;
        for (int s = 0; s < diverseSeeds.Count; s++)
        {
            NozzleDesignInputs seed = diverseSeeds[s];
            for (int j = 0; j < perSeed2[s]; j++)
            {
                Knobs k = SampleKnobsFromBand(rng, run.AutotuneStage2Band, run, seed);
                candidates2[w2++] = ApplyKnobs(CloneDesign(seed), k, run);
            }
        }

        var evals2 = new FlowTuneEvaluation[total2];
        EvaluateTrialBatch(source, run, candidates2, evals2);

        for (int i = 0; i < total2; i++)
        {
            FlowTuneEvaluation ev = evals2[i];
            if (ev.HasDesignError)
                continue;
            double score = AutotuneScoring.ComputeScore(ev, baselineEval, run);
            ev = WithScore(ev, score);
            allStage2.Add(new ScoredTrial(candidates2[i], score, ev));
        }

        if (allStage2.Count == 0)
        {
            log.AppendLine("Stage 2: no valid trials — using stage 1 winner.");
            return FinishResult(best1.Design, best1.Score, best1.Eval, baselineEval, n1, log, stageBests);
        }

        ScoredTrial best2 = allStage2.MaxBy(t => t.Score)!;
        stageBests.Add(new AutotuneStageBestSnapshot { Stage = 2, TrialsInStage = n2, BestScore = best2.Score, BestDesign = best2.Design });
        log.AppendLine($"Stage 2 ({n2} trials): best score {best2.Score:F4} (refinement around diverse seeds).");
        LogDesignDeltaLine(log, "  Stage2 best vs stage1 best", best1.Design, best2.Design);

        List<ScoredTrial> sorted2 = allStage2.OrderByDescending(t => t.Score).ToList();
        LogSecondStageRunnerUp(log, sorted2, top2Keep, best2);

        NozzleDesignInputs polishCenter = best2.Design;
        var allStage3 = new List<ScoredTrial>();

        var knobs3 = new Knobs[n3];
        for (int i = 0; i < n3; i++)
            knobs3[i] = SampleKnobsFromBand(rng, run.AutotuneStage3Band, run, polishCenter);

        var candidates3 = new NozzleDesignInputs[n3];
        for (int i = 0; i < n3; i++)
            candidates3[i] = ApplyKnobs(CloneDesign(polishCenter), knobs3[i], run);

        var evals3 = new FlowTuneEvaluation[n3];
        EvaluateTrialBatch(source, run, candidates3, evals3);

        for (int i = 0; i < n3; i++)
        {
            FlowTuneEvaluation ev = evals3[i];
            if (ev.HasDesignError)
                continue;
            double score = AutotuneScoring.ComputeScore(ev, baselineEval, run);
            ev = WithScore(ev, score);
            allStage3.Add(new ScoredTrial(candidates3[i], score, ev));
        }

        if (allStage3.Count == 0)
        {
            log.AppendLine("Stage 3: no valid trials — using stage 2 winner.");
            return FinishResult(best2.Design, best2.Score, best2.Eval, baselineEval, n1 + n2, log, stageBests);
        }

        ScoredTrial best3 = allStage3.MaxBy(t => t.Score)!;
        stageBests.Add(new AutotuneStageBestSnapshot { Stage = 3, TrialsInStage = n3, BestScore = best3.Score, BestDesign = best3.Design });
        log.AppendLine($"Stage 3 ({n3} trials): best score {best3.Score:F4} (fine polish).");
        LogDesignDeltaLine(log, "  Stage3 best vs stage2 best", best2.Design, best3.Design);
        log.AppendLine("Final winning design vs original template:");
        LogDesignDeltaLine(log, "  Final vs input template", template, best3.Design);
        log.AppendLine($"Total SI evaluations: {n1 + n2 + n3} (no voxels in search).");

        return new Result
        {
            BestSeedDesign = best3.Design,
            BestScore = best3.Score,
            TrialsUsed = n1 + n2 + n3,
            BaselineEvaluation = baselineEval,
            BestEvaluation = best3.Eval,
            CoarseToFineLog = log.ToString(),
            StageBests = stageBests
        };
    }

    private static Result FallbackBaseline(
        SourceInputs source,
        NozzleDesignInputs baseSeed,
        FlowTuneEvaluation baselineEval,
        RunConfiguration run,
        StringBuilder log,
        List<AutotuneStageBestSnapshot> stageBests)
    {
        _ = source;
        log.AppendLine("Stage 1 produced no valid candidates — using baseline seed.");
        double score = AutotuneScoring.ComputeScore(baselineEval, baselineEval, run);
        FlowTuneEvaluation ev = WithScore(baselineEval, score);
        stageBests.Add(new AutotuneStageBestSnapshot
        {
            Stage = 1,
            TrialsInStage = 0,
            BestScore = score,
            BestDesign = baseSeed
        });
        return new Result
        {
            BestSeedDesign = baseSeed,
            BestScore = score,
            TrialsUsed = 0,
            BaselineEvaluation = baselineEval,
            BestEvaluation = ev,
            CoarseToFineLog = log.ToString(),
            StageBests = stageBests
        };
    }

    private static void LogSecondStageRunnerUp(StringBuilder log, List<ScoredTrial> sorted2, int top2Keep, ScoredTrial best2)
    {
        if (sorted2.Count < 2)
            return;
        ScoredTrial second = sorted2[1];
        log.AppendLine(
            $"  Stage 2 runner-up #2 score {second.Score:F4} (best {best2.Score:F4}); stage 3 polishes best only (top2Keep={top2Keep} for future use).");
    }

    /// <summary>
    /// Parallel SI evaluations only — PicoGK is never touched here.
    /// Candidates must be fully built on one thread first so RNG and synthesis order stay deterministic.
    /// </summary>
    private static void EvaluateTrialBatch(
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
        int m = run.AutotuneMaxDegreeOfParallelism;
        if (m > 0)
            o.MaxDegreeOfParallelism = m;
        return o;
    }

    private static Result FinishResult(
        NozzleDesignInputs best,
        double score,
        FlowTuneEvaluation ev,
        FlowTuneEvaluation baselineEval,
        int trials,
        StringBuilder log,
        List<AutotuneStageBestSnapshot> stageBests)
    {
        log.AppendLine($"Total SI evaluations: {trials} (no voxels in search).");
        return new Result
        {
            BestSeedDesign = best,
            BestScore = score,
            TrialsUsed = trials,
            BaselineEvaluation = baselineEval,
            BestEvaluation = ev,
            CoarseToFineLog = log.ToString(),
            StageBests = stageBests
        };
    }

    private static void LogDesignDeltaLine(StringBuilder sb, string label, NozzleDesignInputs a, NozzleDesignInputs b)
    {
        sb.AppendLine(label + ": Δ chamber D/L "
            + $"{b.SwirlChamberDiameterMm - a.SwirlChamberDiameterMm:F2} / {b.SwirlChamberLengthMm - a.SwirlChamberLengthMm:F2} mm; "
            + $"Δ yaw/pitch {b.InjectorYawAngleDeg - a.InjectorYawAngleDeg:F2} / {b.InjectorPitchAngleDeg - a.InjectorPitchAngleDeg:F2} deg; "
            + $"Δ expander L/angle {b.ExpanderLengthMm - a.ExpanderLengthMm:F2} mm / {b.ExpanderHalfAngleDeg - a.ExpanderHalfAngleDeg:F2} deg.");
    }

    private static List<NozzleDesignInputs> SelectDiverseSeeds(List<ScoredTrial> sortedDesc, int n, double minNorm)
    {
        var picked = new List<ScoredTrial>();
        foreach (ScoredTrial t in sortedDesc)
        {
            if (picked.Count >= n)
                break;
            if (picked.Count == 0 || picked.All(p => DesignDistanceNorm(p.Design, t.Design) >= minNorm))
                picked.Add(t);
        }

        foreach (ScoredTrial t in sortedDesc)
        {
            if (picked.Count >= n)
                break;
            if (picked.Any(p => DesignDistanceNorm(p.Design, t.Design) < 0.025))
                continue;
            picked.Add(t);
        }

        return picked.Take(n).Select(t => t.Design).ToList();
    }

    private static double DesignDistanceNorm(NozzleDesignInputs a, NozzleDesignInputs b)
    {
        static double q(double x, double y, double scale) => Math.Pow((x - y) / Math.Max(scale, 1e-6), 2);
        return Math.Sqrt(
            q(a.SwirlChamberDiameterMm, b.SwirlChamberDiameterMm, 30)
            + q(a.SwirlChamberLengthMm, b.SwirlChamberLengthMm, 40)
            + q(a.InletDiameterMm, b.InletDiameterMm, 35)
            + q(a.ExitDiameterMm, b.ExitDiameterMm, 35)
            + q(a.ExpanderHalfAngleDeg, b.ExpanderHalfAngleDeg, 8)
            + q(a.ExpanderLengthMm, b.ExpanderLengthMm, 50)
            + q(a.StatorVaneAngleDeg, b.StatorVaneAngleDeg, 18)
            + q(a.InjectorAxialPositionRatio, b.InjectorAxialPositionRatio, 0.35)
            + q(a.InjectorYawAngleDeg, b.InjectorYawAngleDeg, 22)
            + q(a.InjectorPitchAngleDeg, b.InjectorPitchAngleDeg, 10));
    }

    private static NozzleDesignInputs BaselineSeed(SourceInputs source, NozzleDesignInputs template, RunConfiguration run) =>
        run.AutotuneUseSynthesisBaseline
            ? NozzleGeometrySynthesis.Synthesize(source, template, run.GeometrySynthesisTargetEntrainmentRatio, run)
            : CloneDesign(template);

    private static NozzleDesignInputs BuildCandidateFromKnobs(
        SourceInputs source,
        NozzleDesignInputs template,
        Knobs k,
        RunConfiguration run,
        bool useSynthesisBase)
    {
        if (useSynthesisBase)
            return ApplyKnobs(NozzleGeometrySynthesis.Synthesize(source, template, k.SynthesisTargetEr, run), k, run);
        return ApplyKnobs(CloneDesign(template), k, run);
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

    /// <summary>Legacy single-stage sampling (unchanged bands).</summary>
    private static Knobs SampleKnobsLegacy(Random rng, RunConfiguration run, NozzleDesignInputs template)
    {
        double u(double lo, double hi) => lo + rng.NextDouble() * (hi - lo);
        bool varyEr = run.AutotuneUseSynthesisBaseline;
        double dLo = Math.Min(run.AutotuneSwirlChamberDiameterScaleMin, run.AutotuneSwirlChamberDiameterScaleMax);
        double dHi = Math.Max(run.AutotuneSwirlChamberDiameterScaleMin, run.AutotuneSwirlChamberDiameterScaleMax);
        double lLo = Math.Min(run.AutotuneSwirlChamberLengthScaleMin, run.AutotuneSwirlChamberLengthScaleMax);
        double lHi = Math.Max(run.AutotuneSwirlChamberLengthScaleMin, run.AutotuneSwirlChamberLengthScaleMax);
        double yaw = run.LockInjectorYawTo90Degrees ? 90.0 : u(45.0, 80.0);
        double pitch = run.AutotuneVaryPitch ? u(6.0, 16.0) : template.InjectorPitchAngleDeg;

        return new Knobs(
            chamberD: run.UseDerivedSwirlChamberDiameter && !run.AllowAutotuneDirectChamberDiameterOverride
                ? 1.0
                : u(dLo, dHi),
            chamberL: u(lLo, lHi),
            inlet: u(0.86, 1.24),
            exit: u(0.82, 1.26),
            expAngle: u(0.74, 1.26),
            expLength: u(0.62, 1.48),
            statorAngle: u(0.78, 1.22),
            axialPositionScale: u(0.70, 1.32),
            synthesisTargetEr: varyEr ? u(0.22, 0.62) : NozzleGeometrySynthesis.DefaultTargetEntrainmentRatio,
            injectorYawDeg: yaw,
            injectorPitchDeg: pitch);
    }

    private static Knobs SampleKnobsFromBand(Random rng, in AutotunePerturbationBand b, RunConfiguration run, NozzleDesignInputs angleRef)
    {
        double um(double spread)
        {
            double s = Math.Clamp(spread, 0.008, 0.48);
            double lo = 1.0 - s;
            double hi = 1.0 + s;
            return lo + rng.NextDouble() * (hi - lo);
        }

        double yaw = run.LockInjectorYawTo90Degrees
            ? 90.0
            : Math.Clamp(
                angleRef.InjectorYawAngleDeg + (rng.NextDouble() * 2.0 - 1.0) * b.InjectorYawDegHalfSpan,
                45.0,
                80.0);
        double pitch = run.AutotuneVaryPitch
            ? Math.Clamp(
                angleRef.InjectorPitchAngleDeg + (rng.NextDouble() * 2.0 - 1.0) * b.InjectorPitchDegHalfSpan,
                4.0,
                18.0)
            : angleRef.InjectorPitchAngleDeg;

        double erCenter = NozzleGeometrySynthesis.DefaultTargetEntrainmentRatio;
        double er = run.AutotuneUseSynthesisBaseline
            ? Math.Clamp(erCenter + (rng.NextDouble() * 2.0 - 1.0) * b.SynthesisTargetErHalfSpan, 0.18, 0.68)
            : erCenter;

        return new Knobs(
            chamberD: run.UseDerivedSwirlChamberDiameter && !run.AllowAutotuneDirectChamberDiameterOverride
                ? 1.0
                : um(b.ChamberDiameterSpread),
            chamberL: um(b.ChamberLengthSpread),
            inlet: um(b.InletSpread),
            exit: um(b.ExitSpread),
            expAngle: um(b.ExpanderAngleSpread),
            expLength: um(b.ExpanderLengthSpread),
            statorAngle: um(b.StatorAngleSpread),
            axialPositionScale: um(b.InjectorAxialSpread),
            synthesisTargetEr: er,
            injectorYawDeg: yaw,
            injectorPitchDeg: pitch);
    }

    private static NozzleDesignInputs ApplyKnobs(NozzleDesignInputs b, in Knobs k, RunConfiguration run)
    {
        double lenCap = Math.Clamp(run.AutotuneSwirlChamberLengthMaxMm, 40.0, 220.0);
        double dScale = k.ChamberD;
        if (run.UseDerivedSwirlChamberDiameter && !run.AllowAutotuneDirectChamberDiameterOverride)
            dScale = 1.0;
        double dCh = Math.Clamp(b.SwirlChamberDiameterMm * dScale, 35.0, 220.0);
        double lCh = Math.Clamp(b.SwirlChamberLengthMm * k.ChamberL, 28.0, lenCap);
        double dIn = Math.Clamp(b.InletDiameterMm * k.Inlet, Math.Max(dCh * 1.0, 30.0), dCh * 1.65);
        double dEx = Math.Clamp(b.ExitDiameterMm * k.Exit, dCh * 1.02, dCh * 1.75);
        double ang = Math.Clamp(b.ExpanderHalfAngleDeg * k.ExpAngle, 3.5, 14.0);
        double lEx = Math.Clamp(b.ExpanderLengthMm * k.ExpLength, 25.0, 240.0);
        double st = Math.Clamp(b.StatorVaneAngleDeg * k.StatorAngle, 14.0, 58.0);
        double ax = Math.Clamp(b.InjectorAxialPositionRatio * k.AxialPositionScale, 0.08, 0.92);
        double yaw = run.LockInjectorYawTo90Degrees
            ? 90.0
            : Math.Clamp(k.InjectorYawDeg, 45.0, 80.0);
        double pitch = Math.Clamp(k.InjectorPitchDeg, 4.0, 18.0);

        return new NozzleDesignInputs
        {
            InletDiameterMm = dIn,
            SwirlChamberDiameterMm = dCh,
            SwirlChamberLengthMm = lCh,
            InjectorAxialPositionRatio = ax,
            TotalInjectorAreaMm2 = b.TotalInjectorAreaMm2,
            InjectorCount = b.InjectorCount,
            InjectorWidthMm = b.InjectorWidthMm,
            InjectorHeightMm = b.InjectorHeightMm,
            InjectorYawAngleDeg = yaw,
            InjectorPitchAngleDeg = pitch,
            InjectorRollAngleDeg = b.InjectorRollAngleDeg,
            ExpanderLengthMm = lEx,
            ExpanderHalfAngleDeg = ang,
            ExitDiameterMm = dEx,
            StatorVaneAngleDeg = st,
            StatorVaneCount = b.StatorVaneCount,
            StatorHubDiameterMm = b.StatorHubDiameterMm,
            StatorAxialLengthMm = b.StatorAxialLengthMm,
            StatorBladeChordMm = b.StatorBladeChordMm,
            WallThicknessMm = b.WallThicknessMm
        };
    }

    private static NozzleDesignInputs CloneDesign(NozzleDesignInputs d) => new()
    {
        InletDiameterMm = d.InletDiameterMm,
        SwirlChamberDiameterMm = d.SwirlChamberDiameterMm,
        SwirlChamberLengthMm = d.SwirlChamberLengthMm,
        InjectorAxialPositionRatio = d.InjectorAxialPositionRatio,
        TotalInjectorAreaMm2 = d.TotalInjectorAreaMm2,
        InjectorCount = d.InjectorCount,
        InjectorWidthMm = d.InjectorWidthMm,
        InjectorHeightMm = d.InjectorHeightMm,
        InjectorYawAngleDeg = d.InjectorYawAngleDeg,
        InjectorPitchAngleDeg = d.InjectorPitchAngleDeg,
        InjectorRollAngleDeg = d.InjectorRollAngleDeg,
        ExpanderLengthMm = d.ExpanderLengthMm,
        ExpanderHalfAngleDeg = d.ExpanderHalfAngleDeg,
        ExitDiameterMm = d.ExitDiameterMm,
        StatorVaneAngleDeg = d.StatorVaneAngleDeg,
        StatorVaneCount = d.StatorVaneCount,
        StatorHubDiameterMm = d.StatorHubDiameterMm,
        StatorAxialLengthMm = d.StatorAxialLengthMm,
        StatorBladeChordMm = d.StatorBladeChordMm,
        WallThicknessMm = d.WallThicknessMm
    };
}
