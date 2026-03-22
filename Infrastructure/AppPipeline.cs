using PicoGK;
using PicoGK_Run.Core;
using PicoGK_Run.Geometry;

namespace PicoGK_Run.Infrastructure;

internal sealed class AppPipeline
{
    public PipelineRunResult Run(NozzleInput input) =>
        NozzleFlowCompositionRoot.Run(input, input.Run.ShowInViewer);

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
