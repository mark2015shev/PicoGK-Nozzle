namespace PicoGK_Run.Core;

/// <summary>
/// Named fractional losses on mixed axial kinetic energy (heuristic, 0..1 scale).
/// Not a calibrated pressure-drop model.
/// </summary>
public readonly struct PressureLossBreakdown
{
    /// <summary>Penalty when total injector area ≠ source area (impedance mismatch).</summary>
    public double FractionFromInjectorSourceAreaMismatch { get; init; }

    /// <summary>Extra dissipation tied to injector swirl number (shear/mixing).</summary>
    public double FractionFromSwirlDissipation { get; init; }

    /// <summary>Penalty when chamber is short relative to diameter (incomplete mixing).</summary>
    public double FractionFromShortMixingLength { get; init; }

    /// <summary>Combined loss applied once to mixed velocity (clamped).</summary>
    public double FractionTotal { get; init; }
}
