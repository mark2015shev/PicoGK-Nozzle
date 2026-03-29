using System;
using PicoGK;
using PicoGK_Run.Core;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Infrastructure.Services;

/// <summary>
/// Post-search adjustments before the final pipeline pass (not part of AppPipeline orchestration).
/// </summary>
internal static class AutotuneFinalizationService
{
    public readonly record struct PostSearchSeedResult(
        NozzleDesignInputs SeedForFinal,
        bool FinalPassAppliedEntrainmentDerivedChamberBore);

    public static PostSearchSeedResult ApplyOptionalEntrainmentDerivedChamberBoreAfterSearch(
        SourceInputs source,
        NozzleDesignInputs bestSeedFromSearch,
        RunConfiguration run)
    {
        bool skipDerivedBoreFinalize = run.AutotuneStrategy == AutotuneStrategy.PhysicsControlledFiveParameter
            || run.PhysicsAutotunePreserveWinningChamberDiameter;

        if (!run.UseDerivedSwirlChamberDiameter
            || !run.AutotuneFinalizeApplyEntrainmentDerivedChamberBore
            || skipDerivedBoreFinalize)
        {
            return new PostSearchSeedResult(bestSeedFromSearch, false);
        }

        double dBefore = bestSeedFromSearch.SwirlChamberDiameterMm;
        SwirlChamberSizingModel.SizingDiagnostics dz = SwirlChamberSizingModel.ComputeDerived(
            source,
            bestSeedFromSearch,
            run.GeometrySynthesisTargetEntrainmentRatio,
            run);

        NozzleDesignInputs seed = WithSwirlChamberDiameterMm(bestSeedFromSearch, dz.ChamberDiameterTargetMm);
        if (Math.Abs(dBefore - dz.ChamberDiameterTargetMm) > 0.2)
        {
            Library.Log(
                $"Autotune finalize: chamber bore {dBefore:F2} mm → entrainment-derived {dz.ChamberDiameterTargetMm:F2} mm at GeometrySynthesisTargetEntrainmentRatio={run.GeometrySynthesisTargetEntrainmentRatio:F3} (continuity model; not CFD).");
            foreach (string line in dz.Warnings)
                Library.Log("  Chamber finalize: " + line);
        }

        return new PostSearchSeedResult(seed, true);
    }

    public static ChamberDiameterAudit? MergeAutotuneIntoChamberAudit(
        ChamberDiameterAudit? audit,
        bool searchUsedDerivedBore,
        bool finalPassAppliedDerivedBore,
        bool searchAllowedDirectChamberOverride)
    {
        if (audit == null)
            return null;
        string? fn = audit.Footnote;
        if (finalPassAppliedDerivedBore)
        {
            const string extra =
                "Autotune: winning-seed bore was set from entrainment-derived model at GeometrySynthesisTargetEntrainmentRatio before the final SI+voxel pass (first-order; not CFD).";
            fn = string.IsNullOrEmpty(fn) ? extra : fn + " " + extra;
        }

        return new ChamberDiameterAudit
        {
            InputDesignMm = audit.InputDesignMm,
            AfterSynthesisMm = audit.AfterSynthesisMm,
            PreSiSolveMm = audit.PreSiSolveMm,
            PostFlowDrivenMm = audit.PostFlowDrivenMm,
            UsedForVoxelBuildMm = audit.UsedForVoxelBuildMm,
            DeclaredPrimarySource = audit.DeclaredPrimarySource,
            AutotuneSearchUsedDerivedBoreSizing = searchUsedDerivedBore,
            AutotuneAppliedDirectChamberScale = searchAllowedDirectChamberOverride,
            ReferenceDerivedBoreAtConfiguredTargetErMm = audit.ReferenceDerivedBoreAtConfiguredTargetErMm,
            Footnote = fn
        };
    }

    private static NozzleDesignInputs WithSwirlChamberDiameterMm(NozzleDesignInputs d, double swirlChamberDiameterMm) => new()
    {
        InletDiameterMm = d.InletDiameterMm,
        SwirlChamberDiameterMm = swirlChamberDiameterMm,
        SwirlChamberLengthMm = d.SwirlChamberLengthMm,
        InjectorAxialPositionRatio = d.InjectorAxialPositionRatio,
        InjectorUpstreamGuardLengthMm = d.InjectorUpstreamGuardLengthMm,
        TotalInjectorAreaMm2 = d.TotalInjectorAreaMm2,
        InjectorCount = d.InjectorCount,
        InjectorWidthMm = d.InjectorWidthMm,
        InjectorHeightMm = d.InjectorHeightMm,
        InjectorYawAngleDeg = d.InjectorYawAngleDeg,
        InjectorPitchAngleDeg = d.InjectorPitchAngleDeg,
        InjectorRollAngleDeg = d.InjectorRollAngleDeg,
        ExpanderLengthMm = d.ExpanderLengthMm,
        ExpanderHalfAngleDeg = d.ExpanderHalfAngleDeg,
        ExitDiameterMm = d.ExitDiameterMm,
        StatorVaneAngleDeg = d.StatorVaneAngleDeg,
        StatorVaneCount = d.StatorVaneCount,
        StatorHubDiameterMm = d.StatorHubDiameterMm,
        StatorAxialLengthMm = d.StatorAxialLengthMm,
        StatorBladeChordMm = d.StatorBladeChordMm,
        WallThicknessMm = d.WallThicknessMm
    };
}
