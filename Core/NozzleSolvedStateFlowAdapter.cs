using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Core;

/// <summary>
/// Builds a <see cref="NozzleSolvedState"/> for reporting/viewer when the SI flow pipeline is used
/// (legacy heuristic fields are zero or derived from <see cref="SiFlowDiagnostics"/> when present).
/// </summary>
public static class NozzleSolvedStateFlowAdapter
{
    public static NozzleSolvedState FromSiFlow(
        NozzleDesignResult design,
        JetState inlet,
        JetState outlet,
        SourceInputs source,
        NozzleDesignInputs designInputs,
        double injectorJetVelocityAreaDriverMps,
        double injectorJetVelocityContinuityCheckMps,
        double injectorJetVelocityMps,
        SiFlowDiagnostics? si = null)
    {
        double core = inlet.MassFlowKgS;
        double ambientMdot = Math.Max(0.0, outlet.TotalMassFlowKgS - core);
        double entrainmentRatio = core > 1e-12 ? ambientMdot / core : 0.0;
        double sourceOnly = core * source.SourceVelocityMps;

        // Injector swirl for reporting: use effective Vt/Va from coupled SI path when present (matches march inlet).
        double vJetForInjectorSwirl = si?.Coupling?.InjectorJetVelocityEffectiveMps ?? injectorJetVelocityMps;
        var (vTanInj, vAxInj) = SwirlMath.ResolveInjectorComponents(
            vJetForInjectorSwirl,
            designInputs.InjectorYawAngleDeg,
            designInputs.InjectorPitchAngleDeg);

        double injectorSwirl = SwirlMath.InjectorSwirlNumber(vTanInj, vAxInj);
        double chamberSwirl = si != null
            ? SwirlMath.InjectorSwirlNumber(si.FinalTangentialVelocityMps, Math.Max(si.FinalAxialVelocityMps, 1e-6))
            : injectorSwirl;

        double momentumThrust = si?.MomentumThrustN ?? outlet.TotalMassFlowKgS * outlet.VelocityMps;
        double pressureThrust = si?.PressureThrustN ?? 0.0;
        double finalThrust = si?.NetThrustN ?? design.EstimatedThrustN;

        double inletSuctionDeltaP = si != null
            ? Math.Max(0.0, source.AmbientPressurePa - si.MinInletLocalStaticPressurePa)
            : 0.0;

        var zeroLoss = new PressureLossBreakdown
        {
            FractionFromInjectorSourceAreaMismatch = 0.0,
            FractionFromSwirlDissipation = 0.0,
            FractionFromShortMixingLength = 0.0,
            FractionTotal = 0.0
        };

        return new NozzleSolvedState
        {
            CoreMassFlowKgPerSec = core,
            SourceAreaMm2 = source.SourceOutletAreaMm2,
            TotalInjectorAreaMm2 = designInputs.TotalInjectorAreaMm2,
            InjectorJetVelocityMps = injectorJetVelocityMps,
            InjectorJetVelocityAreaDriverMps = injectorJetVelocityAreaDriverMps,
            InjectorJetVelocityContinuityCheckMps = injectorJetVelocityContinuityCheckMps,
            CoreGasDensityKgPerM3 = inlet.DensityKgM3,
            TangentialVelocityComponentMps = si?.FinalTangentialVelocityMps ?? vTanInj,
            AxialVelocityComponentMps = si?.FinalAxialVelocityMps ?? outlet.VelocityMps,
            InjectorSwirlNumber = injectorSwirl,
            ChamberSwirlNumberForStator = chamberSwirl,
            InletSuctionDeltaPPa = inletSuctionDeltaP,
            InletCaptureEfficiency = si != null && si.SumRequestedEntrainmentIncrementsKgS > 1e-18
                ? Math.Min(1.09, si.SumActualEntrainmentIncrementsKgS / si.SumRequestedEntrainmentIncrementsKgS)
                : 1.0,
            InletPressureThrustComponentN = si?.InletAxialPressureForceN ?? 0.0,
            PressureRecoveryBudgetAfterInlet = 1.0,
            RemainingPressureRecoveryBudget = 1.0,
            SwirlPressureRisePa = si?.StatorRecoveredPressureRisePa ?? 0.0,
            ExpanderWallAxialForceN = si?.ExpanderAxialPressureForceN ?? 0.0,
            SwirlPressureRecoveryEfficiency = si != null ? Math.Min(0.95, 0.35 + injectorSwirl * 0.02) : 0.0,
            RemainingTangentialVelocityAfterPressureRecovery = si?.FinalTangentialVelocityMps ?? vTanInj,
            MomentumThrustComponentN = momentumThrust,
            PressureThrustComponentN = pressureThrust,
            PressureLoss = zeroLoss,
            DominantPressureLossContribution = si != null
                ? "si_compressible_march (first-order)"
                : "si_flow_model (no legacy loss split)",
            AmbientAirMassFlowKgPerSec = ambientMdot,
            EntrainmentRatio = entrainmentRatio,
            MixedMassFlowKgPerSec = outlet.TotalMassFlowKgS,
            MixedVelocityMps = outlet.VelocityMps,
            ExpansionEfficiency = 1.0,
            AxialRecoveryEfficiency = 1.0,
            ExitVelocityMps = outlet.VelocityMps,
            SourceOnlyThrustN = sourceOnly,
            FinalThrustN = finalThrust,
            ExtraThrustN = finalThrust - sourceOnly,
            ThrustGainRatio = sourceOnly > 1e-12 ? finalThrust / sourceOnly : 0.0
        };
    }
}
