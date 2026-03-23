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
        NozzleInput work = input;
        NozzleDesignAutotune.Result? tune = null;
        RunConfiguration originalRun = input.Run;

        if (input.Run.UseAutotune)
        {
            tune = NozzleDesignAutotune.FindBestSeed(input.Source, input.Design, input.Run);
            work = new NozzleInput(input.Source, tune.BestSeedDesign, input.Run.AfterAutotune());
        }

        PipelineRunResult pr = NozzleFlowCompositionRoot.Run(work, work.Run.ShowInViewer);

        if (tune == null)
            return pr;

        var w = new List<string>
        {
            $"Autotune: {tune.TrialsUsed} SI evaluations, best composite score {tune.BestScore:F4} (weights E={originalRun.AutotuneWeightEntrainment:F2}, T={originalRun.AutotuneWeightThrust:F2}; 1-D model — validate in CFD)."
        };
        w.AddRange(pr.SolverWarnings);
        return new PipelineRunResult(pr.Input, pr.Solved, pr.Geometry, w, pr.SiFlow, pr.CriticalRatios);
    }

    /// <summary>Used by <see cref="NozzleFlowCompositionRoot"/> after flow-driven geometry is built.</summary>
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
