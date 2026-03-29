using System;
using System.Collections.Generic;
using System.Diagnostics;
using PicoGK;
using PicoGK_Run.Core;
using PicoGK_Run.Geometry;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;
using PicoGK_Run.Infrastructure.Services;

namespace PicoGK_Run.Infrastructure;

internal sealed class AppPipeline
{
    public PipelineRunResult Run(NozzleInput input)
    {
        if (!input.Run.UseAutotune)
            return NozzleFlowCompositionRoot.Run(input, input.Run.ShowInViewer);

        NozzleDesignInputs baselineTemplate = AutotunePipelineReporting.CloneDesignForLog(input.Design);

        var autotuneSw = Stopwatch.StartNew();
        NozzleDesignAutotune.Result tune = NozzleDesignAutotune.FindBestSeed(input.Source, input.Design, input.Run);
        autotuneSw.Stop();
        AutotunePipelineReporting.LogWallTimeMs(autotuneSw.Elapsed.TotalMilliseconds);
        AutotunePipelineReporting.LogCoarseToFineLines(tune.CoarseToFineLog);

        AutotunePipelineReporting.LogBeforeFinalRun(tune, baselineTemplate, input.Run);
        AutotunePipelineReporting.LogGeometryDeltaToConsole(baselineTemplate, tune.BestSeedDesign);

        var autotuneOpts = new AutotunePipelineOptions(input.Run);
        bool searchDerivedBore = autotuneOpts.UseDerivedSwirlChamberDiameter && autotuneOpts.AutotuneUseSynthesisBaseline;
        bool searchChamberOverride = autotuneOpts.AllowAutotuneDirectChamberDiameterOverride;

        AutotuneFinalizationService.PostSearchSeedResult seedResult =
            AutotuneFinalizationService.ApplyOptionalEntrainmentDerivedChamberBoreAfterSearch(
                input.Source,
                tune.BestSeedDesign,
                input.Run);

        NozzleInput work = new NozzleInput(input.Source, seedResult.SeedForFinal, input.Run.AfterAutotune());

        PipelineRunResult pr = NozzleFlowCompositionRoot.Run(work, work.Run.ShowInViewer);

        ChamberDiameterAudit? auditMerged = AutotuneFinalizationService.MergeAutotuneIntoChamberAudit(
            pr.ChamberDiameterAudit,
            searchDerivedBore,
            seedResult.FinalPassAppliedEntrainmentDerivedChamberBore,
            searchChamberOverride);

        var summary = new AutotuneRunSummary
        {
            Strategy = input.Run.AutotuneStrategy,
            Trials = tune.TrialsUsed,
            BestScore = tune.BestScore,
            BaselineTemplateDesign = baselineTemplate,
            WinningSeedDesign = seedResult.SeedForFinal,
            CoarseToFineLog = tune.CoarseToFineLog,
            SearchUsedEntrainmentDerivedBoreSizing = searchDerivedBore,
            SearchAllowedDirectChamberDiameterOverride = searchChamberOverride,
            FinalPassAppliedEntrainmentDerivedChamberBore = seedResult.FinalPassAppliedEntrainmentDerivedChamberBore
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
            auditMerged,
            pr.JetTrajectory);
    }

    /// <summary>Viewer group order/colors must stay aligned with <see cref="NozzleViewerGroupCatalog"/> (audit log references same IDs).</summary>
    internal static void DisplayGeometryInViewer(NozzleGeometryResult geometry)
    {
        Viewer viewer = Library.oViewer();
        int g = 1;
        foreach (NozzleViewerGroupCatalog.Entry e in NozzleViewerGroupCatalog.Ordered)
            AddSegment(viewer, ref g, VoxelsForCatalogProperty(geometry, e.NozzleGeometryResultProperty), e.ColorHex);

        if (geometry.JetTrajectoryDebug != null)
            AddSegment(viewer, ref g, geometry.JetTrajectoryDebug, NozzleViewerSegmentColors.JetTrajectoryDebugHex);
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

    private static void AddSegment(Viewer viewer, ref int groupId, Voxels voxels, string hex)
    {
        viewer.Add(voxels, groupId);
        viewer.SetGroupMaterial(groupId, hex, NozzleViewerSegmentColors.Roughness, NozzleViewerSegmentColors.Metallic);
        groupId++;
    }
}
