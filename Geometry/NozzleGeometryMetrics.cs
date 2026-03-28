using System;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

/// <summary>Derived radii / stations shared by SI coupling and voxel builders (first-order, mm).</summary>
public static class NozzleGeometryMetrics
{
    /// <summary>Inner R at expander exit if nominal <see cref="NozzleDesignInputs.ExpanderLengthMm"/> and angle are used (template cone).</summary>
    public static double NominalConeOutletInnerRadiusMm(NozzleDesignInputs d)
    {
        double rCh = 0.5 * Math.Max(d.SwirlChamberDiameterMm, 1e-6);
        double L = Math.Max(d.ExpanderLengthMm, 0.0);
        double halfRad = d.ExpanderHalfAngleDeg * (Math.PI / 180.0);
        return rCh + Math.Tan(halfRad) * L;
    }

    /// <summary>Authoritative recovery annulus inner R after <see cref="DownstreamGeometryResolver"/> (matches voxel build).</summary>
    public static double BuiltRecoveryAnnulusInnerRadiusMm(NozzleDesignInputs d, RunConfiguration? run = null) =>
        DownstreamGeometryResolver.Resolve(d, run).RecoveryAnnulusRadiusMm;

    /// <summary>Same as <see cref="BuiltRecoveryAnnulusInnerRadiusMm"/> with default run flags (legacy call sites).</summary>
    public static double ExpanderEndInnerRadiusMm(NozzleDesignInputs d) => BuiltRecoveryAnnulusInnerRadiusMm(d, null);
}
