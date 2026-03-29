using System;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Geometry;
/// <summary>
/// Authoritative inlet lip + flare axial stations and inner radii (mm). Shared by
/// <see cref="GeometryAssemblyPath"/>, <see cref="InletBuilder"/>, and continuity — no duplicate formulas.
/// </summary>
public readonly record struct InletSegmentStations(
    double XInletStart,
    double XLipEnd,
    double XAfterInlet,
    double LipLengthMm,
    double FlareLengthMm,
    double EntranceInnerRadiusMm,
    double ChamberInnerRadiusMm)
{
    public static InletSegmentStations Compute(NozzleDesignInputs d)
    {
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
        double xAfterInlet = xLipEnd + flareLen;
        return new InletSegmentStations(x, xLipEnd, xAfterInlet, lipLen, flareLen, entranceInnerR, chamberInnerR);
    }
}
