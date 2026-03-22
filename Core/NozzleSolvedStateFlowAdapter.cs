using System;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Core;

/// <summary>
/// Builds a <see cref="NozzleSolvedState"/> for reporting/viewer when the SI flow pipeline is used
/// (legacy heuristic fields are zero or derived simply).
/// </summary>
public static class NozzleSolvedStateFlowAdapter
{
    public static NozzleSolvedState FromSiFlow(
        NozzleDesignResult design,
        JetState inlet,
        JetState outlet,
        SourceInputs source,
        NozzleDesignInputs designInputs)
    {
        double core = inlet.MassFlowKgS;
        double ambientMdot = Math.Max(0.0, outlet.TotalMassFlowKgS - core);
        double entrainmentRatio = core > 1e-12 ? ambientMdot / core : 0.0;
        double sourceOnly = core * source.SourceVelocityMps;
        double momentumThrust = outlet.TotalMassFlowKgS * outlet.VelocityMps;
        double pressureThrust = 0.0;

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
            InjectorJetVelocityMps = inlet.VelocityMps,
            InjectorJetVelocityAreaDriverMps = inlet.VelocityMps,
            InjectorJetVelocityContinuityCheckMps = inlet.VelocityMps,
            CoreGasDensityKgPerM3 = inlet.DensityKgM3,
            TangentialVelocityComponentMps = 0.0,
            AxialVelocityComponentMps = inlet.VelocityMps,
            InjectorSwirlNumber = 0.0,
            ChamberSwirlNumberForStator = 0.0,
            InletSuctionDeltaPPa = 0.0,
            InletCaptureEfficiency = 1.0,
            InletPressureThrustComponentN = 0.0,
            PressureRecoveryBudgetAfterInlet = 1.0,
            RemainingPressureRecoveryBudget = 1.0,
            SwirlPressureRisePa = 0.0,
            ExpanderWallAxialForceN = 0.0,
            SwirlPressureRecoveryEfficiency = 0.0,
            RemainingTangentialVelocityAfterPressureRecovery = 0.0,
            MomentumThrustComponentN = momentumThrust,
            PressureThrustComponentN = pressureThrust,
            PressureLoss = zeroLoss,
            DominantPressureLossContribution = "si_flow_model (no legacy loss split)",
            AmbientAirMassFlowKgPerSec = ambientMdot,
            EntrainmentRatio = entrainmentRatio,
            MixedMassFlowKgPerSec = outlet.TotalMassFlowKgS,
            MixedVelocityMps = outlet.VelocityMps,
            ExpansionEfficiency = 1.0,
            AxialRecoveryEfficiency = 1.0,
            ExitVelocityMps = outlet.VelocityMps,
            SourceOnlyThrustN = sourceOnly,
            FinalThrustN = design.EstimatedThrustN,
            ExtraThrustN = design.EstimatedThrustN - sourceOnly,
            ThrustGainRatio = sourceOnly > 1e-12 ? design.EstimatedThrustN / sourceOnly : 0.0
        };
    }
}
