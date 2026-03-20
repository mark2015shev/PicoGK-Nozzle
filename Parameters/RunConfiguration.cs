namespace PicoGK_Run.Parameters;

/// <summary>
/// Runtime viewer / voxel options only. STL export is disabled for this physics-first phase.
/// </summary>
public sealed class RunConfiguration
{
    public float VoxelSizeMM { get; init; } = 0.3f;
    public bool ShowInViewer { get; init; } = true;
}
