namespace PicoGK_Run.Parameters;

/// <summary>Viewer-only runtime options (voxel resolution + show/hide).</summary>
public sealed class RunConfiguration
{
    public float VoxelSizeMM { get; init; } = 0.3f;
    public bool ShowInViewer { get; init; } = true;
}
