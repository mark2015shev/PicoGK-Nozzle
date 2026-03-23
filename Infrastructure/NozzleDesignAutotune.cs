using System;
using PicoGK_Run.Core;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Infrastructure;

/// <summary>
/// Bounded random search over geometric scales on a synthesis (or template) baseline.
/// Objective: weighted entrainment ratio + net thrust vs source-only thrust (1-D SI — validate in CFD).
/// </summary>
public static class NozzleDesignAutotune
{
    public sealed class Result
    {
        public NozzleDesignInputs BestSeedDesign { get; init; } = null!;
        public double BestScore { get; init; }
        public int TrialsUsed { get; init; }
        public FlowTuneEvaluation? BaselineEvaluation { get; init; }
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
        public readonly double SynthesisTargetEr;

        public Knobs(
            double chamberD,
            double chamberL,
            double inlet,
            double exit,
            double expAngle,
            double expLength,
            double statorAngle,
            double synthesisTargetEr)
        {
            ChamberD = chamberD;
            ChamberL = chamberL;
            Inlet = inlet;
            Exit = exit;
            ExpAngle = expAngle;
            ExpLength = expLength;
            StatorAngle = statorAngle;
            SynthesisTargetEr = synthesisTargetEr;
        }
    }

    public static Result FindBestSeed(SourceInputs source, NozzleDesignInputs template, RunConfiguration run)
    {
        int trials = Math.Clamp(run.AutotuneTrials, 20, 5000);
        double wE = Math.Clamp(run.AutotuneWeightEntrainment, 0.05, 0.95);
        double wT = Math.Clamp(run.AutotuneWeightThrust, 0.05, 0.95);
        double sumW = wE + wT;
        wE /= sumW;
        wT /= sumW;

        var rng = new Random(run.AutotuneRandomSeed);

        NozzleDesignInputs Baseline()
        {
            if (run.AutotuneUseSynthesisBaseline)
                return NozzleGeometrySynthesis.Synthesize(source, template, NozzleGeometrySynthesis.DefaultTargetEntrainmentRatio);
            return CloneDesign(template);
        }

        NozzleDesignInputs baseDesign = Baseline();
        FlowTuneEvaluation baselineEval = NozzleFlowCompositionRoot.EvaluateDesignForTuning(source, baseDesign);
        double f0 = Math.Max(baselineEval.SourceOnlyThrustN, 1.0);
        double er0 = Math.Max(baselineEval.EntrainmentRatio, 0.02);

        double bestScore = double.NegativeInfinity;
        NozzleDesignInputs best = baseDesign;

        for (int i = 0; i < trials; i++)
        {
            Knobs k = SampleKnobs(rng, run.AutotuneUseSynthesisBaseline);
            NozzleDesignInputs candidate = run.AutotuneUseSynthesisBaseline
                ? ApplyKnobs(NozzleGeometrySynthesis.Synthesize(source, template, k.SynthesisTargetEr), k)
                : ApplyKnobs(CloneDesign(template), k);

            FlowTuneEvaluation ev = NozzleFlowCompositionRoot.EvaluateDesignForTuning(source, candidate);
            if (ev.HasDesignErrors)
                continue;

            double erN = ev.EntrainmentRatio / er0;
            double thN = ev.NetThrustN / f0;
            double thrustFloor = thN >= 0.88 ? 1.0 : 0.35 + 0.65 * (thN / 0.88);
            double score = wE * erN + wT * thN * thrustFloor - 0.025 * ev.HealthIssueCount;

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (bestScore <= double.NegativeInfinity)
        {
            best = baseDesign;
            bestScore = wE * (baselineEval.EntrainmentRatio / er0) + wT * (baselineEval.NetThrustN / f0);
        }

        return new Result
        {
            BestSeedDesign = best,
            BestScore = bestScore,
            TrialsUsed = trials,
            BaselineEvaluation = baselineEval
        };
    }

    private static Knobs SampleKnobs(Random rng, bool varyEr)
    {
        double u(double lo, double hi) => lo + rng.NextDouble() * (hi - lo);
        return new Knobs(
            chamberD: u(0.90, 1.10),
            chamberL: u(0.80, 1.25),
            inlet: u(0.94, 1.14),
            exit: u(0.90, 1.18),
            expAngle: u(0.82, 1.15),
            expLength: u(0.72, 1.38),
            statorAngle: u(0.86, 1.14),
            synthesisTargetEr: varyEr ? u(0.22, 0.62) : NozzleGeometrySynthesis.DefaultTargetEntrainmentRatio);
    }

    private static NozzleDesignInputs ApplyKnobs(NozzleDesignInputs b, in Knobs k)
    {
        double dCh = Math.Clamp(b.SwirlChamberDiameterMm * k.ChamberD, 35.0, 220.0);
        double lCh = Math.Clamp(b.SwirlChamberLengthMm * k.ChamberL, 28.0, 180.0);
        double dIn = Math.Clamp(b.InletDiameterMm * k.Inlet, Math.Max(dCh * 1.0, 30.0), dCh * 1.65);
        double dEx = Math.Clamp(b.ExitDiameterMm * k.Exit, dCh * 1.02, dCh * 1.75);
        double ang = Math.Clamp(b.ExpanderHalfAngleDeg * k.ExpAngle, 3.5, 14.0);
        double lEx = Math.Clamp(b.ExpanderLengthMm * k.ExpLength, 25.0, 240.0);
        double st = Math.Clamp(b.StatorVaneAngleDeg * k.StatorAngle, 14.0, 58.0);

        return new NozzleDesignInputs
        {
            InletDiameterMm = dIn,
            SwirlChamberDiameterMm = dCh,
            SwirlChamberLengthMm = lCh,
            InjectorAxialPositionRatio = b.InjectorAxialPositionRatio,
            TotalInjectorAreaMm2 = b.TotalInjectorAreaMm2,
            InjectorCount = b.InjectorCount,
            InjectorWidthMm = b.InjectorWidthMm,
            InjectorHeightMm = b.InjectorHeightMm,
            InjectorYawAngleDeg = b.InjectorYawAngleDeg,
            InjectorPitchAngleDeg = b.InjectorPitchAngleDeg,
            InjectorRollAngleDeg = b.InjectorRollAngleDeg,
            ExpanderLengthMm = lEx,
            ExpanderHalfAngleDeg = ang,
            ExitDiameterMm = dEx,
            StatorVaneAngleDeg = st,
            StatorVaneCount = b.StatorVaneCount,
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
        WallThicknessMm = d.WallThicknessMm
    };
}
