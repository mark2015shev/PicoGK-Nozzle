using System;
using System.Collections.Generic;
using PicoGK_Run.Core;

namespace PicoGK_Run.Physics;

/// <summary>
/// Injector / chamber pressure bookkeeping for reporting (ideal-gas injector exit static; march-coupled chamber P).
/// Not CFD — mixing, turbulence, and 3D secondary flows are not resolved.
/// </summary>
public sealed class InjectorPressureVelocityDiagnostics
{
    /// <summary>Total pressure used as jet-source upstream stagnation pressure [Pa].</summary>
    public double InjectorUpstreamTotalPressurePa { get; init; }

    /// <summary>Plain-language definition of how <see cref="SourceInputs.PressureRatio"/> enters the model.</summary>
    public string PressureRatioDefinition { get; init; } = "";

    public double AmbientStaticPressurePa { get; init; }

    /// <summary>Static pressure passed into <see cref="JetSource"/> (current model: ambient) [Pa].</summary>
    public double JetSourceReferenceStaticPressurePa { get; init; }

    public double InjectorJetVelocityRawMps { get; init; }
    public double InjectorJetVelocityEffectiveMps { get; init; }
    public double InjectorVaEffectiveMps { get; init; }
    public double InjectorVtEffectiveMps { get; init; }
    public double InjectorVelocityMagnitudeEffectiveMps { get; init; }

    /// <summary>q = 0.5·ρ·V² using raw blended driver speed [Pa].</summary>
    public double InjectorDynamicPressureRawPa { get; init; }

    /// <summary>q = 0.5·ρ·V² using scalar effective speed after Cd/turning [Pa].</summary>
    public double InjectorDynamicPressureEffectiveScalarPa { get; init; }

    /// <summary>q = 0.5·ρ·(Va²+Vt²) using decomposed effective injector components [Pa].</summary>
    public double InjectorDynamicPressureFromComponentsPa { get; init; }

    public double InjectorTotalPressureLossModelPa { get; init; }

    /// <summary>
    /// Ideal-gas static pressure at effective |V| = √(Va²+Vt²): T = T₀ − V²/(2cₚ), P = P₀′(T/T₀)^(γ/(γ−1))
    /// with P₀′ = P₀_upstream − Δp₀(<see cref="InjectorLossModel"/>). Same γ, cₚ as <see cref="GasProperties"/> / march.
    /// </summary>
    public double InjectorExitStaticPressureFirstOrderPa { get; init; }

    public string InjectorExitStaticPressureAssumptions { get; init; } = "";

    /// <summary>Mixed static from march step whose axial position is nearest injector station [Pa].</summary>
    public double ChamberStaticPressureNearInjectorPa { get; init; }

    public string ChamberStaticPressureNearInjectorNote { get; init; } = "";

    public double CorePressureDropPa { get; init; }

    /// <summary>Order-one core static vs ambient: P_amb − core_drop (floored) [Pa].</summary>
    public double CoreStaticPressurePa { get; init; }

    public double AmbientMinusCorePressurePa { get; init; }

    /// <summary>Short formula block for the log.</summary>
    public string FormulasUsedSummary { get; init; } = "";

    /// <summary>March inlet static carried into first cell (from <see cref="JetState.PressurePa"/> audit) [Pa].</summary>
    public double MarchInletAssignedStaticPressurePa { get; init; }

    public static InjectorPressureVelocityDiagnostics Compute(
        SourceInputs source,
        GasProperties gas,
        double injectorTotalTemperatureK,
        double ambientStaticPressurePa,
        double injectorUpstreamTotalPressurePa,
        double jetSourceReferenceStaticPressurePa,
        double rhoAtInjectorKgM3,
        double injectorJetVelocityRawMps,
        double injectorJetVelocityEffectiveMps,
        double vaEffectiveMps,
        double vtEffectiveMps,
        double injectorYawDeg,
        IReadOnlyList<FlowMarchStepResult> marchSteps,
        IReadOnlyList<FlowStepState>? physicsMarchSteps,
        double chamberLengthM,
        double injectorAxialPositionRatio,
        RadialVortexPressureResult radialChamber,
        double marchInletAssignedStaticPressurePa)
    {
        double rho = Math.Max(rhoAtInjectorKgM3, 1e-9);
        double vRaw = Math.Max(Math.Abs(injectorJetVelocityRawMps), 1e-9);
        double vEff = Math.Max(Math.Abs(injectorJetVelocityEffectiveMps), 1e-9);
        double va = vaEffectiveMps;
        double vt = vtEffectiveMps;

        double qRaw = 0.5 * rho * vRaw * vRaw;
        double qEffScalar = 0.5 * rho * vEff * vEff;
        double qComponents = 0.5 * rho * (va * va + vt * vt);

        InjectorLossResult loss = InjectorLossModel.Compute(rho, vRaw, injectorYawDeg);
        double dPLoss = loss.EstimatedTotalPressureLossPa;

        double vMagEff = Math.Sqrt(Math.Max(0.0, va * va + vt * vt));
        double p0AfterLoss = Math.Max(injectorUpstreamTotalPressurePa - dPLoss, 1.0);
        double t0 = Math.Max(injectorTotalTemperatureK, 1.0);
        (double pExit, _) = CompressibleFlowMath.StaticPressureTemperatureFromTotalStagnationAndSpeed(
            gas,
            p0AfterLoss,
            t0,
            vMagEff);
        pExit = SiPressureGuards.ClampStaticPressurePa(pExit);
        if (pExit > injectorUpstreamTotalPressurePa)
            pExit = injectorUpstreamTotalPressurePa;

        string prDef =
            "SourceInputs.PressureRatio [-] is used as: P0_jet_upstream_total ≈ max(P_ambient_static × PressureRatio, P_ambient + 1 Pa). " +
            "That P0 is the JetSource total pressure — it is NOT the post-injector or chamber mixed static pressure. " +
            "JetSource.StaticPressurePa is currently set to ambient static for the isentropic source helper (see JetSourceReferenceStaticPressurePa).";

        string pExitNote =
            "Injector exit P_static: ideal gas, adiabatic energy T=T₀−V²/(2cₚ) and isentropic P=P₀′(T/T₀)^(γ/(γ−1)), " +
            "P₀′ = P0_upstream − InjectorLossModel.EstimatedTotalPressureLossPa, V = |V_eff|, T₀ = jet source total temperature. " +
            "Uniform inviscid core; mixing, secondary flows, and real combustor physics are not resolved — not CFD.";

        double ratio = Math.Clamp(injectorAxialPositionRatio, 0.0, 1.0);
        double xTargetM = ratio * chamberLengthM;
        double pNearInj = marchInletAssignedStaticPressurePa;
        string nearNote = "Fallback: march inlet assigned static (no steps).";
        IReadOnlyList<FlowStepState>? phys = physicsMarchSteps;
        if (phys != null && phys.Count > 0)
        {
            FlowStepState bestP = phys[0];
            double bestAbsP = double.MaxValue;
            foreach (FlowStepState s in phys)
            {
                double d = Math.Abs(s.X - xTargetM);
                if (d < bestAbsP)
                {
                    bestAbsP = d;
                    bestP = s;
                }
            }

            pNearInj = bestP.PStaticPa;
            nearNote =
                $"Solved P_static from march FlowStepState at x≈{bestP.X:F4} m (nearest to injector ratio×L_ch={xTargetM:F4} m); same SI closure as live march.";
            const double pAbsReasonableMaxPaP = 5_000_000.0;
            const double pAbsReasonableMinPaP = 20.0;
            if (!double.IsFinite(pNearInj) || pNearInj < pAbsReasonableMinPaP || pNearInj > pAbsReasonableMaxPaP)
            {
                pNearInj = double.NaN;
                nearNote =
                    "INVALID: nearest march FlowStepState P_static is non-finite or outside [20 Pa, 5 MPa] — not replaced with a plausible clamp.";
            }
        }
        else if (marchSteps != null && marchSteps.Count > 0)
        {
            FlowMarchStepResult best = marchSteps[0];
            double bestAbs = double.MaxValue;
            foreach (FlowMarchStepResult s in marchSteps)
            {
                double d = Math.Abs(s.AxialPositionM - xTargetM);
                if (d < bestAbs)
                {
                    bestAbs = d;
                    best = s;
                }
            }

            pNearInj = best.MixedStaticPressurePa;
            nearNote =
                $"Mixed-stream static from march step at x≈{best.AxialPositionM:F4} m (nearest to injector ratio×L_ch={xTargetM:F4} m); uniform annulus model.";
            const double pAbsReasonableMaxPa = 5_000_000.0;
            const double pAbsReasonableMinPa = 20.0;
            if (!double.IsFinite(pNearInj) || pNearInj < pAbsReasonableMinPa || pNearInj > pAbsReasonableMaxPa)
            {
                pNearInj = double.NaN;
                nearNote =
                    "INVALID: nearest march MixedStaticPressurePa is non-finite or outside [20 Pa, 5 MPa] — not replaced with a plausible clamp.";
            }
        }

        double coreDrop = Math.Max(0.0, radialChamber.CorePressureDropPa);
        double pCore = Math.Max(50.0, ambientStaticPressurePa - coreDrop);
        double ambMinusCore = ambientStaticPressurePa - pCore;

        string formulas =
            "q_raw = 0.5·ρ·V_raw²; q_eff_scalar = 0.5·ρ·V_eff²; q_components = 0.5·ρ·(Va²+Vt²). " +
            "P_exit_static: CompressibleFlowMath.StaticPressureTemperatureFromTotalStagnationAndSpeed(P₀′, T₀, |V_eff|). " +
            "P_core ≈ P_ambient − CorePressureDropPa (radial vortex model). Not CFD.";

        return new InjectorPressureVelocityDiagnostics
        {
            InjectorUpstreamTotalPressurePa = injectorUpstreamTotalPressurePa,
            PressureRatioDefinition = prDef,
            AmbientStaticPressurePa = ambientStaticPressurePa,
            JetSourceReferenceStaticPressurePa = jetSourceReferenceStaticPressurePa,
            InjectorJetVelocityRawMps = injectorJetVelocityRawMps,
            InjectorJetVelocityEffectiveMps = injectorJetVelocityEffectiveMps,
            InjectorVaEffectiveMps = va,
            InjectorVtEffectiveMps = vt,
            InjectorVelocityMagnitudeEffectiveMps = vMagEff,
            InjectorDynamicPressureRawPa = qRaw,
            InjectorDynamicPressureEffectiveScalarPa = qEffScalar,
            InjectorDynamicPressureFromComponentsPa = qComponents,
            InjectorTotalPressureLossModelPa = dPLoss,
            InjectorExitStaticPressureFirstOrderPa = pExit,
            InjectorExitStaticPressureAssumptions = pExitNote,
            ChamberStaticPressureNearInjectorPa = pNearInj,
            ChamberStaticPressureNearInjectorNote = nearNote,
            CorePressureDropPa = coreDrop,
            CoreStaticPressurePa = pCore,
            AmbientMinusCorePressurePa = ambMinusCore,
            FormulasUsedSummary = formulas,
            MarchInletAssignedStaticPressurePa = marchInletAssignedStaticPressurePa
        };
    }
}
