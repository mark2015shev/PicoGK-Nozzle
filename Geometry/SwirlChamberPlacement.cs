using System;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

/// <summary>
/// Authoritative axial placement of the main swirl chamber and injector reference plane.
/// Chamber span comes from duct topology (inlet junction, optional downstream anchor, physical <see cref="NozzleDesignInputs.SwirlChamberLengthMm"/>);
/// injector ratio only positions the plane inside that span.
/// </summary>
public enum SwirlChamberPlacementHealth
{
    Pass,
    Warn,
    Fail
}

public sealed class SwirlChamberPlacement
{
    /// <summary>Inlet flare end minus assembly overlap — earliest nominal start for the main chamber upstream face.</summary>
    public double InletChamberJunctionXMm { get; init; }

    public double MainChamberStartXMm { get; init; }
    public double MainChamberEndXMm { get; init; }

    /// <summary>From design (<see cref="NozzleDesignInputs.SwirlChamberLengthMm"/>), lower-bounded for numerics.</summary>
    public double PhysicalChamberLengthRequestedMm { get; init; }

    /// <summary>Built main chamber axial extent (equals requested; not stretched by rule-of-six or injector ratio).</summary>
    public double PhysicalChamberLengthBuiltMm { get; init; }

    public double UpstreamRetentionLengthMm { get; init; }
    public double UpstreamRetentionStartXMm { get; init; }
    public bool UsesExplicitUpstreamRetentionSection { get; init; }

    public double RequestedInjectorAxialRatio { get; init; }
    public double ClampedInjectorAxialRatio { get; init; }
    public double InjectorPlaneXMm { get; init; }
    public double InjectorDistanceFromChamberUpstreamFaceMm { get; init; }
    public double InjectorDistanceFromChamberDownstreamFaceMm { get; init; }

    /// <summary>How far the main chamber start lies upstream of <see cref="InletChamberJunctionXMm"/> [mm]; 0 = OK.</summary>
    public double ChamberUpstreamOvershootMm { get; init; }

    public SwirlChamberPlacementHealth PlacementHealth { get; init; }

    public static double ClampInjectorAxialRatio(double requested, RunConfiguration? run)
    {
        double lo = run?.InjectorAxialPositionMin ?? 0.15;
        double hi = run?.InjectorAxialPositionMax ?? 0.85;
        if (lo > hi)
            (lo, hi) = (hi, lo);
        return Math.Clamp(requested, lo, hi);
    }

    /// <summary>
    /// Penalty in unified geometry scoring for upstream overshoot. σ = 2 mm scale; capped at 1.2.
    /// </summary>
    public static double ComputeUpstreamOvershootPenalty(double overshootMm)
    {
        if (overshootMm <= 0.001)
            return 0.0;
        const double sigmaMm = 2.0;
        return Math.Min(1.2, 0.2 * Math.Pow(overshootMm / sigmaMm, 1.2));
    }

    /// <param name="xAfterInlet">Axial end of inlet segment (flare end) [mm], same as <see cref="GeometryAssemblyPath.XAfterInlet"/>.</param>
    public static SwirlChamberPlacement Compute(NozzleDesignInputs d, double xAfterInlet, RunConfiguration? run)
    {
        double overlap = NozzleGeometryBuilder.AssemblyOverlapMm;
        double physicalL = Math.Max(d.SwirlChamberLengthMm, 1.0);
        double guard = Math.Max(0.0, d.InjectorUpstreamGuardLengthMm);
        double baseJunction = xAfterInlet - overlap;
        double anchorLen = run?.SwirlChamberLengthDownstreamAnchorMm ?? 0.0;

        double xEnd;
        double xStart;
        if (anchorLen > 0.0)
        {
            xEnd = baseJunction + anchorLen;
            xStart = xEnd - physicalL;
        }
        else
        {
            xStart = baseJunction;
            xEnd = xStart + physicalL;
        }

        double overshoot = Math.Max(0.0, baseJunction - xStart);
        double warnTh = run?.SwirlChamberUpstreamOvershootWarnMm ?? 0.05;
        double hardTh = run?.SwirlChamberUpstreamOvershootHardRejectMm ?? 2.0;

        SwirlChamberPlacementHealth health = overshoot > hardTh
            ? SwirlChamberPlacementHealth.Fail
            : overshoot > warnTh
                ? SwirlChamberPlacementHealth.Warn
                : SwirlChamberPlacementHealth.Pass;

        double reqInj = d.InjectorAxialPositionRatio;
        double clampInj = ClampInjectorAxialRatio(reqInj, run);
        double injX = xStart + clampInj * physicalL;

        double retStart = guard > 0.0 ? xStart - guard : xStart;

        return new SwirlChamberPlacement
        {
            InletChamberJunctionXMm = baseJunction,
            MainChamberStartXMm = xStart,
            MainChamberEndXMm = xEnd,
            PhysicalChamberLengthRequestedMm = physicalL,
            PhysicalChamberLengthBuiltMm = physicalL,
            UpstreamRetentionLengthMm = guard,
            UpstreamRetentionStartXMm = retStart,
            UsesExplicitUpstreamRetentionSection = guard > 0.0,
            RequestedInjectorAxialRatio = reqInj,
            ClampedInjectorAxialRatio = clampInj,
            InjectorPlaneXMm = injX,
            InjectorDistanceFromChamberUpstreamFaceMm = injX - xStart,
            InjectorDistanceFromChamberDownstreamFaceMm = xEnd - injX,
            ChamberUpstreamOvershootMm = overshoot,
            PlacementHealth = health
        };
    }
}
