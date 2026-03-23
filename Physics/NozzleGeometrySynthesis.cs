using System;
using PicoGK_Run.Core;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Physics;

/// <summary>
/// Builds a <see cref="NozzleDesignInputs"/> from turbine boundary data + a template (injector count, wall, etc.).
/// <para>
/// <b>Intent:</b> move key diameters and angles off “hand tuning” onto <b>documented first-order rules</b>
/// (jet size, swirl number, target entrainment, gentle expander, stator alignment). This is <b>not</b> CFD and
/// <b>not</b> a global numerical optimizer — it is an engineering synthesizer you can replace with search/CFD later.
/// </para>
/// </summary>
public static class NozzleGeometrySynthesis
{
    /// <summary>Default target secondary/core mass-flow ratio for sizing passages (model-scale).</summary>
    public const double DefaultTargetEntrainmentRatio = 0.42;

    /// <summary>
    /// Overwrites template fields that should follow physics; keeps injector count, wall thickness, roll, slot aspect when sensible.
    /// </summary>
    public static NozzleDesignInputs Synthesize(
        SourceInputs source,
        NozzleDesignInputs template,
        double targetEntrainmentRatio = DefaultTargetEntrainmentRatio)
    {
        targetEntrainmentRatio = Math.Clamp(targetEntrainmentRatio, 0.05, 1.2);

        double aSourceMm2 = Math.Max(source.SourceOutletAreaMm2, 1.0);
        double dJetMm = 2.0 * Math.Sqrt(aSourceMm2 / Math.PI);
        double mdot = Math.Max(source.MassFlowKgPerSec, 1e-6);
        double vCore = source.SourceVelocityMps > 1.0
            ? source.SourceVelocityMps
            : VelocityMath.FromMassFlow(mdot, source.AmbientDensityKgPerM3, aSourceMm2 / 1e6);

        double yaw = template.InjectorYawAngleDeg;
        double pitch = template.InjectorPitchAngleDeg;
        var (vt, va) = SwirlMath.ResolveInjectorComponents(vCore, yaw, pitch);
        double swirlNumber = SwirlMath.InjectorSwirlNumber(vt, va);

        int nInj = Math.Max(1, template.InjectorCount);
        double totalInjArea = Math.Min(aSourceMm2 * 0.995, Math.Max(template.TotalInjectorAreaMm2, aSourceMm2 * 0.85));
        const double injectorToBoreAreaMargin = 1.06;
        double dMinForPortsMm = 2.0 * Math.Sqrt((totalInjArea * injectorToBoreAreaMargin) / Math.PI);

        // --- Swirl chamber: slightly larger than jet so shear + swirl can “see” the wall (not a tight throat).
        double chamberScale = 1.02 + 0.11 * Math.Tanh(swirlNumber * 0.45) + 0.06 * Math.Sqrt(targetEntrainmentRatio);
        chamberScale = Math.Clamp(chamberScale, 0.92, 1.28);
        double dChamberMm = Math.Clamp(dJetMm * chamberScale, dJetMm * 0.9, dJetMm * 1.32);
        dChamberMm = Math.Max(dChamberMm, dMinForPortsMm);

        // --- Chamber length: shorter envelope by default (compact vortex volume); still scales with D and ER.
        double lambda = 0.92 + 0.14 * (1.0 - 1.0 / (1.0 + targetEntrainmentRatio));
        lambda = Math.Clamp(lambda, 0.72, 1.12);
        double lChamberMm = Math.Clamp(lambda * dChamberMm, 28.0, 88.0);
        lChamberMm = Math.Max(lChamberMm, Math.Min(95.0, 0.98 * dChamberMm));

        // --- Inlet: capture openness σ = (D_in/D_ch)² ; favor σ > 1 for ambient mouth ≥ bore (bellmouth rule in geometry).
        double sigma = 1.22 + 0.28 * targetEntrainmentRatio - 0.06 * Math.Tanh(swirlNumber * 0.35);
        sigma = Math.Clamp(sigma, 1.05, 2.0);
        double dInletMm = Math.Sqrt(sigma) * dChamberMm;
        dInletMm = Math.Clamp(dInletMm, dChamberMm * 1.01, dChamberMm * 1.55);

        // --- Exit: area scales with (1+ER) and axial speed floor (bulk continuity hint, not Navier–Stokes).
        double vAxialHint = Math.Max(85.0, vCore / (1.35 + 0.22 * swirlNumber));
        double areaScale = (1.0 + targetEntrainmentRatio) * (vCore / Math.Max(vAxialHint, 40.0));
        areaScale = Math.Clamp(areaScale, 1.12, 2.0);
        double dExitMm = Math.Clamp(dChamberMm * Math.Sqrt(areaScale * 0.92), dChamberMm * 1.05, dChamberMm * 1.62);

        // --- Expander: higher swirl → shallower cone to limit separation risk on rotating flow.
        double halfAngleDeg = 14.5 / (1.0 + 0.28 * swirlNumber) - 0.35 * Math.Tanh(targetEntrainmentRatio - 0.35);
        halfAngleDeg = Math.Clamp(halfAngleDeg, 4.5, 11.0);
        double rCh = 0.5 * dChamberMm;
        double rExit = 0.5 * dExitMm;
        double halfRad = halfAngleDeg * (Math.PI / 180.0);
        double tanA = Math.Tan(halfRad);
        double lExpanderMm = tanA > 1e-4
            ? (rExit - rCh) / tanA
            : 80.0;
        lExpanderMm = Math.Clamp(lExpanderMm, 45.0, 200.0);

        // If geometry asks for contraction in cone (r_exit < r_ch), flatten angle / shorten — cone must diverge for this concept.
        if (rExit <= rCh + 0.5)
        {
            rExit = rCh + 8.0;
            dExitMm = 2.0 * rExit;
            lExpanderMm = Math.Clamp((rExit - rCh) / Math.Max(tanA, 0.06), 40.0, 180.0);
        }

        // --- Stator: first-order alignment — turn toward axial; scale with injector yaw and mild L/D factor.
        double ld = lChamberMm / Math.Max(dChamberMm, 1e-6);
        double statorDeg = yaw * (0.32 + 0.04 * Math.Tanh(ld - 1.0)) + 6.0 * Math.Tanh(swirlNumber * 0.25);
        statorDeg = Math.Clamp(statorDeg, 18.0, 52.0);

        double slotH = Math.Max(template.InjectorHeightMm, 1.0);
        double slotW = totalInjArea / (nInj * slotH);

        return new NozzleDesignInputs
        {
            InletDiameterMm = dInletMm,
            SwirlChamberDiameterMm = dChamberMm,
            SwirlChamberLengthMm = lChamberMm,
            InjectorAxialPositionRatio = template.InjectorAxialPositionRatio,
            TotalInjectorAreaMm2 = totalInjArea,
            InjectorCount = template.InjectorCount,
            InjectorWidthMm = Math.Max(2.0, slotW),
            InjectorHeightMm = slotH,
            InjectorYawAngleDeg = template.InjectorYawAngleDeg,
            InjectorPitchAngleDeg = template.InjectorPitchAngleDeg,
            InjectorRollAngleDeg = template.InjectorRollAngleDeg,
            ExpanderLengthMm = lExpanderMm,
            ExpanderHalfAngleDeg = halfAngleDeg,
            ExitDiameterMm = dExitMm,
            StatorVaneAngleDeg = statorDeg,
            StatorVaneCount = Math.Max(8, template.StatorVaneCount),
            StatorHubDiameterMm = template.StatorHubDiameterMm > 0.5
                ? template.StatorHubDiameterMm
                : 0.28 * dChamberMm,
            StatorAxialLengthMm = template.StatorAxialLengthMm > 1.0
                ? template.StatorAxialLengthMm
                : Math.Max(14.0, 0.11 * dChamberMm),
            StatorBladeChordMm = template.StatorBladeChordMm > 0.5
                ? template.StatorBladeChordMm
                : Math.Max(4.0, 0.13 * dChamberMm),
            WallThicknessMm = template.WallThicknessMm
        };
    }
}
