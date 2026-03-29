using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;

/// <summary>
/// Single source of truth for axial stations and inner radii used by <see cref="NozzleGeometryBuilder"/>
/// and geometry audits (mm). Downstream radii come from <see cref="DownstreamGeometryResolver"/> only.
/// </summary>
public sealed class GeometryAssemblyPath
{
    public double OverlapMm { get; init; }
    public double WallMm { get; init; }

    public double XInletStart { get; init; }
    public double XAfterInlet { get; init; }
    public double XSwirlStart { get; init; }
    public double XAfterSwirl { get; init; }
    public double XInjectorPlane { get; init; }
    public double XExpanderStart { get; init; }
    public double XAfterExpander { get; init; }
    /// <summary>Equals <see cref="RecoveryAnnulusInnerRadiusMm"/> (authoritative downstream bore).</summary>
    public double ExpanderEndInnerRadiusMm { get; init; }
    public double XStatorStart { get; init; }
    public double XAfterStator { get; init; }
    public double XExitStart { get; init; }
    public double XAfterExit { get; init; }

    public double ChamberInnerRadiusMm { get; init; }
    public double RecoveryAnnulusInnerRadiusMm { get; init; }
    public double DeclaredExitInnerRadiusMm { get; init; }
    public double ExitInnerRadiusStartMm { get; init; }
    public double ExitInnerRadiusEndMm { get; init; }
    public double ExitSectionLengthMm { get; init; }

    public double XLipEnd { get; init; }
    public double EntranceInnerRadiusMm { get; init; }
    public double LipLengthMm { get; init; }
    public double FlareLengthMm { get; init; }

    public bool UsesPostStatorExitTaper { get; init; }

    /// <summary>Physical main chamber length (same as <see cref="SwirlChamberPlacement.PhysicalChamberLengthBuiltMm"/>).</summary>
    public double SwirlChamberPhysicalLengthMm { get; init; }

    /// <summary>Alias for <see cref="SwirlChamberPhysicalLengthMm"/> (legacy name).</summary>
    public double SwirlChamberEffectiveLengthMm { get; init; }

    /// <summary>Shared swirl + injector stations for voxels, SI handoff, and audits.</summary>
    public SwirlChamberPlacement SwirlPlacement { get; init; } = null!;

    public static GeometryAssemblyPath Compute(NozzleDesignInputs d, RunConfiguration? run = null)
    {
        DownstreamGeometryTargets t = DownstreamGeometryResolver.Resolve(d, run);
        double overlap = NozzleGeometryBuilder.AssemblyOverlapMm;
        double wall = Math.Max(d.WallThicknessMm, 0.0);
        double inletD = Math.Max(d.InletDiameterMm, 1.0);
        double chamberD = Math.Max(d.SwirlChamberDiameterMm, 1.0);
        double refD = Math.Max(inletD, chamberD);
        double lipLen = Math.Max(4.0, 0.08 * refD);
        double flareLen = Math.Max(14.0, 0.30 * refD);
        double inletNominalR = 0.5 * inletD;
        double chamberInnerR = 0.5 * chamberD;
        double entranceInnerR = Math.Max(inletNominalR, chamberInnerR);

        double x = 0.0;
        double xLipEnd = x + lipLen;
        double xFlareEnd = xLipEnd + flareLen;
        double xAfterInlet = xFlareEnd;

        SwirlChamberPlacement swirl = SwirlChamberPlacement.Compute(d, xAfterInlet, run);
        double xSwirlStart = swirl.MainChamberStartXMm;
        double xAfterSwirl = swirl.MainChamberEndXMm;
        double chamberLen = swirl.PhysicalChamberLengthBuiltMm;
        double xInjectorPlane = swirl.InjectorPlaneXMm;

        double xExpStart = xAfterSwirl - overlap;
        double expLen = t.EffectiveExpanderLengthMm;
        double expanderEndInnerR = t.RecoveryAnnulusRadiusMm;
        double xAfterExpander = xExpStart + expLen;

        double xStatorStart = xAfterExpander - overlap;
        double innerRStator = Math.Max(0.5, expanderEndInnerR);
        double lenAuto = Math.Max(10.0, 0.10 * Math.Max(innerRStator * 2.0, t.RecoveryAnnulusDiameterMm));
        double statorLen = d.StatorAxialLengthMm > 1.0 ? d.StatorAxialLengthMm : lenAuto;
        double xAfterStator = xStatorStart + statorLen;

        double xExitStart = xAfterStator - overlap;
        double rExit0 = Math.Max(0.5, t.RecoveryAnnulusRadiusMm);
        double rExit1 = Math.Max(0.5, t.ExitEndInnerRadiusMm);
        double exitLen = ExitBuilder.ComputeExitSectionLengthMm((float)rExit0, (float)rExit1);
        double xAfterExit = xExitStart + exitLen;

        return new GeometryAssemblyPath
        {
            OverlapMm = overlap,
            WallMm = wall,
            XInletStart = x,
            XAfterInlet = xAfterInlet,
            XLipEnd = xLipEnd,
            LipLengthMm = lipLen,
            FlareLengthMm = flareLen,
            EntranceInnerRadiusMm = entranceInnerR,
            XSwirlStart = xSwirlStart,
            XAfterSwirl = xAfterSwirl,
            XInjectorPlane = xInjectorPlane,
            XExpanderStart = xExpStart,
            XAfterExpander = xAfterExpander,
            ExpanderEndInnerRadiusMm = expanderEndInnerR,
            ChamberInnerRadiusMm = chamberInnerR,
            RecoveryAnnulusInnerRadiusMm = t.RecoveryAnnulusRadiusMm,
            DeclaredExitInnerRadiusMm = t.DeclaredExitInnerRadiusMm,
            XStatorStart = xStatorStart,
            XAfterStator = xAfterStator,
            XExitStart = xExitStart,
            XAfterExit = xAfterExit,
            ExitInnerRadiusStartMm = rExit0,
            ExitInnerRadiusEndMm = rExit1,
            ExitSectionLengthMm = exitLen,
            UsesPostStatorExitTaper = t.UsesPostStatorExitTaper,
            SwirlChamberPhysicalLengthMm = chamberLen,
            SwirlChamberEffectiveLengthMm = chamberLen,
            SwirlPlacement = swirl
        };
    }
}
