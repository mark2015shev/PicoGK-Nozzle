using System.Collections.Generic;

namespace PicoGK_Run.Physics.Continuous;

/// <summary>Bookkeeping of where tangential kinetic power went (approximate, reduced-order).</summary>
public sealed class SwirlEnergyAccountingBuckets
{
    /// <summary>0.5·ṁ·V_t² remaining at path exit [W].</summary>
    public double PreservedSwirlPowerW { get; init; }

    /// <summary>Modeled power booked to axial kinetic gain (stator + diffuser axial coupling) [W].</summary>
    public double ToAxialPowerW { get; init; }

    /// <summary>Power proxy from expander axial wall force × reference axial speed [W].</summary>
    public double ToWallThrustPowerW { get; init; }

    public double MixingDissipationPowerW { get; init; }
    public double FrictionDissipationPowerW { get; init; }
    public double StatorRecoveryPowerW { get; init; }

    public IReadOnlyList<string> FormatSummaryLines()
    {
        return new[]
        {
            "--- Swirl / energy bucket summary (continuous path, approximate) ---",
            $"  Preserved swirl @ exit     {PreservedSwirlPowerW:F2} W  (0.5·ṁ·V_t²)",
            $"  Booked → axial            {ToAxialPowerW:F2} W",
            $"  Booked → wall thrust proxy {ToWallThrustPowerW:F2} W",
            $"  Mixing dissipation        {MixingDissipationPowerW:F2} W",
            $"  Friction dissipation      {FrictionDissipationPowerW:F2} W",
            $"  Stator recovery (model)   {StatorRecoveryPowerW:F2} W"
        };
    }
}
