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
        NozzleSolvedState solved = _physicsSolver.Solve(input);
        NozzleGeometryResult geometry = _geometryBuilder.Build(input.Design, solved);

        if (input.Run.ShowInViewer)
        {
            Viewer viewer = Library.oViewer();
            viewer.Add(geometry.NozzleBody, 1);
            viewer.SetGroupMaterial(1, "#7AA5FF", 0.02f, 0.55f);

            viewer.Add(geometry.InjectorReferences, 2);
            viewer.SetGroupMaterial(2, "#FF7B7B", 0.02f, 0.30f);
        }

        if (input.Run.ExportStl)
        {
            Mesh mesh = new(geometry.NozzleBody);
            mesh.SaveToStlFile(input.Run.StlFileName);
        }

        return new PipelineRunResult(input, solved, geometry);
    }
}

