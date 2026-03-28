namespace PicoGK_Run.Physics;

/// <summary>Relative / absolute tolerances for optional per-step SI march checks.</summary>
public static class MarchInvariantTolerances
{
    public const double MassSplitRelativeTolerance = 1e-7;
    public const double IdealGasRelativeTolerance = 0.03;
    public const double ContinuityRelativeTolerance = 0.34;
    public const double MachSanityCeiling = 4.0;
}
