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
        // First-order SI validation sweeps (no voxels/viewer; CSV under ./Output/ValidationSweeps):
        //   dotnet run -- validate
        // Or call: ValidationSweepRunner.RunDefaultK320Validation();
        if (args.Length > 0 && string.Equals(args[0], "validate", StringComparison.OrdinalIgnoreCase))
        {
            ValidationSweepRunner.RunDefaultK320Validation();
            return;
        }

        // Toggle autotune (must use CreateRunWithAutotune or CreateInputWithAutotune so UseAutotune is true).
        bool useAutotune = true;
        NozzleInput input = useAutotune
            ? K320Baseline.CreateInputWithAutotune(trials: 300)
            : new(
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
