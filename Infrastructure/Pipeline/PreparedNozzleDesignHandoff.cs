using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Infrastructure.Pipeline;

/// <summary>Immutable handoff after synthesis / sizing — input to <c>SolveSiPath</c> (same for tuning and final).</summary>
public sealed record PreparedNozzleDesignHandoff(
    NozzleDesignInputs SeedDesign,
    NozzleDesignInputs ActiveDesignAfterSynthesis,
    SwirlChamberSizingModel.SizingDiagnostics? ChamberSizing);
