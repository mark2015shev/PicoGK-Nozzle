using System.Collections.Generic;
using PicoGK;
using PicoGK_Run.Core;
using PicoGK_Run.Geometry;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Infrastructure;

internal sealed class AppPipeline
{
    public PipelineRunResult Run(NozzleInput input)
    {
        if (!input.Run.UseAutotune)
            return NozzleFlowCompositionRoot.Run(input, input.Run.ShowInViewer);

        NozzleDesignAutotune.Result tune = NozzleDesignAutotune.FindBestSeed(input.Source, input.Design, input.Run);
        NozzleInput work = new NozzleInput(input.Source, tune.BestSeedDesign, input.Run.AfterAutotune());

        PipelineRunResult pr = NozzleFlowCompositionRoot.Run(work, work.Run.ShowInViewer);

        var summary = new AutotuneRunSummary
        {
            Trials = tune.TrialsUsed,
            BestScore = tune.BestScore,
            WinningSeedDesign = tune.BestSeedDesign
        };

        var w = new List<string>
        {
            $"Autotune: {tune.TrialsUsed} SI-only evaluations, best score {tune.BestScore:F4} (weights E={input.Run.AutotuneWeightEntrainment:F2}, T={input.Run.AutotuneWeightThrust:F2}). Pre-CFD — validate in CFD."
        };
        w.AddRange(pr.SolverWarnings);

        return new PipelineRunResult(
            pr.Input,
            pr.Solved,
            pr.Geometry,
            w,
            pr.SiFlow,
            pr.CriticalRatios,
            summary);
    }

    internal static void DisplayGeometryInViewer(NozzleGeometryResult geometry)
    {
        Viewer viewer = Library.oViewer();
        int g = 1;
        AddSegment(viewer, ref g, geometry.Inlet, NozzleViewerSegmentColors.InletHex);
        AddSegment(viewer, ref g, geometry.SwirlChamber, NozzleViewerSegmentColors.SwirlChamberHex);
        AddSegment(viewer, ref g, geometry.InjectorReferenceMarkers, NozzleViewerSegmentColors.InjectorReferenceMarkersHex);
        AddSegment(viewer, ref g, geometry.Expander, NozzleViewerSegmentColors.ExpanderHex);
        AddSegment(viewer, ref g, geometry.StatorSection, NozzleViewerSegmentColors.StatorSectionHex);
        AddSegment(viewer, ref g, geometry.Exit, NozzleViewerSegmentColors.ExitHex);
    }

    private static void AddSegment(Viewer viewer, ref int groupId, Voxels voxels, string hex)
    {
        viewer.Add(voxels, groupId);
        viewer.SetGroupMaterial(groupId, hex, NozzleViewerSegmentColors.Roughness, NozzleViewerSegmentColors.Metallic);
        groupId++;
    }
}
