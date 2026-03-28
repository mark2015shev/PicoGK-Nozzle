namespace PicoGK_Run.Core;

/// <summary>How <see cref="SourceInputs.ExhaustTemperatureK"/> is interpreted for live discharge (P = ρ R T_static).</summary>
public enum SourceTemperatureInterpretation
{
    /// <summary>Input temperature is static at the source exit plane.</summary>
    Static,

    /// <summary>Input temperature is total (stagnation); T_static = T_total − V²/(2 c_p).</summary>
    Total
}
