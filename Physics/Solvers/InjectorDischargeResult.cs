using PicoGK_Run.Physics.SwirlSegment;

namespace PicoGK_Run.Physics.Solvers;

/// <summary>Stage 1 — pressure-driven injector discharge (orifice-style) + yaw/pitch decomposition.</summary>
public sealed class InjectorDischargeResult
{
    public double DischargeCoefficient { get; init; }
    public double TotalPressureUpstreamPa { get; init; }
    public double ChamberReferenceStaticPressurePa { get; init; }
    /// <summary>ΔP = P0_upstream − P_chamber ref used for discharge [Pa].</summary>
    public double DrivingPressureDropPa { get; init; }
    /// <summary>ΔP implied by measured ṁ if ṁ is taken as authoritative [Pa].</summary>
    public double ImpliedDeltaPFromMassFlowPa { get; init; }
    public double MassFlowKgS { get; init; }
    public double InjectorAreaM2 { get; init; }
    public double DensityKgM3 { get; init; }
    /// <summary>|V| = ṁ/(ρ A_inj) after authoritative ṁ choice [m/s].</summary>
    public double VelocityMagnitudeFromContinuityMps { get; init; }
    public double EffectiveVelocityMagnitudeMps { get; init; }
    public double AxialVelocityMps { get; init; }
    public double TangentialVelocityMps { get; init; }
    public double SwirlNumberVtOverVa { get; init; }

    /// <summary>Explicit Vx, Vt, Vr decomposition carried through the swirl segment.</summary>
    public InjectorVelocityState? VelocityState { get; init; }

    /// <summary>Equals <see cref="VelocityMagnitudeFromContinuityMps"/>; legacy field name retained for callers.</summary>
    public double LegacyBlendedDriverVelocityMps { get; init; }
    public string Notes { get; init; } = "";
}
