using PicoGK;
using PicoGK_Run.Core;
using PicoGK_Run.Geometry;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Infrastructure;

internal sealed class AppPipeline
{
    private readonly NozzlePhysicsSolver _physicsSolver = new();
    private readonly NozzleGeometryBuilder _geometryBuilder = new();

    public PipelineRunResult Run(NozzleInput input)
    {
        PhysicsSolveResult physics = _physicsSolver.Solve(input);
        NozzleGeometryResult geometry = _geometryBuilder.Build(input.Design, physics.State);

        if (input.Run.ShowInViewer)
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

        return new PipelineRunResult(input, physics.State, geometry, physics.Warnings);
    }

    private static void AddSegment(Viewer viewer, ref int groupId, Voxels voxels, string hex)
    {
        viewer.Add(voxels, groupId);
        viewer.SetGroupMaterial(groupId, hex, NozzleViewerSegmentColors.Roughness, NozzleViewerSegmentColors.Metallic);
        groupId++;
    }
}
