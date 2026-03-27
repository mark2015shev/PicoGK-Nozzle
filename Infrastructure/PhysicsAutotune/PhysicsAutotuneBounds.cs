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
}
