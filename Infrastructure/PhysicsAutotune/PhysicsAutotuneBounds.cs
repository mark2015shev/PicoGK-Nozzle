namespace PicoGK_Run.Infrastructure.PhysicsAutotune;

/// <summary>Conservative absolute bounds [mm] / [deg] for the first physics autotune pass.</summary>
public static class PhysicsAutotuneBounds
{
    public const double SwirlChamberDiameterMinMm = 110.0;
    public const double SwirlChamberDiameterMaxMm = 220.0;

    public const double SwirlChamberLengthMinMm = 60.0;
    public const double SwirlChamberLengthMaxMm = 220.0;

    public const double InletCaptureDiameterMinMm = 130.0;
    public const double InletCaptureDiameterMaxMm = 240.0;

    public const double ExpanderHalfAngleMinDeg = 2.0;
    public const double ExpanderHalfAngleMaxDeg = 7.0;

    public const double StatorVaneAngleMinDeg = 15.0;
    public const double StatorVaneAngleMaxDeg = 55.0;

    public const double ExpanderLengthMinMm = 40.0;
    public const double ExpanderLengthMaxMm = 200.0;

    public const double ExitDiameterMinMm = 70.0;
    public const double ExitDiameterMaxMm = 170.0;

    public const double InjectorAxialPositionRatioMin = 0.35;
    public const double InjectorAxialPositionRatioMax = 0.92;

    public const double StatorHubDiameterMinMm = 12.0;
    public const double StatorHubDiameterMaxMm = 52.0;

    public const double StatorAxialLengthMinMm = 12.0;
    public const double StatorAxialLengthMaxMm = 48.0;

    public const double StatorChordMinMm = 3.5;
    public const double StatorChordMaxMm = 22.0;

    public const int StatorVaneCountMin = 6;
    public const int StatorVaneCountMax = 24;
}
