using PicoGK_Run.Parameters;

namespace PicoGK_Run.Core;

/// <summary>
/// Immutable handoff: source boundary + design + runtime.
/// </summary>
public sealed class NozzleInput
{
    public SourceInputs Source { get; }
    public NozzleDesignInputs Design { get; }
    public RunConfiguration Run { get; }

    public NozzleInput(SourceInputs source, NozzleDesignInputs design, RunConfiguration run)
    {
        Source = source;
        Design = design;
        Run = run;
    }
}
