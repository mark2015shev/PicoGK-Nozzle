using PicoGK;
using PicoGK_Run.Core;
using PicoGK_Run.Geometry;
using PicoGK_Run.Physics;

namespace PicoGK_Run;

internal static class Program
{
    private static void Main(string[] args)
    {
        Library.Go(0.3f, Run);
    }

    private static void Run()
    {
        // 1) Inputs
        JetStreamK320 jet = new(
            outletDiameterMM: 55.0,
            massFlowKgPerSec: 0.80,
            exhaustVelocityMps: 520.0,
            pressureRatio: 2.8,
            exhaustTemperatureK: 950.0);

        AmbientAir ambient = new()
        {
            PressurePa = 101_325.0,
            TemperatureK = 288.15,
            DensityKgPerM3 = 1.225
        };

        NozzleParameters p = new()
        {
            InjectorCount = 16,
            InjectorWidthMM = 7.0,
            InjectorHeightMM = 6.0,
            InjectorAngleDeg = 90.0,
            InjectorTiltDeg = 0.0,
            SwirlChamberDiameterMM = 60.0,
            SwirlChamberLengthMM = 50.0,
            MixerLengthMM = 25.0,
            ExitDiameterMM = 80.0,
            WallThicknessMM = 3.0
        };

        // Pipeline
        AppPipeline.Run(jet, ambient, p);
    }
}
