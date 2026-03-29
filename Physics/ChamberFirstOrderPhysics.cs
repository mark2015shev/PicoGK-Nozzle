using PicoGK_Run.Physics.SwirlSegment;

namespace PicoGK_Run.Physics;

/// <summary>
/// Aggregated reduced-order chamber / vortex / ejector diagnostics for one SI solve — not CFD.
/// </summary>
public sealed class ChamberFirstOrderPhysics
{
    public RadialVortexPressureResult RadialPressure { get; init; } = null!;
    public VortexStructureDiagnosticsResult VortexStructure { get; init; } = null!;
    public SwirlBudgetResult SwirlBudget { get; init; } = null!;
    public SwirlDiffuserRecoveryResult DiffuserRecovery { get; init; } = null!;
    public InjectorLossResult InjectorLoss { get; init; } = null!;
    public StatorLossResult StatorLoss { get; init; } = null!;
    public EjectorRegimeResult EjectorRegime { get; init; } = null!;

    /// <summary>One-line engineer-facing readout.</summary>
    public string InterpretationSummary { get; init; } = "";

    /// <summary>0–1 scalar for autotune (combines structure quality + radial usefulness − losses − ejector stress).</summary>
    public double TuningCompositeQuality { get; init; }

    public double FracSwirlDissipated { get; init; }
    public double FracSwirlForEntrainment { get; init; }
    public double FracSwirlToAxialRecovery { get; init; }
    public double FracSwirlRemainingAtStator { get; init; }

    public double NormalizedTotalPressureLoss01 { get; init; }
    public double RadialPressureUsefulNorm { get; init; }
    public double RecoverableSwirlFraction01 { get; init; }

    /// <summary>Injector / swirl-segment snapshot for logs and autotune penalties.</summary>
    public SwirlSegmentReducedOrderReport? SwirlSegmentReport { get; init; }

    /// <summary>Higher when mean capture pressure deficit is weak vs a reference dynamic head (0–1).</summary>
    public double CapturePressureDeficitWeakness01 { get; init; }
}
