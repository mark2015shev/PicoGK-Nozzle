using System.Collections.Generic;

namespace PicoGK_Run.Geometry;

/// <summary>Result of <see cref="GeometryContinuityValidator.Check"/>.</summary>
public sealed class GeometryContinuityReport
{
    public bool IsAcceptable { get; init; }
    public IReadOnlyList<string> Issues { get; init; } = System.Array.Empty<string>();
}
