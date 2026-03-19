using PicoGK_Run.Parameters;

namespace PicoGK_Run.Core;

/// <summary>
/// Single immutable handoff object for pipeline execution.
/// </summary>
public sealed class NozzleInput
{
    public SourceInputs Source { get; }
    public AmbientAir Ambient { get; }
    public NozzleDesignInputs Design { get; }
    public RunConfiguration Run { get; }

    public NozzleInput(
        SourceInputs source,
        AmbientAir ambient,
        NozzleDesignInputs design,
        RunConfiguration run)
    {
        Source = source;
        Ambient = ambient;
        Design = design;
        Run = run;
    }
}

