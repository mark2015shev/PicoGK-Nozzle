using PicoGK;
using PicoGK_Run.Core;
using PicoGK_Run.Infrastructure;
using PicoGK_Run.Parameters;

namespace PicoGK_Run;

/// <summary>
/// Minimal entry: build <see cref="NozzleInput"/> → run <see cref="AppPipeline"/> → log.
/// SI physics + flow march live in <see cref="PicoGK_Run.Infrastructure.NozzleFlowCompositionRoot"/> (see remarks there for file map).
/// </summary>
internal static class Program
{
    private static void Main(string[] args)
    {
        NozzleInput input = new(
            source: K320Baseline.CreateSource(),
            design: K320Baseline.CreateDesign(),
            run: K320Baseline.CreateRun());

        Library.Go(input.Run.VoxelSizeMM, () => Run(input));
    }

    private static void Run(NozzleInput input)
    {
        AppPipeline pipeline = new();
        PipelineRunResult result = pipeline.Run(input);
        ResultReporter.Report(result);
    }
}
