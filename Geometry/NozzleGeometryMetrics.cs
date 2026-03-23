using System;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

/// <summary>Derived radii / stations shared by SI coupling and voxel builders (first-order, mm).</summary>
public static class NozzleGeometryMetrics
{
    /// <summary>Inner gas-path radius at expander exit plane [mm] — matches <see cref="ExpanderBuilder"/>.</summary>
    public static double ExpanderEndInnerRadiusMm(NozzleDesignInputs d)
    {
        double rCh = 0.5 * Math.Max(d.SwirlChamberDiameterMm, 1e-6);
        double L = Math.Max(d.ExpanderLengthMm, 0.0);
        double halfRad = d.ExpanderHalfAngleDeg * (Math.PI / 180.0);
        return rCh + Math.Tan(halfRad) * L;
    }
}
