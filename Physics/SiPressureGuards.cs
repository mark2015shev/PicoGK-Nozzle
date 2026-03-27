using System;

namespace PicoGK_Run.Physics;

/// <summary>Finite, bounded static pressures for the SI path (Pa internally).</summary>
public static class SiPressureGuards
{
    public const double MinStaticPressurePa = 1.0;
    public const double MaxStaticPressurePa = 5.0e7;

    public static double ClampStaticPressurePa(double pPa) =>
        Math.Clamp(
            double.IsFinite(pPa) ? pPa : MinStaticPressurePa,
            MinStaticPressurePa,
            MaxStaticPressurePa);

    public static double PaToBar(double pPa) => pPa / 100_000.0;

    public static double BarToPa(double pBar) => pBar * 100_000.0;
}
