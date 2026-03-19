using System;
using PicoGK_Run.Core;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Physics;

public sealed class NozzlePhysicsSolver
{
    public NozzleSolvedState Solve(NozzleInput input)
    {
        SourceInputs source = input.Source;
        AmbientAir ambient = input.Ambient;
        NozzleDesignInputs design = input.Design;

        Validate(design, ambient, source);

        // SourceOutletAreaMm2 is authoritative by design.
        double sourceAreaMm2 = source.SourceOutletAreaMm2;
        double injectorAreaMm2 = design.TotalInjectorAreaMm2;

        double sourceAreaM2 = AreaMath.ToSquareMeters(sourceAreaMm2);
        double injectorAreaM2 = AreaMath.ToSquareMeters(injectorAreaMm2);
        double inletAreaM2 = AreaMath.ToSquareMeters(AreaMath.CircleAreaMM2(design.InletDiameterMm));
        double chamberAreaMm2 = AreaMath.CircleAreaMM2(design.SwirlChamberDiameterMm);
        double chamberAreaM2 = AreaMath.ToSquareMeters(chamberAreaMm2);

        double sourceCoreVelocity = source.SourceVelocityMps > 0.0
            ? source.SourceVelocityMps
            : VelocityMath.FromMassFlow(source.MassFlowKgPerSec, ambient.DensityKgPerM3, sourceAreaM2);

        // Injector velocity from mass-flow continuity over total injector area.
        double injectorJetVelocity = VelocityMath.FromMassFlow(
            source.MassFlowKgPerSec,
            ambient.DensityKgPerM3,
            Math.Max(injectorAreaM2, 1e-9));

        var components = SwirlMath.ResolveComponents(
            injectorJetVelocity,
            design.InjectorYawAngleDeg,
            design.InjectorPitchAngleDeg,
            design.InjectorRollAngleDeg);

        double tangentialVelocity = components.TangentialMps;
        double axialVelocity = components.AxialMps;
        double swirlStrength = SwirlMath.EstimateStrength(tangentialVelocity, axialVelocity);

        // Entrainment heuristic:
        // - Inlet area surplus creates capture potential.
        // - Chamber residence and swirl improve momentum transfer/mixing.
        double inletToSourceRatio = inletAreaM2 / Math.Max(sourceAreaM2, 1e-9);
        double chamberResidenceRatio = design.SwirlChamberLengthMm / Math.Max(design.SwirlChamberDiameterMm, 1e-9);
        double capturePotential = Math.Max(0.0, inletToSourceRatio - 1.0);
        double swirlAssist = Math.Clamp(swirlStrength / (1.0 + swirlStrength), 0.0, 1.0);

        double entrainmentRatio =
            capturePotential *
            (0.20 + (0.35 * Math.Clamp(chamberResidenceRatio, 0.3, 3.0))) *
            (1.00 + (0.35 * swirlAssist));
        entrainmentRatio = Math.Clamp(entrainmentRatio, 0.0, 3.0);

        double ambientAirMassFlow = source.MassFlowKgPerSec * entrainmentRatio;
        double mixedMassFlow = source.MassFlowKgPerSec + ambientAirMassFlow;

        // Momentum-based mixed velocity (ambient initially near zero axial momentum).
        double mixedVelocityIdeal = (source.MassFlowKgPerSec * sourceCoreVelocity) / Math.Max(mixedMassFlow, 1e-9);
        double lossFraction = PressureLossMath.EstimateLossFraction(
            injectorAreaMm2 / Math.Max(sourceAreaMm2, 1e-9),
            chamberResidenceRatio,
            swirlStrength);
        double mixedVelocity = mixedVelocityIdeal * (1.0 - lossFraction);

        double expansionEfficiency = EstimateExpansionEfficiency(design, source);
        double axialRecoveryEfficiency = EstimateAxialRecoveryEfficiency(design, swirlStrength);
        double exitVelocity = mixedVelocity * expansionEfficiency * axialRecoveryEfficiency;

        // Thrust approximation: F ~= mdot * V (pressure thrust omitted in this first-order model).
        double sourceOnlyThrust = source.MassFlowKgPerSec * sourceCoreVelocity;
        double finalThrust = mixedMassFlow * exitVelocity;
        double extraThrust = finalThrust - sourceOnlyThrust;
        double thrustGainRatio = finalThrust / Math.Max(sourceOnlyThrust, 1e-9);

        return new NozzleSolvedState
        {
            SourceAreaMm2 = sourceAreaMm2,
            TotalInjectorAreaMm2 = injectorAreaMm2,
            InjectorJetVelocityMps = injectorJetVelocity,
            TangentialVelocityComponentMps = tangentialVelocity,
            AxialVelocityComponentMps = axialVelocity,
            SwirlStrength = swirlStrength,
            AmbientAirMassFlowKgPerSec = ambientAirMassFlow,
            EntrainmentRatio = entrainmentRatio,
            MixedMassFlowKgPerSec = mixedMassFlow,
            MixedVelocityMps = mixedVelocity,
            ExpansionEfficiency = expansionEfficiency,
            AxialRecoveryEfficiency = axialRecoveryEfficiency,
            ExitVelocityMps = exitVelocity,
            SourceOnlyThrustN = sourceOnlyThrust,
            FinalThrustN = finalThrust,
            ExtraThrustN = extraThrust,
            ThrustGainRatio = thrustGainRatio
        };
    }

    private static double EstimateExpansionEfficiency(NozzleDesignInputs d, SourceInputs source)
    {
        // Heuristic: near-7..10 deg half-angle and sufficient expansion length
        // tend to improve recoverable expansion quality.
        double angle = d.ExpanderHalfAngleDeg;
        double angleMatch = Math.Exp(-Math.Pow((angle - 8.0) / 5.0, 2.0));
        double lengthScale = d.ExpanderLengthMm / Math.Max(d.SwirlChamberDiameterMm, 1e-9);
        double lengthFactor = 1.0 - Math.Exp(-Math.Max(0.0, lengthScale));

        double sourceDiameterMm = source.SourceOutletDiameterMm ?? AreaMath.CircleDiameterFromAreaMm2(source.SourceOutletAreaMm2);
        double expansionRatio = d.ExitDiameterMm / Math.Max(sourceDiameterMm, 1e-9);
        double ratioMatch = Math.Exp(-Math.Pow((expansionRatio - 1.6) / 0.8, 2.0));
        double pressurePotential = Math.Clamp((source.PressureRatio - 1.0) / 2.5, 0.0, 1.2);

        double eta = 0.30 + (0.35 * angleMatch * lengthFactor) + (0.20 * ratioMatch) + (0.10 * pressurePotential);
        return Math.Clamp(eta, 0.20, 0.96);
    }

    private static double EstimateAxialRecoveryEfficiency(NozzleDesignInputs d, double swirlStrength)
    {
        // Heuristic de-swirl model:
        // - moderate stator angle and enough vane count recover axial momentum.
        // - very high incoming swirl is harder to straighten.
        double angleMatch = Math.Exp(-Math.Pow((Math.Abs(d.StatorVaneAngleDeg) - 28.0) / 14.0, 2.0));
        double vaneFactor = Math.Clamp(d.StatorVaneCount / 12.0, 0.25, 1.0);
        double swirlPenalty = 1.0 / (1.0 + (0.12 * swirlStrength * swirlStrength));

        double recovery = 0.40 + (0.45 * angleMatch * vaneFactor * swirlPenalty);
        return Math.Clamp(recovery, 0.20, 0.98);
    }

    private static void Validate(NozzleDesignInputs d, AmbientAir ambient, SourceInputs source)
    {
        if (source.SourceOutletAreaMm2 <= 0) throw new ArgumentOutOfRangeException(nameof(source.SourceOutletAreaMm2));
        if (source.MassFlowKgPerSec <= 0) throw new ArgumentOutOfRangeException(nameof(source.MassFlowKgPerSec));
        if (source.SourceVelocityMps <= 0) throw new ArgumentOutOfRangeException(nameof(source.SourceVelocityMps));

        if (d.InletDiameterMm <= 0) throw new ArgumentOutOfRangeException(nameof(d.InletDiameterMm));
        if (d.SwirlChamberDiameterMm <= 0 || d.SwirlChamberLengthMm <= 0) throw new ArgumentOutOfRangeException("Swirl chamber dimensions must be > 0.");
        if (d.TotalInjectorAreaMm2 <= 0) throw new ArgumentOutOfRangeException(nameof(d.TotalInjectorAreaMm2));
        if (d.InjectorCount <= 0) throw new ArgumentOutOfRangeException(nameof(d.InjectorCount));
        if (d.InjectorWidthMm <= 0 || d.InjectorHeightMm <= 0) throw new ArgumentOutOfRangeException("Injector dimensions must be > 0.");
        if (d.ExpanderLengthMm <= 0 || d.ExitDiameterMm <= 0) throw new ArgumentOutOfRangeException("Expander/exit dimensions must be > 0.");
        if (d.StatorVaneCount <= 0) throw new ArgumentOutOfRangeException(nameof(d.StatorVaneCount));

        if (ambient.DensityKgPerM3 <= 0) throw new ArgumentOutOfRangeException(nameof(ambient.DensityKgPerM3));
    }
}

