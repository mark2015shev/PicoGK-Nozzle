using System;
using System.Collections.Generic;
using PicoGK_Run.Core;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Physics;

/// <summary>
/// Physics-first parametric nozzle / ejector estimator. All non-trivial sub-models are
/// explicit <b>heuristics</b> — not CFD, not test-stand calibrated.
/// </summary>
public sealed class NozzlePhysicsSolver
{
    private const double SpecificGasConstantAirJPerKgK = 287.0;

    /// <summary>
    /// HEURISTIC — NOT CFD: scales estimated wall pressure rise from swirl (order dp/dr ~ rho*v_theta^2/r).
    /// Swirl-pressure recovery on expander walls — do not interpret as a separate centrifugal thrust mechanism.
    /// </summary>
    private const double SwirlPressureRiseCoefficientHeuristic = 0.30;

    /// <summary>HEURISTIC: projects wall pressure resultant to axial direction on angled expander (sin α), &lt; 1.</summary>
    private const double ExpanderWallAxialProjectionCoefficientHeuristic = 0.48;

    /// <summary>Weight on V_core×(A_source/A_inj) in jet-speed blend; remainder is continuity mdot/(ρA).</summary>
    public const double InjectorJetVelocityDriverBlend = 0.88;

    public PhysicsSolveResult Solve(NozzleInput input)
    {
        List<string> warnings = new();
        SourceInputs source = input.Source;
        NozzleDesignInputs design = input.Design;

        Validate(design, source);

        WarnInjectorSlotArea(design, warnings);
        WarnRollUnused(design, warnings);

        // Thrust baseline (simplification, not heuristic): F0 ≈ mdot_core * V_core, no pressure term.

        double sourceAreaMm2 = source.SourceOutletAreaMm2;
        double injectorAreaMm2 = design.TotalInjectorAreaMm2;

        double sourceAreaM2 = AreaMath.ToSquareMeters(sourceAreaMm2);
        double injectorAreaM2 = AreaMath.ToSquareMeters(injectorAreaMm2);
        double inletAreaM2 = AreaMath.ToSquareMeters(AreaMath.CircleAreaMM2(design.InletDiameterMm));
        double chamberAreaMm2 = AreaMath.CircleAreaMM2(design.SwirlChamberDiameterMm);
        double chamberAreaM2 = AreaMath.ToSquareMeters(chamberAreaMm2);

        double coreMdot = source.MassFlowKgPerSec;
        double vCore = source.SourceVelocityMps > 0.0
            ? source.SourceVelocityMps
            : VelocityMath.FromMassFlow(coreMdot, source.AmbientDensityKgPerM3, sourceAreaM2);

        double rhoCore = EstimateCoreGasDensityKgPerM3(source, warnings);

        // -------------------------------------------------------------------------
        // Source → injector jet speed (HEURISTIC momentum / area model — NOT CFD)
        // Primary driver: K320/source V_core scaled by area ratio A_source/A_inj (1D bulk continuation
        // at similar density). Continuity mdot/(ρ A_inj) is a secondary blend so ρ errors do not
        // collapse the jet unphysically below the source-speed scale when areas match.
        // -------------------------------------------------------------------------
        double areaDriver = vCore * (sourceAreaMm2 / Math.Max(injectorAreaMm2, 1e-9));
        double continuityCheck = coreMdot / (rhoCore * Math.Max(injectorAreaM2, 1e-12));
        double injectorJetVelocity = InjectorJetVelocityDriverBlend * areaDriver
            + (1.0 - InjectorJetVelocityDriverBlend) * continuityCheck;

        if (continuityCheck > 1e-6 && Math.Abs(continuityCheck - areaDriver) / Math.Max(areaDriver, 1e-6) > 0.42)
            warnings.Add("Injector continuity check (mdot/ρA) differs strongly from V_core×(A_source/A_inj); review ρ_core or areas.");

        var (vTan, vAx) = SwirlMath.ResolveInjectorComponents(
            injectorJetVelocity,
            design.InjectorYawAngleDeg,
            design.InjectorPitchAngleDeg);

        double injectorSwirlNumber = SwirlMath.InjectorSwirlNumber(vTan, vAx);

        double chamberLd = design.SwirlChamberLengthMm / Math.Max(design.SwirlChamberDiameterMm, 1e-9);
        double rAx = Math.Clamp(design.InjectorAxialPositionRatio, 0.0, 1.0);

        // HEURISTIC — NOT CFD: decay of injector directive swirl (used after mixing for expander + stator paths)
        double chamberSwirlRaw = injectorSwirlNumber * Math.Exp(-0.55 * Math.Clamp(chamberLd, 0.0, 4.0));
        chamberSwirlRaw = Math.Clamp(chamberSwirlRaw, 0.0, 8.0);

        // -------------------------------------------------------------------------
        // HEURISTIC — NOT CFD: entrainment (bounded, first-order)
        // Ties to source momentum scale (V_core), inlet capture, chamber area, L/D, axial injector
        // station (ratio along chamber), and injector swirl number.
        // -------------------------------------------------------------------------
        double inletToSource = inletAreaM2 / Math.Max(sourceAreaM2, 1e-12);
        double chamberToSource = chamberAreaM2 / Math.Max(sourceAreaM2, 1e-12);
        double capture = Math.Clamp(Math.Max(0.0, inletToSource - 1.0), 0.0, 6.0);
        double chamberOpenness = Math.Clamp(Math.Sqrt(chamberToSource), 0.4, 2.2);
        double swirlBoost = injectorSwirlNumber / (1.0 + 0.9 * injectorSwirlNumber);

        double momentumScale = Math.Clamp(vCore / 420.0, 0.65, 1.35);
        double placementMix = 0.88 + 0.20 * rAx;

        double entrainmentRaw =
            momentumScale * (
                0.10 * capture * chamberOpenness * placementMix
                + 0.14 * Math.Clamp(chamberLd, 0.12, 2.8) * swirlBoost * (0.95 + 0.10 * rAx));

        double entrainmentRatio = Math.Clamp(entrainmentRaw, 0.0, 1.35);
        if (entrainmentRatio >= 1.32)
            warnings.Add("Unrealistic entrainment: ratio hit upper heuristic cap (1.35); treat as optimistic.");

        if (entrainmentRatio > 0.90 && inletToSource < 1.12)
            warnings.Add("Unrealistic entrainment: high ratio with only modest inlet capture (A_inlet/A_source).");

        double ambientMdot = coreMdot * entrainmentRatio;
        double mixedMdot = coreMdot + ambientMdot;

        double mixedVelocityIdeal = coreMdot * vCore / Math.Max(mixedMdot, 1e-12);

        double injectorToSourceArea = injectorAreaMm2 / Math.Max(sourceAreaMm2, 1e-12);
        PressureLossBreakdown loss = PressureLossMath.Compute(injectorToSourceArea, chamberLd, injectorSwirlNumber);
        string dominantLoss = PressureLossMath.DominantContribution(loss);

        double mixedVelocityAfterLoss = mixedVelocityIdeal * (1.0 - loss.FractionTotal);
        if (mixedVelocityAfterLoss < 0.45 * vCore && mixedVelocityIdeal > 1e-6)
            warnings.Add("Severe mixed-velocity collapse: dilution and/or heuristic losses reduced axial speed well below core speed (before numeric floor).");

        double vFloor = 0.42 * vCore;
        double mixedVelocity = mixedVelocityAfterLoss;
        if (mixedVelocity < vFloor)
        {
            warnings.Add("Severe mixed-velocity collapse: applied minimum floor (0.42 * V_core); raw mixed speed was lower.");
            mixedVelocity = vFloor;
        }

        // -------------------------------------------------------------------------
        // HEURISTIC — NOT CFD: swirl-pressure recovery on expander walls (after mixing, BEFORE stator).
        // Physics motivation: strong v_theta supports a radial pressure gradient (~ rho*v_theta^2/r);
        // higher wall pressure on outer contour, projected axially on angled expander surfaces.
        // This is NOT an additive "centrifugal bonus force"; it is a bounded pressure-recovery / wall-force
        // term drawn from the same tangential-energy budget as stator recovery (no double counting).
        // -------------------------------------------------------------------------
        double rhoAmbient = source.AmbientDensityKgPerM3;
        double volFlowCore = coreMdot / Math.Max(rhoCore, 1e-9);
        double volFlowAmb = ambientMdot / Math.Max(rhoAmbient, 1e-9);
        double rhoMix = mixedMdot / Math.Max(volFlowCore + volFlowAmb, 1e-12);
        rhoMix = Math.Clamp(rhoMix, 0.2, 25.0);

        // Tangential speed scale in mixed flow (same swirl budget as chamberSwirlRaw); bounded vs axial bulk.
        double vThetaPreExpander = mixedVelocity * (chamberSwirlRaw / (1.0 + chamberSwirlRaw));
        vThetaPreExpander = Math.Min(vThetaPreExpander, 0.90 * mixedVelocity);

        double rChamberM = 0.5 * design.SwirlChamberDiameterMm / 1000.0;
        double dpSwirlHeuristic = SwirlPressureRiseCoefficientHeuristic * rhoMix * vThetaPreExpander * vThetaPreExpander
            / Math.Max(rChamberM, 1e-6);
        double dpVsDynamicHeadCap = 0.11 * rhoMix * mixedVelocity * mixedVelocity;
        double swirlPressureRisePa = Math.Min(dpSwirlHeuristic, dpVsDynamicHeadCap);
        swirlPressureRisePa = Math.Min(swirlPressureRisePa, 9000.0);
        swirlPressureRisePa = Math.Max(0.0, swirlPressureRisePa);

        double rExitM = 0.5 * design.ExitDiameterMm / 1000.0;
        double lExpanderM = design.ExpanderLengthMm / 1000.0;
        double halfAngleRad = design.ExpanderHalfAngleDeg * (Math.PI / 180.0);
        double deltaR = Math.Abs(rExitM - rChamberM);
        double slantLength = Math.Sqrt(lExpanderM * lExpanderM + deltaR * deltaR);
        if (slantLength < 1e-8)
            slantLength = Math.Max(lExpanderM, 1e-8);
        double expanderLateralAreaM2 = Math.PI * (rChamberM + rExitM) * slantLength;

        // Axial component only: pressure × wetted area × sin(half-angle) × heuristic projection (conservative).
        double expanderWallAxialForceN = swirlPressureRisePa * expanderLateralAreaM2 * Math.Sin(halfAngleRad)
            * ExpanderWallAxialProjectionCoefficientHeuristic;

        double fCapVsMomentum = 0.16 * mixedMdot * mixedVelocity;
        double fCapVsPressureArea = swirlPressureRisePa * expanderLateralAreaM2 * 0.52;
        expanderWallAxialForceN = Math.Min(expanderWallAxialForceN, fCapVsMomentum);
        expanderWallAxialForceN = Math.Min(expanderWallAxialForceN, fCapVsPressureArea);
        expanderWallAxialForceN = Math.Max(0.0, expanderWallAxialForceN);

        // Debit tangential kinetic energy before stator so pressure recovery and stator do not use full swirl twice.
        double kTanRef = 0.5 * mixedMdot * vThetaPreExpander * vThetaPreExpander;
        double workLikeFromWall = expanderWallAxialForceN * mixedVelocity * 0.24;
        double energyTap = Math.Min(workLikeFromWall, kTanRef * 0.26);
        energyTap = Math.Min(energyTap, kTanRef * 0.32);

        double vThetaRem = vThetaPreExpander > 1e-9
            ? vThetaPreExpander * Math.Sqrt(Math.Max(0.0, 1.0 - energyTap / Math.Max(kTanRef, 1e-12)))
            : 0.0;
        vThetaRem = Math.Min(vThetaRem, vThetaPreExpander);

        double swirlPressureRecoveryEfficiency = vThetaPreExpander > 1e-9
            ? Math.Clamp(1.0 - (vThetaRem / vThetaPreExpander) * (vThetaRem / vThetaPreExpander), 0.0, 0.35)
            : 0.0;

        double chamberSwirlForStator = vThetaPreExpander > 1e-9
            ? chamberSwirlRaw * (vThetaRem / vThetaPreExpander)
            : 0.0;
        chamberSwirlForStator = Math.Clamp(chamberSwirlForStator, 0.0, 8.0);

        double expansionEfficiency = EstimateExpansionEfficiency(design, source, warnings);
        double axialRecovery = EstimateStatorAxialRecovery(design, chamberSwirlForStator, warnings);

        double vAfterExpansion = mixedVelocity * expansionEfficiency;
        double exitVelocity = vAfterExpansion * axialRecovery;

        double sourceOnlyThrust = coreMdot * vCore;
        double momentumThrust = mixedMdot * exitVelocity;
        double pressureThrust = expanderWallAxialForceN;
        double finalThrust = momentumThrust + pressureThrust;
        double extraThrust = finalThrust - sourceOnlyThrust;
        double thrustGainRatio = finalThrust / Math.Max(sourceOnlyThrust, 1e-9);

        NozzleSolvedState state = new()
        {
            CoreMassFlowKgPerSec = coreMdot,
            SourceAreaMm2 = sourceAreaMm2,
            TotalInjectorAreaMm2 = injectorAreaMm2,
            InjectorJetVelocityMps = injectorJetVelocity,
            InjectorJetVelocityAreaDriverMps = areaDriver,
            InjectorJetVelocityContinuityCheckMps = continuityCheck,
            CoreGasDensityKgPerM3 = rhoCore,
            TangentialVelocityComponentMps = vTan,
            AxialVelocityComponentMps = vAx,
            InjectorSwirlNumber = injectorSwirlNumber,
            ChamberSwirlNumberForStator = chamberSwirlForStator,
            SwirlPressureRisePa = swirlPressureRisePa,
            ExpanderWallAxialForceN = expanderWallAxialForceN,
            SwirlPressureRecoveryEfficiency = swirlPressureRecoveryEfficiency,
            RemainingTangentialVelocityAfterPressureRecovery = vThetaRem,
            MomentumThrustComponentN = momentumThrust,
            PressureThrustComponentN = pressureThrust,
            PressureLoss = loss,
            DominantPressureLossContribution = dominantLoss,
            AmbientAirMassFlowKgPerSec = ambientMdot,
            EntrainmentRatio = entrainmentRatio,
            MixedMassFlowKgPerSec = mixedMdot,
            MixedVelocityMps = mixedVelocity,
            ExpansionEfficiency = expansionEfficiency,
            AxialRecoveryEfficiency = axialRecovery,
            ExitVelocityMps = exitVelocity,
            SourceOnlyThrustN = sourceOnlyThrust,
            FinalThrustN = finalThrust,
            ExtraThrustN = extraThrust,
            ThrustGainRatio = thrustGainRatio
        };

        RunDesignSanityChecks(input, state, warnings);

        return new PhysicsSolveResult { State = state, Warnings = warnings };
    }

    /// <summary>
    /// <b>HEURISTIC</b> ρ for continuity <b>cross-check / blend</b> only. Primary jet speed uses V_core×(A_s/A_inj).
    /// </summary>
    private static double EstimateCoreGasDensityKgPerM3(SourceInputs s, List<string> warnings)
    {
        if (!s.ExhaustTemperatureK.HasValue || s.ExhaustTemperatureK.Value < 250.0)
        {
            warnings.Add("ExhaustTemperatureK missing/low; ρ_core falls back to AmbientDensityKgPerM3 (continuity blend only).");
            return s.AmbientDensityKgPerM3;
        }

        double tEx = s.ExhaustTemperatureK.Value;
        double pScale = s.AmbientPressurePa * Math.Clamp(s.PressureRatio, 1.0, 6.0);
        double rho = pScale / (SpecificGasConstantAirJPerKgK * tEx);
        return Math.Clamp(rho, 0.15, 22.0);
    }

    // HEURISTIC — NOT CFD: expansion efficiency (see prior comments in file history)
    private static double EstimateExpansionEfficiency(NozzleDesignInputs d, SourceInputs source, List<string> warnings)
    {
        double halfDeg = d.ExpanderHalfAngleDeg;
        if (halfDeg > 18.0)
            warnings.Add("Aggressive expander: half-angle > 18°; expansion efficiency is heavily penalized in this heuristic model.");
        else if (halfDeg > 14.0)
            warnings.Add("Aggressive expander: half-angle > 14°; watch separation risk in real flow (heuristic model only).");

        double angleRad = halfDeg * (Math.PI / 180.0);
        double steepPenalty = Math.Clamp((halfDeg - 10.0) / 12.0, 0.0, 1.0);
        double angleFactor = Math.Cos(angleRad) * (1.0 - 0.35 * steepPenalty);

        double lengthRatio = d.ExpanderLengthMm / Math.Max(d.SwirlChamberDiameterMm, 1e-9);
        if (lengthRatio < 0.55)
            warnings.Add("Expander length is short relative to chamber diameter; expansion recovery may be poor.");
        double lengthFactor = 1.0 - Math.Exp(-1.15 * Math.Max(0.0, lengthRatio));

        double sourceDiamMm = AreaMath.CircleDiameterFromAreaMm2(source.SourceOutletAreaMm2);
        double expansionRatio = d.ExitDiameterMm / Math.Max(sourceDiamMm, 1e-9);
        double ratioBand = Math.Exp(-Math.Pow((expansionRatio - 1.85) / 0.95, 2.0));

        double pressureDrive = Math.Clamp((source.PressureRatio - 1.0) / 3.0, 0.0, 1.0);

        double eta = 0.22 + 0.38 * angleFactor * lengthFactor + 0.22 * ratioBand + 0.12 * pressureDrive;
        return Math.Clamp(eta, 0.12, 0.94);
    }

    // HEURISTIC — NOT CFD: stator recovery; vane angle vs implied swirl turning; capped η
    private static double EstimateStatorAxialRecovery(
        NozzleDesignInputs d,
        double chamberSwirlNumber,
        List<string> warnings)
    {
        double beta = Math.Abs(d.StatorVaneAngleDeg);
        if (beta < 8.0 || beta > 52.0)
            warnings.Add("Stator vane angle outside sensible range (~8–52°) for this first-order recovery model.");

        double phiSwirlDeg = Math.Atan(Math.Min(chamberSwirlNumber, 3.0)) * (180.0 / Math.PI);
        double matchToSwirl = Math.Exp(-Math.Pow((beta - phiSwirlDeg) / 17.0, 2.0));
        double pragmaticMid = Math.Exp(-Math.Pow((beta - 24.0) / 20.0, 2.0));
        double combinedMatch = 0.52 * matchToSwirl + 0.48 * pragmaticMid;

        double vaneFactor = Math.Clamp(d.StatorVaneCount / 14.0, 0.28, 1.0);
        double overload = chamberSwirlNumber / (2.6 + chamberSwirlNumber);

        double eta = 0.34 + 0.36 * combinedMatch * vaneFactor * (1.0 - 0.26 * overload);
        eta = Math.Clamp(eta, 0.22, 0.82);
        if (eta >= 0.78 && chamberSwirlNumber > 2.2)
            warnings.Add("Stator recovery is near the model cap; unlikely to recover all swirl energy (heuristic cap).");
        return eta;
    }

    private static void WarnInjectorSlotArea(NozzleDesignInputs d, List<string> warnings)
    {
        double slotSum = d.InjectorCount * d.InjectorWidthMm * d.InjectorHeightMm;
        if (slotSum <= 0) return;
        double rel = Math.Abs(slotSum - d.TotalInjectorAreaMm2) / Math.Max(d.TotalInjectorAreaMm2, 1e-9);
        if (rel > 0.08)
        {
            warnings.Add(
                $"Injector slot area (Count×W×H = {slotSum:F1} mm²) differs from TotalInjectorAreaMm2 ({d.TotalInjectorAreaMm2:F1} mm²) by {100.0 * rel:F1}%. " +
                "Physics uses TotalInjectorAreaMm2 for area ratio / continuity; slots are for reference geometry only.");
        }
    }

    private static void WarnRollUnused(NozzleDesignInputs d, List<string> warnings)
    {
        if (Math.Abs(d.InjectorRollAngleDeg) > 1.0)
            warnings.Add("InjectorRollAngleDeg is non-zero but physics ignores roll for axisymmetric direction (see SwirlMath).");
    }

    private static void RunDesignSanityChecks(
        NozzleInput input,
        NozzleSolvedState s,
        List<string> warnings)
    {
        var d = input.Design;
        var src = input.Source;

        if (d.TotalInjectorAreaMm2 > src.SourceOutletAreaMm2)
            warnings.Add("Injector area exceeds source area: TotalInjectorAreaMm2 > SourceOutletAreaMm2.");

        double sourceD = AreaMath.CircleDiameterFromAreaMm2(src.SourceOutletAreaMm2);
        if (d.InletDiameterMm < 1.02 * sourceD)
            warnings.Add("Inlet too small for intended entrainment: inlet diameter only marginally larger than equivalent source diameter.");

        if (d.InletDiameterMm < 1.06 * sourceD && s.EntrainmentRatio < 0.10)
            warnings.Add("Inlet too small for intended entrainment: low entrainment ratio with tight inlet vs source size.");

        if (d.WallThicknessMm < 1.8)
            warnings.Add("Wall thickness is very thin for robust hardware/geometry.");

        double ld = d.SwirlChamberLengthMm / Math.Max(d.SwirlChamberDiameterMm, 1e-9);
        if (ld < 0.35)
            warnings.Add("Swirl chamber L/D is very low; mixing model assumes insufficient length.");

        if (s.EntrainmentRatio > 1.05 && s.MixedVelocityMps < 0.55 * src.SourceVelocityMps)
            warnings.Add("High entrainment with strongly reduced mixed velocity; check loss model and inputs.");

        if (s.ExtraThrustN < 0.0)
            warnings.Add("Final thrust is below source-only thrust for this first-order model.");
    }

    private static void Validate(NozzleDesignInputs d, SourceInputs source)
    {
        if (source.SourceOutletAreaMm2 <= 0) throw new ArgumentOutOfRangeException(nameof(source.SourceOutletAreaMm2));
        if (source.MassFlowKgPerSec <= 0) throw new ArgumentOutOfRangeException(nameof(source.MassFlowKgPerSec));
        if (source.SourceVelocityMps <= 0) throw new ArgumentOutOfRangeException(nameof(source.SourceVelocityMps));
        if (source.AmbientPressurePa <= 0) throw new ArgumentOutOfRangeException(nameof(source.AmbientPressurePa));
        if (source.AmbientTemperatureK <= 0) throw new ArgumentOutOfRangeException(nameof(source.AmbientTemperatureK));
        if (source.AmbientDensityKgPerM3 <= 0) throw new ArgumentOutOfRangeException(nameof(source.AmbientDensityKgPerM3));

        if (d.InletDiameterMm <= 0) throw new ArgumentOutOfRangeException(nameof(d.InletDiameterMm));
        if (d.SwirlChamberDiameterMm <= 0 || d.SwirlChamberLengthMm <= 0) throw new ArgumentOutOfRangeException(nameof(d.SwirlChamberDiameterMm));
        if (d.InjectorAxialPositionRatio < 0.0 || d.InjectorAxialPositionRatio > 1.0)
            throw new ArgumentOutOfRangeException(nameof(d.InjectorAxialPositionRatio), "Must be in [0, 1] (chamber upstream → downstream).");
        if (d.TotalInjectorAreaMm2 <= 0) throw new ArgumentOutOfRangeException(nameof(d.TotalInjectorAreaMm2));
        if (d.InjectorCount <= 0) throw new ArgumentOutOfRangeException(nameof(d.InjectorCount));
        if (d.InjectorWidthMm <= 0 || d.InjectorHeightMm <= 0) throw new ArgumentOutOfRangeException(nameof(d.InjectorWidthMm));
        if (d.ExpanderLengthMm <= 0 || d.ExitDiameterMm <= 0) throw new ArgumentOutOfRangeException(nameof(d.ExpanderLengthMm));
        if (d.StatorVaneCount <= 0) throw new ArgumentOutOfRangeException(nameof(d.StatorVaneCount));
        if (d.WallThicknessMm <= 0) throw new ArgumentOutOfRangeException(nameof(d.WallThicknessMm));
    }
}
