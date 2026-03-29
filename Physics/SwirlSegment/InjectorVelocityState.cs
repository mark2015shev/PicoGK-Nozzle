namespace PicoGK_Run.Physics.SwirlSegment;

/// <summary>
/// Axisymmetric injector exit velocity decomposition. Roll does not change direction in this reduced-order model.
/// Radial component is meridional (inward toward axis positive per <see cref="SwirlMath.DecomposeInjectorVelocityMps"/>).
/// </summary>
public sealed class InjectorVelocityState
{
    /// <summary>|V| consistent with mass continuity at the injector plane [m/s].</summary>
    public double VelocityMagnitudeMps { get; init; }

    public double AxialVelocityMps { get; init; }
    public double TangentialVelocityMps { get; init; }

    /// <summary>Meridional radial velocity (positive = toward chamber axis in pitch plane) [m/s].</summary>
    public double RadialVelocityMps { get; init; }

    /// <summary>|Vt|/max(|Vx|, floor) — avoid singular labels at pure tangential injection.</summary>
    public double SwirlRatioVtOverVx { get; init; }

    /// <summary>Angle between velocity vector and +x (axis); atan2(sqrt(Vt²+Vr²), Vx) [deg].</summary>
    public double FlowAngleDeg { get; init; }
}
