namespace PicoGK_Run.Parameters;

/// <summary>
/// Runtime behavior options (export and viewer behavior).
/// </summary>
public sealed class RunConfiguration
{
    public float VoxelSizeMM { get; init; } = 0.3f;
    public bool ExportStl { get; init; } = false;
    public string StlFileName { get; init; } = "nozzle_result.stl";
    public bool ShowInViewer { get; init; } = true;
}

