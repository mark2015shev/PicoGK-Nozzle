using PicoGK_Run.Core;
using PicoGK_Run.Geometry;
using PicoGK_Run.Infrastructure;
using PicoGK_Run.Infrastructure.Pipeline;
using PicoGK_Run.Infrastructure.Services;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Tests;

public class ArchitecturePipelineTests
{
    [Fact]
    public void EvaluateCandidateUnified_SameInputsTwice_CoreMetricsMatch()
    {
        SourceInputs source = K320Baseline.CreateSource();
        NozzleDesignInputs design = K320Baseline.CreateDesign();
        RunConfiguration run = K320Baseline.CreateValidationRun();

        UnifiedEvaluationResult a = NozzleFlowCompositionRoot.EvaluateCandidateUnified(
            source, design, run, NozzleEvaluationMode.TuningFast);
        UnifiedEvaluationResult b = NozzleFlowCompositionRoot.EvaluateCandidateUnified(
            source, design, run, NozzleEvaluationMode.TuningFast);

        Assert.Equal(a.Solved.EntrainmentRatio, b.Solved.EntrainmentRatio);
        Assert.Equal(a.SiDiagnostics.NetThrustN, b.SiDiagnostics.NetThrustN);
        Assert.Equal(a.Solved.MixedMassFlowKgPerSec, b.Solved.MixedMassFlowKgPerSec);
        Assert.Equal(a.DrivenDesign.SwirlChamberDiameterMm, b.DrivenDesign.SwirlChamberDiameterMm);
    }

    [Fact]
    public void EvaluateCandidateUnified_TuningAndFinalMatch_WhenContinuityFlagsAligned()
    {
        SourceInputs source = K320Baseline.CreateSource();
        NozzleDesignInputs design = K320Baseline.CreateDesign();
        RunConfiguration run = new()
        {
            VoxelSizeMM = 0.3f,
            ShowInViewer = false,
            UsePhysicsInformedGeometry = false,
            UseAutotune = false,
            EnablePipelineProfiling = false,
            ApplyHardSiThrustAndPressureAssertions = true,
            SiVerbosityLevel = SiVerbosityLevel.Low,
            ValidateMarchStepInvariants = false,
            RunGeometryContinuityCheck = true,
            EvaluateGeometryContinuityDuringAutotune = true
        };

        UnifiedEvaluationResult tuning = NozzleFlowCompositionRoot.EvaluateCandidateUnified(
            source, design, run, NozzleEvaluationMode.TuningFast);
        UnifiedEvaluationResult final = NozzleFlowCompositionRoot.EvaluateCandidateUnified(
            source, design, run, NozzleEvaluationMode.FinalDetailed);

        Assert.Equal(tuning.Solved.EntrainmentRatio, final.Solved.EntrainmentRatio);
        Assert.Equal(tuning.SiDiagnostics.NetThrustN, final.SiDiagnostics.NetThrustN);
        Assert.Equal(tuning.Solved.ExitVelocityMps, final.Solved.ExitVelocityMps);
        Assert.NotNull(final.GeometryContinuity);
        Assert.NotNull(tuning.GeometryContinuity);
    }

    [Fact]
    public void AutotuneFinalization_WhenFinalizeDisabled_ReturnsSameSeedReference()
    {
        SourceInputs source = K320Baseline.CreateSource();
        NozzleDesignInputs seed = K320Baseline.CreateDesign();
        var run = new RunConfiguration
        {
            ShowInViewer = false,
            UsePhysicsInformedGeometry = false,
            UseAutotune = false,
            UseDerivedSwirlChamberDiameter = true,
            AutotuneFinalizeApplyEntrainmentDerivedChamberBore = false,
            EnablePipelineProfiling = false,
            ApplyHardSiThrustAndPressureAssertions = false,
            SiVerbosityLevel = SiVerbosityLevel.Low
        };

        AutotuneFinalizationService.PostSearchSeedResult r =
            AutotuneFinalizationService.ApplyOptionalEntrainmentDerivedChamberBoreAfterSearch(source, seed, run);

        Assert.Same(seed, r.SeedForFinal);
        Assert.False(r.FinalPassAppliedEntrainmentDerivedChamberBore);
    }

    [Fact]
    public void PipelineReporting_PrintSiFlowSummary_DoesNotMutateDiagnostics()
    {
        SourceInputs source = K320Baseline.CreateSource();
        NozzleDesignInputs design = K320Baseline.CreateDesign();
        RunConfiguration run = K320Baseline.CreateValidationRun();

        UnifiedEvaluationResult u = NozzleFlowCompositionRoot.EvaluateCandidateUnified(
            source, design, run, NozzleEvaluationMode.TuningFast);

        double thrustBefore = u.SiDiagnostics.NetThrustN;
        double machBefore = u.SiDiagnostics.MaxInletMach;

        PipelineReportingService.PrintSiFlowSummary(
            u.InletState,
            u.DesignResult,
            u.SiDiagnostics,
            SiVerbosityLevel.Low);

        Assert.Equal(thrustBefore, u.SiDiagnostics.NetThrustN);
        Assert.Equal(machBefore, u.SiDiagnostics.MaxInletMach);
    }

    [Fact]
    public void GeometryConsistencyService_StructuredChecksIncludePhysicalReject_ForTinyInlet()
    {
        NozzleDesignInputs b = K320Baseline.CreateDesign();
        var d = new NozzleDesignInputs
        {
            InletDiameterMm = 0.2,
            SwirlChamberDiameterMm = b.SwirlChamberDiameterMm,
            SwirlChamberLengthMm = b.SwirlChamberLengthMm,
            InjectorAxialPositionRatio = b.InjectorAxialPositionRatio,
            InjectorUpstreamGuardLengthMm = b.InjectorUpstreamGuardLengthMm,
            TotalInjectorAreaMm2 = b.TotalInjectorAreaMm2,
            InjectorCount = b.InjectorCount,
            InjectorWidthMm = b.InjectorWidthMm,
            InjectorHeightMm = b.InjectorHeightMm,
            InjectorYawAngleDeg = b.InjectorYawAngleDeg,
            InjectorPitchAngleDeg = b.InjectorPitchAngleDeg,
            InjectorRollAngleDeg = b.InjectorRollAngleDeg,
            ExpanderLengthMm = b.ExpanderLengthMm,
            ExpanderHalfAngleDeg = b.ExpanderHalfAngleDeg,
            ExitDiameterMm = b.ExitDiameterMm,
            StatorVaneAngleDeg = b.StatorVaneAngleDeg,
            StatorVaneCount = b.StatorVaneCount,
            StatorHubDiameterMm = b.StatorHubDiameterMm,
            StatorAxialLengthMm = b.StatorAxialLengthMm,
            StatorBladeChordMm = b.StatorBladeChordMm,
            WallThicknessMm = b.WallThicknessMm
        };

        var run = new RunConfiguration
        {
            ShowInViewer = false,
            UsePhysicsInformedGeometry = false,
            UseAutotune = false,
            EnablePipelineProfiling = false,
            ApplyHardSiThrustAndPressureAssertions = false,
            SiVerbosityLevel = SiVerbosityLevel.Low
        };

        GeometryContinuityReport? rep = GeometryConsistencyService.EvaluateDrivenDesign(d, true, run);
        Assert.NotNull(rep);
        Assert.False(rep!.IsAcceptable);
        Assert.Contains(
            rep.Checks,
            c => c is { Kind: GeometryConsistencyCheckKind.DiameterPhysical, Severity: GeometryConsistencySeverity.Reject });
    }
}
