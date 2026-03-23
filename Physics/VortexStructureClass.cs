namespace PicoGK_Run.Physics;

/// <summary>
/// Heuristic vortex regime label from 1-D SI scalars — not a CFD stability analysis.
/// </summary>
public enum VortexStructureClass
{
    ForcedVortexDominated,
    MixedVortex,
    FreeVortexDominated,
    PossibleBreakdownOrUnstable
}
