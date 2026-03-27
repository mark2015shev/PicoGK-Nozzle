using System;
using System.Collections.Generic;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics.Solvers;

namespace PicoGK_Run.Physics.Reports;

/// <summary>Builds <see cref="SwirlChamberHealthReport"/> and plain-language choke / geometry warnings.</summary>
public static class SwirlChamberHealthReportBuilder
{
    private const double InjOverBoreSoft = 0.82;
    private const double InjOverBoreSevere = 0.92;
    private const double MinLdForVortex = 0.72;
    private const double PreferredLd = 0.95;
    private const double InjectorDownstreamAggressiveRatio = 0.9;

    public static SwirlChamberHealthReport Build(
        NozzleDesignInputs design,
        AmbientAir ambient,
        InjectorDischargeResult injector,
        double aCaptureM2,
        double aFreeChamberMm2,
        double sumActualEntrainmentKgS,
        double mixedMdotChamberEndKgS,
        double expanderEntryVaMps,
        double expanderEntryVtMps,
        double statorSwirlCorrelation,
        double exitVaMps,
        double thrustN,
        double etaStatorEff,
        double statorDpPa,
        double pCoreEstPa,
        double? physicsInjectorYawDegrees = null)
    {
        double aBore = SwirlChamberMarchGeometry.ChamberBoreAreaMm2(design.SwirlChamberDiameterMm);
        double aInlet = SwirlChamberMarchGeometry.InletCaptureAreaMm2(design.InletDiameterMm);
        double aInj = Math.Max(design.TotalInjectorAreaMm2, 1e-6);
        double rInjBore = aBore > 1e-6 ? aInj / aBore : 0.0;
        double rFreeBore = aBore > 1e-6 ? aFreeChamberMm2 / aBore : 0.0;
        double ld = SwirlChamberSolver.ChamberSlendernessLD(design.SwirlChamberLengthMm, design.SwirlChamberDiameterMm);

        double pot = AmbientInflowSolver.PotentialMassFlowKgS(
            ambient.PressurePa,
            pCoreEstPa,
            aCaptureM2,
            ambient.DensityKgM3,
            injector.SwirlNumberVtOverVa);

        var warns = new List<string>();

        if (rInjBore >= 1.0)
            warns.Add("Injector blockage likely choking entrainment: A_inj ≥ A_chamber bore — ports cannot fit in bore area budget.");
        else if (rInjBore >= InjOverBoreSevere)
            warns.Add("Injector blockage likely choking entrainment: A_inj/A_chamber bore is very high — little wall annulus for shear and secondary inflow.");
        else if (rInjBore >= InjOverBoreSoft)
            warns.Add("Injector ports use most of the chamber bore — swirl and entrainment margin are tight; consider larger D_chamber or fewer/smaller ports.");

        if (ld < MinLdForVortex)
            warns.Add("Chamber too short for vortex development: L/D is below a modest development guideline — swirl may not organize before the expander.");
        else if (ld < PreferredLd)
            warns.Add("Chamber L/D is modest — vortex development is borderline; consider longer chamber if entrainment stays low.");

        double dIn = design.InletDiameterMm;
        double dCh = design.SwirlChamberDiameterMm;
        if (dIn < dCh * 1.01)
            warns.Add("Inlet lip diameter is not clearly larger than chamber bore — check bell-mouth capture vs contraction.");

        if (design.InjectorAxialPositionRatio >= InjectorDownstreamAggressiveRatio)
            warns.Add("Injectors are far downstream in the chamber — expander begins soon after injection; risk that the vortex has little axial runout.");

        if (design.ExpanderHalfAngleDeg > 10.5)
            warns.Add("Expander cone half-angle is fairly aggressive for rotating flow — separation risk may trim effective recovery.");

        if (statorSwirlCorrelation > 6.0 && etaStatorEff < 0.15)
            warns.Add("Stator is receiving strongly swirl-dominated flow but effective η is very low — incidence/turning model may be over-penalizing; inspect stator coupling and recovery caps.");

        if (sumActualEntrainmentKgS < 0.08 * Math.Max(mixedMdotChamberEndKgS, 1e-6))
            warns.Add("Ambient inflow actual is small vs mixed flow — core suction or capture geometry may be limiting entrainment (not necessarily invalid for 90° tangential injection).");

        if (Math.Abs(injector.AxialVelocityMps) < 2.0 && Math.Abs(injector.TangentialVelocityMps) > 80.0)
            warns.Add("Pure tangential injection (V_a ≈ 0): downstream transport should come from entrainment, pressure gradients, mixing, and expander — not from injector axial jet.");

        return new SwirlChamberHealthReport
        {
            InletCaptureAreaMm2 = aInlet,
            ChamberBoreAreaMm2 = aBore,
            ChamberFreeAnnulusAreaMm2 = aFreeChamberMm2,
            TotalInjectorAreaMm2 = aInj,
            InjectorToBoreAreaRatio = rInjBore,
            FreeAnnulusToBoreAreaRatio = rFreeBore,
            ChamberSlendernessLD = ld,
            InjectorAxialPositionRatio = design.InjectorAxialPositionRatio,
            ExpanderHalfAngleDeg = design.ExpanderHalfAngleDeg,
            ExpanderLengthMm = design.ExpanderLengthMm,
            InjectorAxialVelocityMps = injector.AxialVelocityMps,
            InjectorTangentialVelocityMps = injector.TangentialVelocityMps,
            InjectorYawAngleDeg = physicsInjectorYawDegrees ?? design.InjectorYawAngleDeg,
            EstimatedCoreStaticPressurePa = pCoreEstPa,
            AmbientStaticPressurePa = ambient.PressurePa,
            AmbientInflowPotentialKgS = pot,
            AmbientInflowActualSumKgS = sumActualEntrainmentKgS,
            MixedMassFlowAtChamberEndKgS = mixedMdotChamberEndKgS,
            ExpanderEntryAxialVelocityMps = expanderEntryVaMps,
            ExpanderEntryTangentialVelocityMps = expanderEntryVtMps,
            StatorEntrySwirlNumberVtOverVa = statorSwirlCorrelation,
            ExitAxialVelocityMps = exitVaMps,
            ThrustEstimateN = thrustN,
            StatorEffectiveEtaUsed = etaStatorEff,
            StatorRecoveredPressureRisePa = statorDpPa,
            CorePressureModelSummary = CorePressureSolver.FormulaSummary,
            PlainLanguageWarnings = warns
        };
    }
}
