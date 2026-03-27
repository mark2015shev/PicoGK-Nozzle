using System.Collections.Generic;

namespace PicoGK_Run.Physics.Reports;

/// <summary>
/// Swirl-vortex chamber audit: geometry openness, blockage, entrainment, expander/stator entry — not CFD.
/// </summary>
public sealed class SwirlChamberHealthReport
{
    public double InletCaptureAreaMm2 { get; init; }
    public double ChamberBoreAreaMm2 { get; init; }
    public double ChamberFreeAnnulusAreaMm2 { get; init; }
    public double TotalInjectorAreaMm2 { get; init; }
    /// <summary>A_inj / A_chamber bore [-].</summary>
    public double InjectorToBoreAreaRatio { get; init; }
    /// <summary>A_free / A_chamber bore [-].</summary>
    public double FreeAnnulusToBoreAreaRatio { get; init; }
    public double ChamberSlendernessLD { get; init; }
    public double InjectorAxialPositionRatio { get; init; }
    public double ExpanderHalfAngleDeg { get; init; }
    public double ExpanderLengthMm { get; init; }

    public double InjectorAxialVelocityMps { get; init; }
    public double InjectorTangentialVelocityMps { get; init; }
    public double InjectorYawAngleDeg { get; init; }

    /// <summary>Injector-plane flux swirl S = Ġθ/(R·ġx) used for ambient-potential amplification (not |Vt|/|Va|).</summary>
    public double InjectorPlaneFluxSwirlNumber { get; init; }

    public double EstimatedCoreStaticPressurePa { get; init; }
    public double AmbientStaticPressurePa { get; init; }
    public double AmbientInflowPotentialKgS { get; init; }
    public double AmbientInflowActualSumKgS { get; init; }
    public double MixedMassFlowAtChamberEndKgS { get; init; }

    public double ExpanderEntryAxialVelocityMps { get; init; }
    public double ExpanderEntryTangentialVelocityMps { get; init; }
    /// <summary>Prefer flux swirl S from SI march; field name is legacy.</summary>
    public double StatorEntrySwirlNumberVtOverVa { get; init; }

    public double ExitAxialVelocityMps { get; init; }
    public double ThrustEstimateN { get; init; }

    public double StatorEffectiveEtaUsed { get; init; }
    public double StatorRecoveredPressureRisePa { get; init; }

    public string CorePressureModelSummary { get; init; } = "";

    public IReadOnlyList<string> PlainLanguageWarnings { get; init; } = System.Array.Empty<string>();
}
