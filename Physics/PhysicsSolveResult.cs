using System.Collections.Generic;
using PicoGK_Run.Core;

namespace PicoGK_Run.Physics;

public sealed class PhysicsSolveResult
{
    public required NozzleSolvedState State { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
}
