using System;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Core;

/// <summary>
/// Computes <see cref="NozzleCriticalRatiosSnapshot"/> from design + optional solved flow (SI path).
/// </summary>
public static class NozzleCriticalRatios
{
    private const double DegToRad = Math.PI / 180.0;

    public static NozzleCriticalRatiosSnapshot Compute(
        NozzleDesignInputs d,
        SourceInputs? source,
        NozzleSolvedState? solved,
        SiFlowDiagnostics? si)
    {
        double dIn = Math.Max(d.InletDiameterMm, 1e-6);
        double dCh = Math.Max(d.SwirlChamberDiameterMm, 1e-6);
        double lCh = Math.Max(d.SwirlChamberLengthMm, 1e-6);

        double aIn = Math.PI * 0.25 * dIn * dIn;
        double aCh = Math.PI * 0.25 * dCh * dCh;
        double sigma = aIn / aCh;

        double vRef = 600.0;
        if (source != null && source.SourceVelocityMps > 1.0)
            vRef = source.SourceVelocityMps;
        var (vt, va) = SwirlMath.ResolveInjectorComponents(vRef, d.InjectorYawAngleDeg, d.InjectorPitchAngleDeg);
        double vMagRef = Math.Sqrt(vt * vt + va * va);
        double swirlReportScalar = si != null
            ? si.InjectorPlaneSwirlDirective
            : SwirlMath.InjectorSwirlDirective(vt, Math.Max(vMagRef, 1e-9));

        double ld = lCh / dCh;

        double aInj = Math.Max(d.TotalInjectorAreaMm2, 0.0);
        double portToChamber = aCh > 1e-9 ? aInj / aCh : 0.0;

        double rCh = 0.5 * dCh;
        double halfRad = d.ExpanderHalfAngleDeg * DegToRad;
        double rExpEnd = rCh + Math.Tan(halfRad) * d.ExpanderLengthMm;
        double rExitTgt = 0.5 * Math.Max(d.ExitDiameterMm, 1e-6);
        double mismatch = rCh > 1e-6 ? Math.Abs(rExpEnd - rExitTgt) / rCh : 0.0;

        double yawMis = Math.Abs(d.StatorVaneAngleDeg - d.InjectorYawAngleDeg);

        double? er = null;
        if (solved != null && solved.CoreMassFlowKgPerSec > 1e-12)
            er = solved.AmbientAirMassFlowKgPerSec / solved.CoreMassFlowKgPerSec;

        return new NozzleCriticalRatiosSnapshot
        {
            CaptureToChamberAreaRatio = sigma,
            InjectorSwirlNumber = swirlReportScalar,
            InjectorPlaneFluxSwirlNumber = si != null && si.InjectorPlaneFluxSwirlNumber > 0
                ? si.InjectorPlaneFluxSwirlNumber
                : null,
            ChamberSlendernessLD = ld,
            InjectorPortToChamberAreaRatio = portToChamber,
            ExpanderHalfAngleDeg = d.ExpanderHalfAngleDeg,
            ExpanderEndInnerRadiusMm = rExpEnd,
            ExitTargetInnerRadiusMm = rExitTgt,
            ExpanderExitToTargetRadiusMismatchRatio = mismatch,
            StatorToInjectorYawMismatchDeg = yawMis,
            SolvedEntrainmentRatio = er
        };
    }
}
