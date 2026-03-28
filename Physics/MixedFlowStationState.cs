using System;

namespace PicoGK_Run.Physics;

/// <summary>
/// Authoritative mixed-stream scalars at one axial station (SI). Prefer this over re-deriving from parallel types.
/// </summary>
public readonly record struct MixedFlowStationState(
    double MdotPrimaryKgS,
    double MdotSecondaryKgS,
    double MdotTotalKgS,
    double DensityKgM3,
    double PStaticPa,
    double TStaticK,
    double PTotalPa,
    double TTotalK,
    double VAxialMps,
    double VTangentialMps,
    double MachNumber,
    string StationTag)
{
    public static MixedFlowStationState FromFlowStepState(FlowStepState s, string stationTag = "")
    {
        double mp = Math.Max(s.MdotPrimaryKgS, 0.0);
        double ms = Math.Max(s.MdotSecondaryKgS, 0.0);
        double mt = Math.Max(s.MdotTotalKgS, mp + ms);
        return new MixedFlowStationState(
            mp,
            ms,
            mt,
            Math.Max(s.DensityKgM3, 1e-18),
            s.PStaticPa,
            Math.Max(s.TStaticK, 1.0),
            s.PTotalPa,
            Math.Max(s.TTotalK, 1.0),
            s.VAxialMps,
            s.VTangentialMps,
            s.Mach,
            string.IsNullOrEmpty(stationTag) ? $"x={s.X:F4}m" : stationTag);
    }

    /// <summary>1-D jet slice: tangential speed must be supplied (0 if unknown).</summary>
    public static MixedFlowStationState FromJetState(
        JetState j,
        double tangentialVelocityMps,
        double machNumber,
        double totalPressurePa,
        double totalTemperatureK,
        string stationTag)
    {
        double mp = Math.Max(j.MassFlowKgS, 0.0);
        double ms = Math.Max(j.EntrainedMassFlowKgS, 0.0);
        double mt = Math.Max(j.TotalMassFlowKgS, mp + ms);
        return new MixedFlowStationState(
            mp,
            ms,
            mt,
            Math.Max(j.DensityKgM3, 1e-18),
            j.PressurePa,
            Math.Max(j.TemperatureK, 1.0),
            Math.Max(totalPressurePa, 1.0),
            Math.Max(totalTemperatureK, 1.0),
            j.VelocityMps,
            tangentialVelocityMps,
            machNumber,
            stationTag);
    }
}
