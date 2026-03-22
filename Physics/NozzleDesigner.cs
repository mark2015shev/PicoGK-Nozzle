using System;
using System.Collections.Generic;

namespace PicoGK_Run.Physics;

/// <summary>
/// Maps solved <see cref="JetState"/> history to <see cref="NozzleDesignResult"/>.
/// </summary>
public sealed class NozzleDesigner
{
    private readonly ThrustCalculator _thrust = new();

    public NozzleDesignResult CreateDesignResult(
        JetState inletState,
        IReadOnlyList<JetState> flowStates,
        double outletAreaM2,
        double ambientPressurePa,
        double freestreamVelocityMps,
        SiFlowDiagnostics? siDiagnostics = null)
    {
        if (flowStates.Count == 0)
            throw new ArgumentException("flowStates must contain at least one state.", nameof(flowStates));

        JetState outlet = flowStates[^1];
        double mixingLengthM = outlet.AxialPositionM - flowStates[0].AxialPositionM;

        double thrustN = siDiagnostics != null
            ? siDiagnostics.NetThrustN
            : _thrust.ComputeThrustN(
                outlet.TotalMassFlowKgS,
                outlet.VelocityMps,
                freestreamVelocityMps,
                outlet.PressurePa,
                ambientPressurePa,
                outlet.AreaM2);

        double aIn = Math.Max(inletState.AreaM2, 1e-12);
        double aOut = Math.Max(outletAreaM2, 1e-12);

        return new NozzleDesignResult
        {
            InletAreaM2 = aIn,
            OutletAreaM2 = aOut,
            SuggestedMixingLengthM = Math.Max(mixingLengthM, 0.0),
            SuggestedInletRadiusM = Math.Sqrt(aIn / Math.PI),
            SuggestedOutletRadiusM = Math.Sqrt(aOut / Math.PI),
            EstimatedExitVelocityMps = outlet.VelocityMps,
            EstimatedTotalMassFlowKgS = outlet.TotalMassFlowKgS,
            EstimatedThrustN = thrustN
        };
    }
}
