using System;
using PicoGK_Run.Core;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Physics.Solvers;

/// <summary>
/// Stage 1: ṁ = C_d A √(2 ρ ΔP) with ΔP from upstream total vs chamber reference static;
/// authoritative ṁ comes from <see cref="SourceInputs.MassFlowKgPerSec"/> when specified.
/// Velocity magnitude ṁ/(ρA), then yaw/pitch → V_a, V_t.
/// </summary>
public static class InjectorDischargeSolver
{
    /// <summary>Solve injector state. Chamber static is usually ambient for first-pass SI (iterate in future).</summary>
    public static InjectorDischargeResult Solve(
        SourceInputs source,
        NozzleDesignInputs design,
        double gasDensityKgM3,
        double chamberReferenceStaticPressurePa)
    {
        double rho = Math.Max(gasDensityKgM3, 1e-9);
        double aInj = Math.Max(design.TotalInjectorAreaMm2 * 1e-6, 1e-12);
        double pCh = Math.Max(chamberReferenceStaticPressurePa, 1.0);
        double p0 = Math.Max(source.AmbientPressurePa * source.PressureRatio, pCh + 1.0);
        double dP = Math.Max(p0 - pCh, 0.0);

        double cd = Math.Clamp(ChamberPhysicsCoefficients.InjectorDischargeCoefficient, 0.45, 1.0);
        double mdotOrifice = cd * aInj * Math.Sqrt(2.0 * rho * dP);

        double mdotAuth = source.MassFlowKgPerSec > 1e-12 ? source.MassFlowKgPerSec : mdotOrifice;
        double impliedDp = mdotAuth > 1e-18 && cd * aInj > 1e-18
            ? (mdotAuth / (cd * aInj)) * (mdotAuth / (cd * aInj)) / (2.0 * rho)
            : dP;

        double vCont = mdotAuth / (rho * aInj);

        double sourceAreaM2 = Math.Max(source.SourceOutletAreaMm2 * 1e-6, 1e-12);
        double vCore = source.SourceVelocityMps > 0.0
            ? source.SourceVelocityMps
            : VelocityMath.FromMassFlow(mdotAuth, source.AmbientDensityKgPerM3, sourceAreaM2);
        double areaDriver = vCore * (source.SourceOutletAreaMm2 / Math.Max(design.TotalInjectorAreaMm2, 1e-9));
        double continuityCheck = mdotAuth / (rho * aInj);
        double legacyBlend = NozzlePhysicsSolver.InjectorJetVelocityDriverBlend * areaDriver
            + (1.0 - NozzlePhysicsSolver.InjectorJetVelocityDriverBlend) * continuityCheck;

        double vEff = InjectorLossModel.EffectiveJetVelocityMps(vCont, rho, design.InjectorYawAngleDeg);
        var (vt, va) = SwirlMath.ResolveInjectorComponents(vEff, design.InjectorYawAngleDeg, design.InjectorPitchAngleDeg);
        double s = Math.Abs(vt) / Math.Max(Math.Abs(va), 1e-6);

        string notes = mdotAuth > 1e-12 && Math.Abs(mdotOrifice - mdotAuth) / mdotAuth > 0.12
            ? "Authoritative ṁ from source; orifice ṁ(P0−P_ch) differs >12% — refine P_chamber or C_d vs datasheet."
            : "Discharge consistent with chosen ṁ and chamber reference P.";

        return new InjectorDischargeResult
        {
            DischargeCoefficient = cd,
            TotalPressureUpstreamPa = p0,
            ChamberReferenceStaticPressurePa = pCh,
            DrivingPressureDropPa = dP,
            ImpliedDeltaPFromMassFlowPa = impliedDp,
            MassFlowKgS = mdotAuth,
            InjectorAreaM2 = aInj,
            DensityKgM3 = rho,
            VelocityMagnitudeFromContinuityMps = vCont,
            EffectiveVelocityMagnitudeMps = vEff,
            AxialVelocityMps = va,
            TangentialVelocityMps = vt,
            SwirlNumberVtOverVa = s,
            LegacyBlendedDriverVelocityMps = legacyBlend,
            Notes = notes
        };
    }
}
