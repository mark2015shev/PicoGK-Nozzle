using System.Collections.Generic;

namespace PicoGK_Run.Physics;

/// <summary>Quasi-steady dual-path discharge split at one chamber bulk state (SI units).</summary>
public sealed class SwirlChamberDualPathDischargeResult
{
    public double MdotPrimaryKgS { get; init; }
    public double MdotSecondaryKgS { get; init; }
    public double MdotTotalInKgS { get; init; }

    /// <summary>Orifice-style demand at bulk P: ṁ = C_d A √(2 ρ max(0, P_ch − P_ref)).</summary>
    public double MdotUpstreamRawKgS { get; init; }
    public double MdotDownstreamRawKgS { get; init; }

    /// <summary>Mass-balanced split preserving raw ratio: scales so ṁ_up + ṁ_down = ṁ_total,in.</summary>
    public double MdotUpstreamBalancedKgS { get; init; }
    public double MdotDownstreamBalancedKgS { get; init; }

    public double FractionUpstream { get; init; }
    public double FractionDownstream { get; init; }

    public double ChamberBulkPressurePa { get; init; }
    public double ChamberBulkDensityKgM3 { get; init; }
    public double DeltaPUpstreamPa { get; init; }
    public double DeltaPDownstreamPa { get; init; }

    public double PUpstreamReferencePa { get; init; }
    public double PDownstreamReferencePa { get; init; }
    public double EffectiveUpstreamEscapeAreaM2 { get; init; }
    public double EffectiveDownstreamEscapeAreaM2 { get; init; }

    /// <summary>ṁ_in − (ṁ_up,raw + ṁ_down,raw): positive ⇒ bulk P would need to rise for raw orifices to clear mass at this state.</summary>
    public double QuasiSteadyOrificeResidualKgS { get; init; }

    /// <summary>Continuity-consistent axial speed from march [m/s].</summary>
    public double VAxialContinuityMps { get; init; }

    /// <summary>|V_a| weighted by discharge preference (does not replace continuity closure).</summary>
    public double VAxialDischargeWeightedMps { get; init; }

    public double VTangentialMps { get; init; }

    /// <summary>UPSTREAM-DOMINANT, DOWNSTREAM-DOMINANT, or SPLIT.</summary>
    public string DirectionalClassification { get; init; } = "SPLIT";

    public IReadOnlyList<string> FormatReportLines()
    {
        double pctUp = 100.0 * FractionUpstream;
        double pctDown = 100.0 * FractionDownstream;
        string narrative =
            $"The primary exhaust enters tangentially, but chamber bulk pressure vs path references biases about {pctDown:F1}% of the mixed flow toward the expander/exit and {pctUp:F1}% toward the inlet lip (quasi-steady dual-path model; validate in CFD).";
        return (IReadOnlyList<string>)new[]
        {
            "SWIRL CHAMBER FLOW DIRECTION SPLIT",
            $"  mdot_primary [kg/s]:              {MdotPrimaryKgS:F6}",
            $"  mdot_secondary [kg/s]:            {MdotSecondaryKgS:F6}",
            $"  mdot_total_in [kg/s]:             {MdotTotalInKgS:F6}",
            $"  mdot_up (balanced) [kg/s]:        {MdotUpstreamBalancedKgS:F6}",
            $"  mdot_down (balanced) [kg/s]:      {MdotDownstreamBalancedKgS:F6}",
            $"  mdot_up (raw orifice) [kg/s]:     {MdotUpstreamRawKgS:F6}",
            $"  mdot_down (raw orifice) [kg/s]:   {MdotDownstreamRawKgS:F6}",
            $"  f_up [-]:                         {FractionUpstream:F4}",
            $"  f_down [-]:                       {FractionDownstream:F4}",
            $"  chamber P_bulk [Pa]:              {ChamberBulkPressurePa:F1}",
            $"  chamber rho [kg/m3]:              {ChamberBulkDensityKgM3:F4}",
            $"  P_upstream_reference [Pa]:        {PUpstreamReferencePa:F1}",
            $"  P_downstream_reference [Pa]:      {PDownstreamReferencePa:F1}",
            $"  ΔP_up [Pa]:                       {DeltaPUpstreamPa:F1}",
            $"  ΔP_down [Pa]:                     {DeltaPDownstreamPa:F1}",
            $"  A_up_effective [m2]:              {EffectiveUpstreamEscapeAreaM2:E4}",
            $"  A_down_effective [m2]:            {EffectiveDownstreamEscapeAreaM2:E4}",
            $"  quasi-steady orifice residual [kg/s]: {QuasiSteadyOrificeResidualKgS:F6}  (ṁ_in − ṁ_up,raw − ṁ_down,raw)",
            $"  V_a continuity [m/s]:              {VAxialContinuityMps:F3}",
            $"  V_a discharge-weighted [m/s]:     {VAxialDischargeWeightedMps:F3}",
            $"  V_tangential [m/s]:               {VTangentialMps:F3}",
            $"  directional result:               {DirectionalClassification}",
            narrative
        };
    }
}
