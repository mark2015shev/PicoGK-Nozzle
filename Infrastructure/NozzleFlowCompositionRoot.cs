using System;
using System.Collections.Generic;
using PicoGK;
using PicoGK_Run.Core;
using PicoGK_Run.Geometry;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;
using PicoGK_Run.Physics.JetTrajectory;
using PicoGK_Run.Infrastructure.Pipeline;
using PicoGK_Run.Infrastructure.Services;

namespace PicoGK_Run.Infrastructure;

/// <summary>Outputs from <see cref="NozzleFlowCompositionRoot.EvaluateSiPathForValidation"/> (SI only).</summary>
internal sealed record SiPathValidationPack(
    NozzleSolvedState Solved,
    SiFlowDiagnostics SiDiag,
    NozzleCriticalRatiosSnapshot CriticalRatios,
    IReadOnlyList<string> HealthMessages);

/// <summary>
/// Wires SI physics → design result → mm geometry → <see cref="PipelineRunResult"/>.
/// </summary>
public static class NozzleFlowCompositionRoot
{
    /// <summary>
    /// Same prepare → <c>SolveSiPath</c> (+ optional continuity) as <see cref="Run"/> — no voxels, no viewer, no console summary.
    /// </summary>
    internal static FlowTuneEvaluation EvaluateDesignForTuning(
        SourceInputs source,
        NozzleDesignInputs candidateDesign,
        RunConfiguration run)
    {
        UnifiedEvaluationResult u = UnifiedPhysicsEvaluationService.EvaluateCandidateUnified(
            source, candidateDesign, run, NozzleEvaluationMode.TuningFast);
        return UnifiedPhysicsEvaluationService.ToFlowTuneEvaluation(u);
    }

    /// <summary>Authoritative handoff: synthesis / derived-bore reference / template — identical for tuning and final.</summary>
    public static PreparedNozzleDesignHandoff PrepareActiveDesignForSolve(
        SourceInputs source,
        NozzleDesignInputs seedDesign,
        RunConfiguration run) =>
        DesignPreparationService.PrepareActiveDesignForSolve(source, seedDesign, run);

    /// <summary>Single evaluation path shared by autotune and final run (voxels excluded).</summary>
    public static UnifiedEvaluationResult EvaluateCandidateUnified(
        SourceInputs source,
        NozzleDesignInputs seedDesign,
        RunConfiguration run,
        NozzleEvaluationMode mode) =>
        UnifiedPhysicsEvaluationService.EvaluateCandidateUnified(source, seedDesign, run, mode);

    public static PipelineRunResult Run(NozzleInput input, bool showInViewer)
    {
        PipelineProfiler.ResetSession(input.Run.EnablePipelineProfiling);

        double chamberDiameterInputMm = input.Design.SwirlChamberDiameterMm;

        PreparedNozzleDesignHandoff prep;
        using (PipelineProfiler.Stage("pipeline.synthesis"))
            prep = DesignPreparationService.PrepareActiveDesignForSolve(input.Source, input.Design, input.Run);
        SwirlChamberSizingModel.SizingDiagnostics? chamberSizing = prep.ChamberSizing;

        SiPathSolveResult path;
        using (PipelineProfiler.Stage("physics.solveSiPath"))
            path = PhysicsSiPathService.Solve(input.Source, prep.ActiveDesignAfterSynthesis, input.Run);

        GeometryContinuityReport? geomContinuity;
        using (PipelineProfiler.Stage("geometry.continuity"))
        {
            geomContinuity = GeometryConsistencyService.EvaluateDrivenDesign(
                path.DrivenDesign,
                input.Run.RunGeometryContinuityCheck,
                input.Run);
        }

        UnifiedEvaluationResult unified = UnifiedPhysicsEvaluationService.BuildUnifiedAfterSolve(prep, path, geomContinuity, input.Run);

        JetTrajectoryResult? jetTrajectory = null;
        Voxels? jetTrajectoryDebug = null;
        if (input.Run.UsePhysicsTracedJetTrajectory && unified.PhysicsStages?.Stage1Injector != null)
        {
            using (PipelineProfiler.Stage("physics.jetTrajectory"))
            {
                JetTrajectorySolveService.Outcome jetOutcome = JetTrajectorySolveService.TrySolve(
                    unified,
                    input,
                    msg =>
                    {
                        try
                        {
                            Library.Log("JET TRAJECTORY: solve failed — " + msg);
                        }
                        catch
                        {
                        }

                        ConsoleReportColor.WriteError("JET TRAJECTORY: solve failed — " + msg);
                    });
                jetTrajectory = jetOutcome.Trajectory;
                jetTrajectoryDebug = jetOutcome.DebugVoxels;
            }
        }

        void LogJetTrajectoryComparison(string line)
        {
            try
            {
                Library.Log(line);
            }
            catch
            {
            }

            ConsoleReportColor.WriteClassifiedLine(line);
        }

        JetTrajectoryResult.LogComparisonToLibrary(jetTrajectory, input.Run, LogJetTrajectoryComparison);

        // Per-segment timings live in NozzleGeometryBuilder (sum ≈ full voxel assembly; no outer wrapper — avoids double-count in TOTAL).
        NozzleGeometryResult geometry = GeometryBuildService.Build(
            unified.DrivenDesign,
            unified.Solved,
            input.Run,
            jetTrajectoryDebug);

        if (showInViewer)
        {
            using (PipelineProfiler.Stage("viewer.display"))
                AppPipeline.DisplayGeometryInViewer(geometry);
        }

        using (PipelineProfiler.Stage("reporting.consoleSummary"))
            PipelineReportingService.PrintSiFlowSummary(
                unified.InletState,
                unified.DesignResult,
                unified.SiDiagnostics,
                new DiagnosticsPipelineOptions(input.Run).SiVerbosityLevel);

        var warnings = new List<string>
        {
            "SI path: compressible entrainment march + first-order stator/expander bookkeeping (not CFD)."
        };
        if (input.Run.UsePhysicsInformedGeometry)
        {
            warnings.Add(
                input.Run.UseDerivedSwirlChamberDiameter
                    ? "Geometry pre-sized by NozzleGeometrySynthesis with entrainment-derived swirl chamber bore (continuity sizing — not CFD)."
                    : "Geometry pre-sized by NozzleGeometrySynthesis (jet×swirl/ER approximate bore rules; not CFD).");
        }

        if (chamberSizing != null)
        {
            foreach (string cw in chamberSizing.Warnings)
                warnings.Add("Chamber sizing: " + cw);
        }

        if (!input.Run.UsePhysicsInformedGeometry
            && input.Run.UseDerivedSwirlChamberDiameter
            && chamberSizing != null
            && chamberSizing.Mode == SwirlChamberSizingModel.DiameterMode.ReferenceDerivedAtConfiguredTargetEr)
        {
            double dAct = chamberDiameterInputMm;
            double dRef = chamberSizing.ChamberDiameterTargetMm;
            if (Math.Abs(dAct - dRef) > 1.5)
            {
                warnings.Add(
                    $"Chamber bore trace: actual input/seed D={dAct:F2} mm vs reference derived at GeometrySynthesisTargetEntrainmentRatio ({input.Run.GeometrySynthesisTargetEntrainmentRatio:F3}) D≈{dRef:F2} mm — autotune trials use per-trial ER; this is not CFD.");
            }
        }

        if (geomContinuity is { IsAcceptable: false })
            warnings.AddRange(geomContinuity.Issues);
        warnings.AddRange(unified.HealthMessages);

        NozzleInput effectiveInput = new NozzleInput(input.Source, unified.DrivenDesign, input.Run);
        PipelineProfileReport? profile = PipelineProfiler.TryBuildReport();

        string declaredSource = !input.Run.UsePhysicsInformedGeometry
            ? (input.Run.UseDerivedSwirlChamberDiameter
                ? "input_seed (no synthesis this run; see reference derived at configured target ER in sizing section)"
                : "template_or_hand_seed")
            : (input.Run.UseDerivedSwirlChamberDiameter
                ? "entrainment-derived via NozzleGeometrySynthesis (this run)"
                : "synthesis-approximate-bore (jet×swirl×ER)");

        double? refDerivedMm = input.Run.UseDerivedSwirlChamberDiameter ? chamberSizing?.ChamberDiameterTargetMm : null;

        var chamberAudit = new ChamberDiameterAudit
        {
            InputDesignMm = chamberDiameterInputMm,
            AfterSynthesisMm = prep.ActiveDesignAfterSynthesis.SwirlChamberDiameterMm,
            PreSiSolveMm = prep.ActiveDesignAfterSynthesis.SwirlChamberDiameterMm,
            PostFlowDrivenMm = unified.DrivenDesign.SwirlChamberDiameterMm,
            UsedForVoxelBuildMm = unified.DrivenDesign.SwirlChamberDiameterMm,
            DeclaredPrimarySource = declaredSource,
            AutotuneSearchUsedDerivedBoreSizing = false,
            AutotuneAppliedDirectChamberScale = false,
            ReferenceDerivedBoreAtConfiguredTargetErMm = refDerivedMm,
            Footnote = !input.Run.UsePhysicsInformedGeometry
                ? "UsePhysicsInformedGeometry was false — voxel bore equals input design (e.g. autotune winning seed). Enable synthesis on the final pass only if you intend to re-run sizing (may overwrite tuned seed)."
                : null
        };

        if (input.Run.UsePhysicsTracedJetTrajectory && jetTrajectory == null && unified.PhysicsStages?.Stage1Injector != null)
            warnings.Add("JET TRAJECTORY: UsePhysicsTracedJetTrajectory was true but the trajectory solve did not produce a result (see log).");

        return new PipelineRunResult(
            effectiveInput,
            unified.Solved,
            geometry,
            warnings,
            unified.SiDiagnostics,
            unified.CriticalRatios,
            autotune: null,
            physicsStages: unified.PhysicsStages,
            geometryContinuity: geomContinuity,
            performanceProfile: profile,
            chamberSizing: chamberSizing,
            chamberDiameterAudit: chamberAudit,
            jetTrajectory: jetTrajectory);
    }

    /// <summary>
    /// Coupled SI solve only (no voxels/viewer) — for validation sweeps and tooling; same path as tuning eval.
    /// </summary>
    internal static SiPathValidationPack EvaluateSiPathForValidation(
        SourceInputs source,
        NozzleDesignInputs design,
        RunConfiguration? run = null)
    {
        SiPathSolveResult r = PhysicsSiPathService.Solve(source, design, run);
        return new SiPathValidationPack(r.Solved, r.SiDiag, r.CriticalRatios, r.HealthMessages);
    }
}