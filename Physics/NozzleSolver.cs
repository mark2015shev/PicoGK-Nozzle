using System;
using PicoGK_Run.Core;

namespace PicoGK_Run.Physics;

public static class NozzleSolver
{
    public static NozzleSolvedState Solve(JetStreamK320 jet, AmbientAir ambient, NozzleParameters p)
    {
        if (p.InjectorCount <= 0) throw new ArgumentOutOfRangeException(nameof(p.InjectorCount));
        if (p.InjectorWidthMM <= 0) throw new ArgumentOutOfRangeException(nameof(p.InjectorWidthMM));
        if (p.InjectorHeightMM <= 0) throw new ArgumentOutOfRangeException(nameof(p.InjectorHeightMM));
        if (p.SwirlChamberDiameterMM <= 0) throw new ArgumentOutOfRangeException(nameof(p.SwirlChamberDiameterMM));
        if (p.ExitDiameterMM <= 0) throw new ArgumentOutOfRangeException(nameof(p.ExitDiameterMM));
        if (ambient.DensityKgPerM3 <= 0) throw new ArgumentOutOfRangeException(nameof(ambient.DensityKgPerM3));

        double totalInjectorAreaMM2 = p.InjectorCount * p.InjectorWidthMM * p.InjectorHeightMM;
        double chamberAreaMM2 = FlowMath.CircleAreaMM2FromDiameterMM(p.SwirlChamberDiameterMM);
        double exitAreaMM2 = FlowMath.CircleAreaMM2FromDiameterMM(p.ExitDiameterMM);

        double chamberAreaM2 = chamberAreaMM2 * 1e-6;
        double exitAreaM2 = exitAreaMM2 * 1e-6;
        double inletAreaM2 = FlowMath.CircleAreaM2FromDiameterMM(jet.OutletDiameterMM);

        double coreV = FlowMath.VelocityMps(jet.MassFlowKgPerSec, ambient.DensityKgPerM3, chamberAreaM2);

        double injectorAreaM2 = (totalInjectorAreaMM2 * 1e-6);
        double jetV = FlowMath.VelocityMps(jet.MassFlowKgPerSec, ambient.DensityKgPerM3, Math.Max(injectorAreaM2, 1e-9));

        double swirl = SwirlMath.EstimateSwirlStrength(p.InjectorAngleDeg, jetV, coreV);
        double loss = PressureLossMath.EstimateLoss(inletAreaM2, chamberAreaM2, exitAreaM2, swirl);

        double thrustGain = Math.Max(0.0, 0.05 * swirl - 0.02 * loss);

        return new NozzleSolvedState
        {
            TotalInjectorAreaMM2 = totalInjectorAreaMM2,
            ChamberAreaMM2 = chamberAreaMM2,
            ExitAreaMM2 = exitAreaMM2,
            CoreVelocityMps = coreV,
            SwirlStrength = swirl,
            PressureLoss = loss,
            EstimatedThrustGain = thrustGain
        };
    }
}

