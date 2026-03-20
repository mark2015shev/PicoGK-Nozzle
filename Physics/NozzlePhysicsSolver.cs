using System;
using System.Collections.Generic;
using PicoGK_Run.Core;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Physics;

/// <summary>
/// Physics-first parametric nozzle / ejector estimator. All non-trivial sub-models are
/// explicit heuristics (labeled in code); not CFD, not experimentally fitted to hardware.
/// </summary>
public sealed class NozzlePhysicsSolver
{
    private const double SpecificGasConstantAirJPerKgK = 287.0;

    public PhysicsSolveResult Solve(NozzleInput input)
    {
        List<string> warnings = new();
        SourceInputs source = input.Source;
        NozzleDesignInputs design = input.Design;

        Validate(design, source);

        WarnInjectorSlotArea(design, warnings);
        WarnRollUnused(design, warnings);

        // --- Model choice (not a "heuristic", but a simplification): thrust baseline ---
        // Source-only thrust uses F0 ≈ mdot_core * V_core with no pressure-thrust / area term.

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

        // Continuity at injectors (steady, uniform): mdot_core = rho_core * A_inj * V_inj.
        // rho_core comes from EstimateCoreGasDensityKgPerM3 (heuristic ideal gas when T_exhaust set).
        double injectorJetVelocity = coreMdot / (rhoCore * Math.Max(injectorAreaM2, 1e-12));

        var (vTan, vAx) = SwirlMath.ResolveInjectorComponents(
            injectorJetVelocity,
            design.InjectorYawAngleDeg,
            design.InjectorPitchAngleDeg);

        double injectorSwirlNumber = SwirlMath.InjectorSwirlNumber(vTan, vAx);

        double chamberLd = design.SwirlChamberLengthMm / Math.Max(design.SwirlChamberDiameterMm, 1e-9);

        // -------------------------------------------------------------------------
        // HEURISTIC — NOT CFD-CALIBRATED: chamber swirl decay before stator model
        // First-order exponential decay in L/D; coefficient 0.55 is a placeholder.
        // -------------------------------------------------------------------------
        double chamberSwirlForStator = injectorSwirlNumber * Math.Exp(-0.55 * Math.Clamp(chamberLd, 0.0, 4.0));
        chamberSwirlForStator = Math.Clamp(chamberSwirlForStator, 0.0, 8.0);

        // -------------------------------------------------------------------------
        // HEURISTIC — NOT CFD-CALIBRATED: entrainment (secondary / ambient air ingestion)
        // Algebraic ejector-style estimate from inlet capture, chamber openness, L/D, and
        // injector swirl number. Coefficients (0.11, 0.16, caps) are not from CFD.
        // -------------------------------------------------------------------------
        double inletToSource = inletAreaM2 / Math.Max(sourceAreaM2, 1e-12);
        double chamberToSource = chamberAreaM2 / Math.Max(sourceAreaM2, 1e-12);
        double capture = Math.Clamp(Math.Max(0.0, inletToSource - 1.0), 0.0, 6.0);
        double chamberOpenness = Math.Clamp(Math.Sqrt(chamberToSource), 0.4, 2.2);
        double swirlBoost = injectorSwirlNumber / (1.0 + 0.9 * injectorSwirlNumber);

        double entrainmentRaw =
            0.11 * capture * chamberOpenness
            + 0.16 * Math.Clamp(chamberLd, 0.12, 2.8) * swirlBoost;

        double entrainmentRatio = Math.Clamp(entrainmentRaw, 0.0, 1.35);
        if (entrainmentRatio >= 1.32)
            warnings.Add("Unrealistic entrainment: ratio hit upper heuristic cap (1.35); treat as optimistic.");

        if (entrainmentRatio > 0.90 && inletToSource < 1.12)
            warnings.Add("Unrealistic entrainment: high entrainment ratio with only modest inlet capture (A_inlet/A_source); review inputs or model limits.");

        double ambientMdot = coreMdot * entrainmentRatio;
        double mixedMdot = coreMdot + ambientMdot;

        // Momentum (first-order): ambient drawn axially ~ stagnant → only core contributes axial momentum.
        double mixedVelocityIdeal = coreMdot * vCore / Math.Max(mixedMdot, 1e-12);

        double injectorToSourceArea = injectorAreaMm2 / Math.Max(sourceAreaMm2, 1e-12);
        PressureLossBreakdown loss = PressureLossMath.Compute(injectorToSourceArea, chamberLd, injectorSwirlNumber);

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

        double expansionEfficiency = EstimateExpansionEfficiency(design, source, warnings);
        double axialRecovery = EstimateStatorAxialRecovery(design, chamberSwirlForStator, warnings);

        double vAfterExpansion = mixedVelocity * expansionEfficiency;
        double exitVelocity = vAfterExpansion * axialRecovery;

        double sourceOnlyThrust = coreMdot * vCore;
        double finalThrust = mixedMdot * exitVelocity;
        double extraThrust = finalThrust - sourceOnlyThrust;
        double thrustGainRatio = finalThrust / Math.Max(sourceOnlyThrust, 1e-9);

        NozzleSolvedState state = new()
        {
            CoreMassFlowKgPerSec = coreMdot,
            SourceAreaMm2 = sourceAreaMm2,
            TotalInjectorAreaMm2 = injectorAreaMm2,
            InjectorJetVelocityMps = injectorJetVelocity,
            CoreGasDensityKgPerM3 = rhoCore,
            TangentialVelocityComponentMps = vTan,
            AxialVelocityComponentMps = vAx,
            InjectorSwirlNumber = injectorSwirlNumber,
            ChamberSwirlNumberForStator = chamberSwirlForStator,
            PressureLoss = loss,
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

        RunDesignSanityChecks(input, state, inletToSource, warnings);

        return new PhysicsSolveResult { State = state, Warnings = warnings };
    }

    /// <summary>
    /// <b>HEURISTIC — NOT CFD-CALIBRATED:</b> core gas density at injectors via ideal gas
    /// ρ ≈ (P_amb·π_clamp)/(R·T_exhaust). Used only for continuity V_inj = mdot/(ρ A_inj).
    /// </summary>
    private static double EstimateCoreGasDensityKgPerM3(SourceInputs s, List<string> warnings)
    {
        if (!s.ExhaustTemperatureK.HasValue || s.ExhaustTemperatureK.Value < 250.0)
        {
            warnings.Add("ExhaustTemperatureK missing/low; using AmbientDensityKgPerM3 for injector continuity (conservative/approximate).");
            return s.AmbientDensityKgPerM3;
        }

        double tEx = s.ExhaustTemperatureK.Value;
        double pScale = s.AmbientPressurePa * Math.Clamp(s.PressureRatio, 1.0, 6.0);
        double rho = pScale / (SpecificGasConstantAirJPerKgK * tEx);
        return Math.Clamp(rho, 0.15, 22.0);
    }

    // -------------------------------------------------------------------------
    // HEURISTIC — NOT CFD-CALIBRATED: expansion / divergent section recovery
    // Combines half-angle, length ratio, area expansion ratio, and pressure ratio into one η.
    // Too-steep angle and too-short length reduce η; not a Method-of-Characteristics nozzle solve.
    // -------------------------------------------------------------------------
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

    // -------------------------------------------------------------------------
    // HEURISTIC — NOT CFD-CALIBRATED: stator / axial recovery
    // Maps vane angle, vane count, and chamber-decayed swirl to a single η applied once:
    // V_exit = V_after_expansion * η_stator. Not a blade row CFD / deviation model.
    // -------------------------------------------------------------------------
    private static double EstimateStatorAxialRecovery(
        NozzleDesignInputs d,
        double chamberSwirlNumber,
        List<string> warnings)
    {
        double beta = Math.Abs(d.StatorVaneAngleDeg);
        if (beta < 8.0 || beta > 55.0)
            warnings.Add("Stator vane angle outside sensible range (~8–55°) for this first-order recovery model.");

        double angleMatch = Math.Exp(-Math.Pow((beta - 26.0) / 16.0, 2.0));
        double vaneFactor = Math.Clamp(d.StatorVaneCount / 14.0, 0.28, 1.0);
        double swirlLoad = chamberSwirlNumber / (1.0 + 0.22 * chamberSwirlNumber * chamberSwirlNumber);

        double eta = 0.38 + 0.48 * angleMatch * vaneFactor * (0.55 + 0.45 * (1.0 / (1.0 + 0.35 * swirlLoad)));
        return Math.Clamp(eta, 0.20, 0.97);
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
                "Solver uses TotalInjectorAreaMm2 for continuity; slots are for documentation/geometry.");
        }
    }

    private static void WarnRollUnused(NozzleDesignInputs d, List<string> warnings)
    {
        if (Math.Abs(d.InjectorRollAngleDeg) > 1.0)
            warnings.Add("InjectorRollAngleDeg is non-zero but physics decomposition ignores roll for axisymmetric injector direction (see SwirlMath).");
    }

    private static void RunDesignSanityChecks(
        NozzleInput input,
        NozzleSolvedState s,
        double inletToSourceAreaRatio,
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
        if (d.TotalInjectorAreaMm2 <= 0) throw new ArgumentOutOfRangeException(nameof(d.TotalInjectorAreaMm2));
        if (d.InjectorCount <= 0) throw new ArgumentOutOfRangeException(nameof(d.InjectorCount));
        if (d.InjectorWidthMm <= 0 || d.InjectorHeightMm <= 0) throw new ArgumentOutOfRangeException(nameof(d.InjectorWidthMm));
        if (d.ExpanderLengthMm <= 0 || d.ExitDiameterMm <= 0) throw new ArgumentOutOfRangeException(nameof(d.ExpanderLengthMm));
        if (d.StatorVaneCount <= 0) throw new ArgumentOutOfRangeException(nameof(d.StatorVaneCount));
        if (d.WallThicknessMm <= 0) throw new ArgumentOutOfRangeException(nameof(d.WallThicknessMm));
    }
}
