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
        // Default: fixed hand design, no autotune.
        NozzleInput input = new(
            source: K320Baseline.CreateSource(),
            design: K320Baseline.CreateDesign(),
            run: K320Baseline.CreateRun());

        // Autotune on the 1-D SI model (many fast evals, then one voxel build). Example:
        // NozzleInput input = K320Baseline.CreateInputWithAutotune(trials: 200);
        // Or: run: K320Baseline.CreateRunWithAutotune(200) with the same source/design as above.

        Library.Go(input.Run.VoxelSizeMM, () => Run(input));
    }

    private static void Run(NozzleInput input)
    {
        AppPipeline pipeline = new();
        PipelineRunResult result = pipeline.Run(input);
        ResultReporter.Report(result);
    }
}
