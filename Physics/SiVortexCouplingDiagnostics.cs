using System.Collections.Generic;

namespace PicoGK_Run.Physics;

/// <summary>
/// First-order SI path audit: values actually used in thrust/momentum vs raw references.
/// Not CFD — explicit bookkeeping for coupled vortex physics.
/// </summary>
public sealed class SiVortexCouplingDiagnostics
{
    /// <summary>Blended driver jet speed before Cd / turning loss [m/s].</summary>
    public double InjectorJetVelocityRawMps { get; init; }

    /// <summary>
    /// V_eff = Cd·V_in·√max(0,1−K_turn) with K_turn from <see cref="InjectorLossModel"/>; clamped &gt; 0 [m/s].
    /// </summary>
    public double InjectorJetVelocityEffectiveMps { get; init; }

    public double InjectorVtRawMps { get; init; }
    public double InjectorVaRawMps { get; init; }
    public double InjectorVtEffectiveMps { get; init; }
    public double InjectorVaEffectiveMps { get; init; }

    /// <summary>Added to capture pressure deficit in entrainment velocity (Pa); zero when core-suction coupling is off.</summary>
    public double CaptureStaticPressureDeficitAugmentationPa { get; init; }
    public double DeltaPCoreUsefulForEntrainmentPa { get; init; }

    public double StatorEtaBase { get; init; }
    public double StatorEtaEffective { get; init; }
    public double StatorCouplingKIncidence { get; init; }
    public double StatorCouplingKTurn { get; init; }

    public double ExpanderDeltaPBasePa { get; init; }
    public double ExpanderDeltaPEffectivePa { get; init; }
    public double DiffuserRecoveryMultiplier { get; init; }
    public double DiffuserSeparationAxialFactor { get; init; }

    public double FinalAxialVelocityBaseMps { get; init; }
    public double FinalAxialVelocityEffectiveMps { get; init; }

    public SwirlEnergyCouplingLedger SwirlEnergy { get; init; } = null!;

    /// <summary>Short engineer-facing lines (interpretation, not claims).</summary>
    public IReadOnlyList<string> CouplingSummaryLines { get; init; } = System.Array.Empty<string>();
}

/// <summary>
/// Tangential kinetic energy rate E_θ = ½·ṁ·V_θ² audit chain (first-order; mass flow may change along chamber).
/// </summary>
public sealed class SwirlEnergyCouplingLedger
{
    /// <summary>½·ṁ_core·Vt_raw² from raw blended jet [W].</summary>
    public double EThetaInjectedRaw_W { get; init; }

    /// <summary>½·ṁ_core·Vt_eff² after injector loss coupling [W].</summary>
    public double EThetaAfterInjectorLoss_W { get; init; }

    /// <summary>½·ṁ_mix,end·Vt_mixed² at chamber end (pre-stator) [W].</summary>
    public double EThetaAfterChamberDecay_W { get; init; }

    /// <summary>Reduced-order debit to entrainment mixing/dilution [W].</summary>
    public double EThetaUsedForEntrainment_W { get; init; }

    /// <summary>Bookkeeping tie to diffuser coupling (reduced recovery vs ideal) [W].</summary>
    public double EThetaUsedForDiffuserRecovery_W { get; init; }

    /// <summary>½·ṁ·(Vt_pre²−Vt_post²) stator row (tangential KE removed) [W].</summary>
    public double EThetaUsedForStatorRecovery_W { get; init; }

    /// <summary>½·ṁ_exit·Vt_exit² residual [W].</summary>
    public double EThetaExitResidual_W { get; init; }

    /// <summary>Closure: decay + uncaptured dissipation [W].</summary>
    public double EThetaDissipated_W { get; init; }

    /// <summary>
    /// Builds an explicit energy audit (first-order). Uses primary-track decay vs mixed-state tangential KE.
    /// </summary>
    public static SwirlEnergyCouplingLedger Build(
        double mdotCoreKgS,
        double vtRawMps,
        double vtEffMps,
        double vtPrimaryEndMps,
        double mdotMixEndKgS,
        double vtMixedEndMps,
        double vtAfterStatorMps,
        double diffuserRecoveryMultiplier,
        double diffuserBookkeepingK)
    {
        double mc = Math.Max(mdotCoreKgS, 1e-18);
        double mm = Math.Max(mdotMixEndKgS, 1e-18);
        double e0 = 0.5 * mc * vtRawMps * vtRawMps;
        double e1 = 0.5 * mc * vtEffMps * vtEffMps;
        double ePriEnd = 0.5 * mc * vtPrimaryEndMps * vtPrimaryEndMps;
        double e2 = 0.5 * mm * vtMixedEndMps * vtMixedEndMps;
        double e3 = 0.5 * mm * vtAfterStatorMps * vtAfterStatorMps;

        double dInj = Math.Max(0.0, e0 - e1);
        double dDecayPrimary = Math.Max(0.0, e1 - ePriEnd);
        double dEntMix = Math.Max(0.0, ePriEnd - e2);
        double dStator = Math.Max(0.0, e2 - e3);
        double dDiff = Math.Max(0.0, e2 * (1.0 - Math.Clamp(diffuserRecoveryMultiplier, 0.0, 2.0)) * Math.Max(diffuserBookkeepingK, 0.0));

        double eDiss = dInj + dDecayPrimary;

        return new SwirlEnergyCouplingLedger
        {
            EThetaInjectedRaw_W = e0,
            EThetaAfterInjectorLoss_W = e1,
            EThetaAfterChamberDecay_W = e2,
            EThetaUsedForEntrainment_W = dEntMix,
            EThetaUsedForDiffuserRecovery_W = dDiff,
            EThetaUsedForStatorRecovery_W = dStator,
            EThetaExitResidual_W = e3,
            EThetaDissipated_W = eDiss
        };
    }
}
