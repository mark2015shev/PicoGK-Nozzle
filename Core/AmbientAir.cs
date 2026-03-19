namespace PicoGK_Run.Core;

public sealed class AmbientAir
{
    public double PressurePa { get; }
    public double TemperatureK { get; }
    public double DensityKgPerM3 { get; }

    public AmbientAir(double pressurePa, double temperatureK, double densityKgPerM3)
    {
        PressurePa = pressurePa;
        TemperatureK = temperatureK;
        DensityKgPerM3 = densityKgPerM3;
    }
}

