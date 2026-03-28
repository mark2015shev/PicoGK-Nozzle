using PicoGK_Run.Core;
using PicoGK_Run.Infrastructure;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;
using PicoGK_Run.Physics.Reports;

namespace PicoGK_Run.Tests;

public class SiNozzleRegressionTests
{
    [Fact]
    public void K320_Baseline_Has_Finite_Flow_And_Thrust_Control_Volume()
    {
        SiPathValidationPack p = NozzleFlowCompositionRoot.EvaluateSiPathForValidation(
            K320Baseline.CreateSource(),
            K320Baseline.CreateDesign(),
            K320Baseline.CreateValidationRun());

        Assert.False(double.IsNaN(p.Solved.MixedMassFlowKgPerSec));
        Assert.False(double.IsInfinity(p.Solved.MixedMassFlowKgPerSec));
        Assert.InRange(p.Solved.MixedMassFlowKgPerSec, 0.35, 2.5);

        double machBulk = p.SiDiag.MarchPhysicsClosure?.FinalMachBulk ?? double.NaN;
        Assert.False(double.IsNaN(machBulk));
        Assert.InRange(machBulk, 0.0, 1.2);

        Assert.False(double.IsNaN(p.Solved.ExitVelocityMps));
        // Exit plane axial speed after stator/diffuser bookkeeping (first-order SI; not raw source jet speed).
        Assert.InRange(p.Solved.ExitVelocityMps, 5.0, 950.0);

        Assert.True(p.SiDiag.ThrustControlVolumeIsValid, p.SiDiag.ThrustControlVolumeInvalidReason);

        SwirlEntranceCapacityDualResult? cap = p.SiDiag.ChamberMarch?.SwirlEntranceCapacityStations;
        Assert.NotNull(cap);
        Assert.NotEqual(SwirlEntranceCapacityClassification.FailChoking, cap!.CombinedClassification);

        SiDiagnosticsReport rep = p.SiDiag.ToStructuredReport();
        Assert.NotNull(rep.Thrust);
        Assert.Contains("netThrustN", rep.ToJson(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Legacy_PressureRatio_Does_Not_Affect_Live_Consistency_Or_Derived_Physics()
    {
        SourceInputs ok = K320Baseline.CreateSource();
        var withAbsurdPr = new SourceInputs(
            ok.SourceOutletAreaMm2,
            ok.MassFlowKgPerSec,
            ok.SourceVelocityMps,
            ok.AmbientPressurePa,
            ok.AmbientTemperatureK,
            ok.AmbientDensityKgPerM3,
            ok.ExhaustTemperatureK,
            ok.ExhaustTemperatureIsTotalK,
            legacyPressureRatio: 50.0);

        SiPathValidationPack p = NozzleFlowCompositionRoot.EvaluateSiPathForValidation(
            withAbsurdPr,
            K320Baseline.CreateDesign(),
            K320Baseline.CreateValidationRun());

        Assert.NotNull(p.SiDiag.SourceDischargeConsistency);
        SourceDischargeConsistencyReport r = p.SiDiag.SourceDischargeConsistency!;
        Assert.True(r.DerivedStatePhysicsPass);
        Assert.True(r.OverallPass == (r.DerivedStatePhysicsPass && r.ChokingConsistencyPass));
        Assert.Contains(
            r.FailureAndWarningMessages,
            m => m.Contains("Deprecated legacy field PressureRatio", StringComparison.Ordinal));
        Assert.Equal(SourceLiveThermodynamicsMode.DerivedDischargeStateOnly, r.SelectedMode);
    }

    [Fact]
    public void Very_Small_Swirl_Chamber_Triggers_Capacity_Fail()
    {
        NozzleDesignInputs d = K320Baseline.CreateDesignWithSwirlChamberDiameterMm(30.0);

        var run = new RunConfiguration
        {
            ShowInViewer = false,
            UsePhysicsInformedGeometry = false,
            UseAutotune = false,
            EnablePipelineProfiling = false,
            ApplyHardSiThrustAndPressureAssertions = false,
            SiVerbosityLevel = SiVerbosityLevel.Low
        };

        SiPathValidationPack p = NozzleFlowCompositionRoot.EvaluateSiPathForValidation(
            K320Baseline.CreateSource(),
            d,
            run);

        SwirlEntranceCapacityClassification cc =
            p.SiDiag.ChamberMarch!.SwirlEntranceCapacityStations!.CombinedClassification;

        Assert.True(
            cc is SwirlEntranceCapacityClassification.FailChoking
                or SwirlEntranceCapacityClassification.FailRestrictive,
            $"Expected restrictive/choking capacity classification at 30 mm bore; got {cc}.");
    }
}
