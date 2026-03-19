namespace PicoGK_Run.Core;

/// <summary>
/// High-level nozzle design variables (geometry intent only; no physics here).
/// </summary>
public sealed class NozzleParameters
{
    public int InjectorCount { get; set; }
    public double InjectorWidthMM { get; set; }
    public double InjectorHeightMM { get; set; }
    public double InjectorAngleDeg { get; set; }
    public double InjectorTiltDeg { get; set; }
    public double SwirlChamberDiameterMM { get; set; }
    public double SwirlChamberLengthMM { get; set; }
    public double MixerLengthMM { get; set; }
    public double ExitDiameterMM { get; set; }
    public double WallThicknessMM { get; set; }
}

