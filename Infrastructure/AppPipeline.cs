using System;
using System.Collections.Generic;
using System.Diagnostics;
using PicoGK;
using PicoGK_Run.Core;
using PicoGK_Run.Geometry;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Infrastructure;

internal sealed class AppPipeline
{
    public PipelineRunResult Run(NozzleInput input)
    {
        if (!input.Run.UseAutotune)
            return NozzleFlowCompositionRoot.Run(input, input.Run.ShowInViewer);

        NozzleDesignInputs baselineTemplate = CloneDesignForLog(input.Design);

        var autotuneSw = Stopwatch.StartNew();
        NozzleDesignAutotune.Result tune = NozzleDesignAutotune.FindBestSeed(input.Source, input.Design, input.Run);
        autotuneSw.Stop();
        Library.Log(
            $"Autotune wall time: {autotuneSw.Elapsed.TotalMilliseconds:F0} ms (SI-only trials; separate from final-run PipelineProfiler session).");

        if (!string.IsNullOrEmpty(tune.CoarseToFineLog))
        {
            foreach (string line in tune.CoarseToFineLog.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                Library.Log(line.TrimEnd('\r'));
        }

        LogAutotuneBeforeFinalRun(tune, baselineTemplate, input.Run);
        LogAutotuneGeometryDeltaToConsole(baselineTemplate, tune.BestSeedDesign);

        bool searchDerivedBore = input.Run.UseDerivedSwirlChamberDiameter && input.Run.AutotuneUseSynthesisBaseline;
        bool searchChamberOverride = input.Run.AllowAutotuneDirectChamberDiameterOverride;

        bool finalPassDerivedBore = false;
        NozzleDesignInputs seedForFinal = tune.BestSeedDesign;
        bool skipDerivedBoreFinalize = input.Run.AutotuneStrategy == AutotuneStrategy.PhysicsControlledFiveParameter
            || input.Run.PhysicsAutotunePreserveWinningChamberDiameter;

        if (input.Run.UseDerivedSwirlChamberDiameter && input.Run.AutotuneFinalizeApplyEntrainmentDerivedChamberBore && !skipDerivedBoreFinalize)
        {
            finalPassDerivedBore = true;
            double dBefore = seedForFinal.SwirlChamberDiameterMm;
            SwirlChamberSizingModel.SizingDiagnostics dz = SwirlChamberSizingModel.ComputeDerived(
                input.Source,
                seedForFinal,
                input.Run.GeometrySynthesisTargetEntrainmentRatio,
                input.Run);
            seedForFinal = WithSwirlChamberBoreMm(seedForFinal, dz.ChamberDiameterTargetMm);
            if (Math.Abs(dBefore - dz.ChamberDiameterTargetMm) > 0.2)
            {
                Library.Log(
                    $"Autotune finalize: chamber bore {dBefore:F2} mm → entrainment-derived {dz.ChamberDiameterTargetMm:F2} mm at GeometrySynthesisTargetEntrainmentRatio={input.Run.GeometrySynthesisTargetEntrainmentRatio:F3} (continuity model; not CFD).");
                foreach (string line in dz.Warnings)
                    Library.Log("  Chamber finalize: " + line);
            }
        }

        NozzleInput work = new NozzleInput(input.Source, seedForFinal, input.Run.AfterAutotune());

        PipelineRunResult pr = NozzleFlowCompositionRoot.Run(work, work.Run.ShowInViewer);

        ChamberDiameterAudit? auditMerged = MergeAutotuneIntoChamberAudit(
            pr.ChamberDiameterAudit,
            searchDerivedBore,
            finalPassDerivedBore,
            searchChamberOverride);

        var summary = new AutotuneRunSummary
        {
            Strategy = input.Run.AutotuneStrategy,
            Trials = tune.TrialsUsed,
            BestScore = tune.BestScore,
            BaselineTemplateDesign = baselineTemplate,
            WinningSeedDesign = seedForFinal,
            CoarseToFineLog = tune.CoarseToFineLog,
            SearchUsedEntrainmentDerivedBoreSizing = searchDerivedBore,
            SearchAllowedDirectChamberDiameterOverride = searchChamberOverride,
            FinalPassAppliedEntrainmentDerivedChamberBore = finalPassDerivedBore
        };

        var w = new List<string>
        {
            $"Autotune: {tune.TrialsUsed} SI-only evaluations, best score {tune.BestScore:F4} (E/T/V/P pos.; breakdown/separation/loss/ejector/low-axial penalties — see RunConfiguration). Pre-CFD — validate in CFD."
        };
        w.AddRange(pr.SolverWarnings);

        return new PipelineRunResult(
            pr.Input,
            pr.Solved,
            pr.Geometry,
            w,
            pr.SiFlow,
            pr.CriticalRatios,
            summary,
            pr.PhysicsStages,
            pr.GeometryContinuity,
            pr.PerformanceProfile,
            pr.ChamberSizing,
            auditMerged);
    }

    private static ChamberDiameterAudit? MergeAutotuneIntoChamberAudit(
        ChamberDiameterAudit? audit,
        bool searchUsedDerivedBore,
        bool finalPassAppliedDerivedBore,
        bool searchAllowedDirectChamberOverride)
    {
        if (audit == null)
            return null;
        string? fn = audit.Footnote;
        if (finalPassAppliedDerivedBore)
        {
            const string extra =
                "Autotune: winning-seed bore was set from entrainment-derived model at GeometrySynthesisTargetEntrainmentRatio before the final SI+voxel pass (first-order; not CFD).";
            fn = string.IsNullOrEmpty(fn) ? extra : fn + " " + extra;
        }

        return new ChamberDiameterAudit
        {
            InputDesignMm = audit.InputDesignMm,
            AfterSynthesisMm = audit.AfterSynthesisMm,
            PreSiSolveMm = audit.PreSiSolveMm,
            PostFlowDrivenMm = audit.PostFlowDrivenMm,
            UsedForVoxelBuildMm = audit.UsedForVoxelBuildMm,
            DeclaredPrimarySource = audit.DeclaredPrimarySource,
            AutotuneSearchUsedDerivedBoreSizing = searchUsedDerivedBore,
            AutotuneAppliedDirectChamberScale = searchAllowedDirectChamberOverride,
            ReferenceDerivedBoreAtConfiguredTargetErMm = audit.ReferenceDerivedBoreAtConfiguredTargetErMm,
            Footnote = fn
        };
    }

    private static NozzleDesignInputs WithSwirlChamberBoreMm(NozzleDesignInputs d, double swirlChamberDiameterMm) => new()
    {
        InletDiameterMm = d.InletDiameterMm,
        SwirlChamberDiameterMm = swirlChamberDiameterMm,
        SwirlChamberLengthMm = d.SwirlChamberLengthMm,
        InjectorAxialPositionRatio = d.InjectorAxialPositionRatio,
        InjectorUpstreamGuardLengthMm = d.InjectorUpstreamGuardLengthMm,
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

    /// <summary>Viewer group order/colors must stay aligned with <see cref="NozzleViewerGroupCatalog"/> (audit log references same IDs).</summary>
    internal static void DisplayGeometryInViewer(NozzleGeometryResult geometry)
    {
        Viewer viewer = Library.oViewer();
        int g = 1;
        foreach (NozzleViewerGroupCatalog.Entry e in NozzleViewerGroupCatalog.Ordered)
            AddSegment(viewer, ref g, VoxelsForCatalogProperty(geometry, e.NozzleGeometryResultProperty), e.ColorHex);
    }

    private static Voxels VoxelsForCatalogProperty(NozzleGeometryResult geometry, string propertyName) =>
        propertyName switch
        {
            nameof(NozzleGeometryResult.Inlet) => geometry.Inlet,
            nameof(NozzleGeometryResult.SwirlChamber) => geometry.SwirlChamber,
            nameof(NozzleGeometryResult.InjectorReferenceMarkers) => geometry.InjectorReferenceMarkers,
            nameof(NozzleGeometryResult.Expander) => geometry.Expander,
            nameof(NozzleGeometryResult.StatorSection) => geometry.StatorSection,
            nameof(NozzleGeometryResult.Exit) => geometry.Exit,
            _ => throw new ArgumentOutOfRangeException(nameof(propertyName), propertyName, "Unknown nozzle geometry property for viewer group.")
        };

    private static void LogAutotuneBeforeFinalRun(NozzleDesignAutotune.Result tune, NozzleDesignInputs baselineTemplate, RunConfiguration run)
    {
        Library.Log("=== Autotune enabled (SI search — pre-CFD) ===");
        Library.Log($"Autotune strategy: {run.AutotuneStrategy} (same coupled SI scoring path for all trials).");
        Library.Log($"Autotune: trial count (SI-only evals): {tune.TrialsUsed}");
        Library.Log($"Autotune: best composite score [-]:      {tune.BestScore:F4}");
        Library.Log("Autotune: winning seed geometry [mm / deg]:");
        LogDesignToLibrary(tune.BestSeedDesign);
        Library.Log($"Autotune: winning injector Yaw / Pitch [deg]: {tune.BestSeedDesign.InjectorYawAngleDeg:F2} / {tune.BestSeedDesign.InjectorPitchAngleDeg:F2}");

        if (run.AutotuneStrategy == AutotuneStrategy.PhysicsControlledFiveParameter && tune.BestGeometryGenome != null)
        {
            NozzleGeometryGenome baseG = NozzleGeometryGenome.FromDesignInputs(baselineTemplate);
            foreach (string line in NozzleGeometryGenomeDiagnostics.FormatAutotuneReport(
                         baseG,
                         tune.BestGeometryGenome,
                         run,
                         tune.PhysicsAutotuneBestDetail))
                Library.Log(line);
        }
    }

    private static void LogDesignToLibrary(NozzleDesignInputs d)
    {
        Library.Log($"  InletDiameterMm:            {d.InletDiameterMm:F2}");
        Library.Log($"  SwirlChamber D x L:         {d.SwirlChamberDiameterMm:F2} x {d.SwirlChamberLengthMm:F2}");
        Library.Log($"  InjectorAxialPositionRatio: {d.InjectorAxialPositionRatio:F3}");
        Library.Log($"  InjectorYaw / Pitch [deg]:  {d.InjectorYawAngleDeg:F2} / {d.InjectorPitchAngleDeg:F2}");
        Library.Log($"  ExpanderLength / HalfAngle: {d.ExpanderLengthMm:F2} / {d.ExpanderHalfAngleDeg:F2}");
        Library.Log($"  ExitDiameterMm:             {d.ExitDiameterMm:F2}");
        Library.Log($"  StatorVaneAngleDeg:         {d.StatorVaneAngleDeg:F2}");
        Library.Log($"  StatorHubDiameterMm:        {d.StatorHubDiameterMm:F2}  AxialLength: {d.StatorAxialLengthMm:F2}  BladeChord: {d.StatorBladeChordMm:F2}");
    }

    /// <summary>Proves at least one tuned knob moved vs the hand template (console — easy to spot in CI / terminal).</summary>
    private static void LogAutotuneGeometryDeltaToConsole(NozzleDesignInputs baseline, NozzleDesignInputs tuned)
    {
        const double eps = 0.02; // mm or deg; ratio uses absolute delta
        bool inlet = Math.Abs(tuned.InletDiameterMm - baseline.InletDiameterMm) > eps;
        bool chD = Math.Abs(tuned.SwirlChamberDiameterMm - baseline.SwirlChamberDiameterMm) > eps;
        bool chL = Math.Abs(tuned.SwirlChamberLengthMm - baseline.SwirlChamberLengthMm) > eps;
        bool exit = Math.Abs(tuned.ExitDiameterMm - baseline.ExitDiameterMm) > eps;
        bool exL = Math.Abs(tuned.ExpanderLengthMm - baseline.ExpanderLengthMm) > eps;
        bool exA = Math.Abs(tuned.ExpanderHalfAngleDeg - baseline.ExpanderHalfAngleDeg) > 0.05;
        bool st = Math.Abs(tuned.StatorVaneAngleDeg - baseline.StatorVaneAngleDeg) > 0.05;
        bool ax = Math.Abs(tuned.InjectorAxialPositionRatio - baseline.InjectorAxialPositionRatio) > 0.02;
        bool yaw = Math.Abs(tuned.InjectorYawAngleDeg - baseline.InjectorYawAngleDeg) > 0.05;
        bool pitch = Math.Abs(tuned.InjectorPitchAngleDeg - baseline.InjectorPitchAngleDeg) > 0.05;
        bool any = inlet || chD || chL || exit || exL || exA || st || ax || yaw || pitch;

        ConsoleStatusWriter.WriteLine("[Autotune] Geometry delta vs hand template: " + (any ? "YES (at least one knob changed)" : "NO — winner matches template within tolerance"), StatusLevel.Normal);
        if (!any)
            ConsoleReportColor.WriteWarning(
                "[Autotune] WARNING: widen search bounds or increase trials if you expected visible geometry changes.");
    }

    private static NozzleDesignInputs CloneDesignForLog(NozzleDesignInputs d) => new()
    {
        InletDiameterMm = d.InletDiameterMm,
        SwirlChamberDiameterMm = d.SwirlChamberDiameterMm,
        SwirlChamberLengthMm = d.SwirlChamberLengthMm,
        InjectorAxialPositionRatio = d.InjectorAxialPositionRatio,
        InjectorUpstreamGuardLengthMm = d.InjectorUpstreamGuardLengthMm,
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

    private static void AddSegment(Viewer viewer, ref int groupId, Voxels voxels, string hex)
    {
        viewer.Add(voxels, groupId);
        viewer.SetGroupMaterial(groupId, hex, NozzleViewerSegmentColors.Roughness, NozzleViewerSegmentColors.Metallic);
        groupId++;
    }
}
