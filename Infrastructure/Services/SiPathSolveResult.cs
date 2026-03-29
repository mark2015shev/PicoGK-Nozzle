using System.Collections.Generic;
using PicoGK_Run.Core;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;
using PicoGK_Run.Physics.Solvers;

namespace PicoGK_Run.Infrastructure.Services;

/// <summary>Output of <see cref="PhysicsSiPathService.Solve"/> — driven design + SI diagnostics (no voxels).</summary>
internal sealed record SiPathSolveResult(
    NozzleDesignInputs DrivenDesign,
    NozzleSolvedState Solved,
    SiFlowDiagnostics SiDiag,
    NozzleCriticalRatiosSnapshot CriticalRatios,
    IReadOnlyList<string> HealthMessages,
    NozzleDesignResult DesignResult,
    JetState InletState,
    NozzlePhysicsStageResult PhysicsStages);
