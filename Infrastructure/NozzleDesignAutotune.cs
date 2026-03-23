using System;
using PicoGK_Run.Core;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Infrastructure;

/// <summary>
/// Reproducible random search over bounded geometry + injector yaw (45–80°) and pitch (conservative band); SI forward model only — no voxels per trial.
/// <para>
/// <b>Swirl segment (voxels):</b> axial length = <see cref="NozzleDesignInputs.SwirlChamberLengthMm"/>; inner diameter = <see cref="NozzleDesignInputs.SwirlChamberDiameterMm"/>.
/// With <see cref="RunConfiguration.UseAutotune"/> true, <b>both</b> are search variables: each trial applies random multipliers in
/// <see cref="RunConfiguration.AutotuneSwirlChamberDiameterScaleMin"/>/<see cref="RunConfiguration.AutotuneSwirlChamberDiameterScaleMax"/> and
/// length scales in <see cref="RunConfiguration.AutotuneSwirlChamberLengthScaleMin"/>/<see cref="RunConfiguration.AutotuneSwirlChamberLengthScaleMax"/>,
/// then clamps length to <see cref="RunConfiguration.AutotuneSwirlChamberLengthMaxMm"/>. Without autotune, values come from the hand template or synthesis only.
/// </para>
/// </summary>
public static class NozzleDesignAutotune
{
    public sealed class Result
    {
        public NozzleDesignInputs BestSeedDesign { get; init; } = null!;
        public double BestScore { get; init; }
        public int TrialsUsed { get; init; }
        public FlowTuneEvaluation BaselineEvaluation { get; init; } = null!;
        /// <summary>Best candidate with <see cref="FlowTuneEvaluation.Score"/> set.</summary>
        public FlowTuneEvaluation? BestEvaluation { get; init; }
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
        /// <summary>Absolute injector yaw [deg], typically 45–80 (reduces swirl vs 80° baseline).</summary>
        public readonly double InjectorYawDeg;
        /// <summary>Absolute injector pitch [deg], conservative band.</summary>
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

    public static Result FindBestSeed(SourceInputs source, NozzleDesignInputs template, RunConfiguration run)
    {
        int trials = Math.Clamp(run.AutotuneTrials, 20, 5000);
        var rng = new Random(run.AutotuneRandomSeed);

        NozzleDesignInputs BaselineSeed()
        {
            if (run.AutotuneUseSynthesisBaseline)
                return NozzleGeometrySynthesis.Synthesize(source, template, NozzleGeometrySynthesis.DefaultTargetEntrainmentRatio);
            return CloneDesign(template);
        }

        NozzleDesignInputs baseSeed = BaselineSeed();
        FlowTuneEvaluation baselineEval = NozzleFlowCompositionRoot.EvaluateDesignForTuning(source, baseSeed, run);

        double bestScore = double.NegativeInfinity;
        NozzleDesignInputs bestSeed = baseSeed;
        FlowTuneEvaluation? bestEv = null;

        for (int i = 0; i < trials; i++)
        {
            Knobs k = SampleKnobs(rng, run);
            NozzleDesignInputs candidate = run.AutotuneUseSynthesisBaseline
                ? ApplyKnobs(NozzleGeometrySynthesis.Synthesize(source, template, k.SynthesisTargetEr), k, run)
                : ApplyKnobs(CloneDesign(template), k, run);

            FlowTuneEvaluation ev = NozzleFlowCompositionRoot.EvaluateDesignForTuning(source, candidate, run);
            if (ev.HasDesignError)
                continue;

            double score = AutotuneScoring.ComputeScore(ev, baselineEval, run);
            ev = WithScore(ev, score);

            if (score > bestScore)
            {
                bestScore = score;
                bestSeed = candidate;
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
        CoreMassFlowKgS = e.CoreMassFlowKgS
    };

    private static Knobs SampleKnobs(Random rng, RunConfiguration run)
    {
        double u(double lo, double hi) => lo + rng.NextDouble() * (hi - lo);
        bool varyEr = run.AutotuneUseSynthesisBaseline;
        double dLo = Math.Min(run.AutotuneSwirlChamberDiameterScaleMin, run.AutotuneSwirlChamberDiameterScaleMax);
        double dHi = Math.Max(run.AutotuneSwirlChamberDiameterScaleMin, run.AutotuneSwirlChamberDiameterScaleMax);
        double lLo = Math.Min(run.AutotuneSwirlChamberLengthScaleMin, run.AutotuneSwirlChamberLengthScaleMax);
        double lHi = Math.Max(run.AutotuneSwirlChamberLengthScaleMin, run.AutotuneSwirlChamberLengthScaleMax);
        // Injector angles: yaw strongly affects |Vt|/|Va|; pitch nudged in a narrow band.
        double yaw = u(45.0, 80.0);
        double pitch = u(6.0, 16.0);

        return new Knobs(
            chamberD: u(dLo, dHi),
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

    private static NozzleDesignInputs ApplyKnobs(NozzleDesignInputs b, in Knobs k, RunConfiguration run)
    {
        double lenCap = Math.Clamp(run.AutotuneSwirlChamberLengthMaxMm, 40.0, 220.0);
        double dCh = Math.Clamp(b.SwirlChamberDiameterMm * k.ChamberD, 35.0, 220.0);
        double lCh = Math.Clamp(b.SwirlChamberLengthMm * k.ChamberL, 28.0, lenCap);
        double dIn = Math.Clamp(b.InletDiameterMm * k.Inlet, Math.Max(dCh * 1.0, 30.0), dCh * 1.65);
        double dEx = Math.Clamp(b.ExitDiameterMm * k.Exit, dCh * 1.02, dCh * 1.75);
        double ang = Math.Clamp(b.ExpanderHalfAngleDeg * k.ExpAngle, 3.5, 14.0);
        double lEx = Math.Clamp(b.ExpanderLengthMm * k.ExpLength, 25.0, 240.0);
        double st = Math.Clamp(b.StatorVaneAngleDeg * k.StatorAngle, 14.0, 58.0);
        double ax = Math.Clamp(b.InjectorAxialPositionRatio * k.AxialPositionScale, 0.08, 0.92);
        double yaw = Math.Clamp(k.InjectorYawDeg, 45.0, 80.0);
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
