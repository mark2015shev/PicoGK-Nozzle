using PicoGK;
using PicoGK_Run.Core;
using PicoGK_Run.Geometry;
using PicoGK_Run.Infrastructure.Pipeline;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Infrastructure.Services;

/// <summary>
/// Authoritative design normalization before SI solve: synthesis vs derived-bore reference vs template, injector ratio clamp.
/// </summary>
internal static class DesignPreparationService
{
    public static PreparedNozzleDesignHandoff PrepareActiveDesignForSolve(
        SourceInputs source,
        NozzleDesignInputs seedDesign,
        RunConfiguration run)
    {
        NozzleDesignInputs activeDesign;
        SwirlChamberSizingModel.SizingDiagnostics? chamberSizing;
        if (run.UsePhysicsInformedGeometry)
        {
            NozzleGeometrySynthesis.GeometrySynthesisResult syn = NozzleGeometrySynthesis.SynthesizeWithDiagnostics(
                source,
                seedDesign,
                run.GeometrySynthesisTargetEntrainmentRatio,
                run);
            activeDesign = syn.Design;
            chamberSizing = syn.ChamberSizing;
            if (syn.VortexEntrainmentHints is { Count: > 0 } vh)
            {
                foreach (string line in vh)
                    Library.Log(line);
            }
        }
        else if (run.UseDerivedSwirlChamberDiameter)
        {
            activeDesign = seedDesign;
            chamberSizing = SwirlChamberSizingModel.ComputeDerived(
                source,
                seedDesign,
                run.GeometrySynthesisTargetEntrainmentRatio,
                run,
                SwirlChamberSizingModel.DiameterMode.ReferenceDerivedAtConfiguredTargetEr);
        }
        else
        {
            activeDesign = seedDesign;
            chamberSizing = SwirlChamberSizingModel.ForUserTemplate(seedDesign, source, run);
        }

        double injClamped = SwirlChamberPlacement.ClampInjectorAxialRatio(activeDesign.InjectorAxialPositionRatio, run);
        if (Math.Abs(activeDesign.InjectorAxialPositionRatio - injClamped) > 1e-12)
            activeDesign = activeDesign.WithInjectorAxialPositionRatio(injClamped);

        return new PreparedNozzleDesignHandoff(seedDesign, activeDesign, chamberSizing);
    }
}
