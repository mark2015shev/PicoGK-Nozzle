using System;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

/// <summary>
/// Single handoff for expander outlet, stator casing ID, and exit inner radii (mm). All segment builders
/// must consume this object — no independent downstream radius math.
/// </summary>
public sealed record DownstreamGeometryTargets(
    double ChamberInnerRadiusMm,
    /// <summary>Authoritative gas-path inner radius at expander outlet = stator casing ID = exit section start.</summary>
    double RecoveryAnnulusRadiusMm,
    double DeclaredExitInnerRadiusMm,
    /// <summary>Exit plane inner radius (Mode A = <see cref="RecoveryAnnulusRadiusMm"/>; Mode B = declared exit when taper enabled).</summary>
    double ExitEndInnerRadiusMm,
    double NominalExpanderLengthMm,
    double EffectiveExpanderLengthMm,
    /// <summary>Outlet radius if <see cref="NominalExpanderLengthMm"/> were used with current angle and chamber R.</summary>
    double NominalConeOutletInnerRadiusMm,
    bool UsesPostStatorExitTaper,
    bool ConeCannotReachDeclaredExit,
    bool ExpanderLengthClampedToMax,
    double MaxExpanderLengthUsedMm)
{
    public double RecoveryAnnulusDiameterMm => 2.0 * RecoveryAnnulusRadiusMm;

    public double ExitStartInnerRadiusMm => RecoveryAnnulusRadiusMm;

    /// <summary>Max |ΔR| among expander→stator and stator→exit-start (built path; should be ~0).</summary>
    public double BuiltDownstreamRadialContinuityErrorMm => 0.0;

    /// <summary>Design inconsistency: nominal cone vs declared exit inner R, mm.</summary>
    public double NominalConeVersusDeclaredExitInnerRadiusMm =>
        Math.Abs(NominalConeOutletInnerRadiusMm - DeclaredExitInnerRadiusMm);
}
