using PicoGK_Run.Core;
using PicoGK_Run.Geometry;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Tests;

public class GeometryAssemblyConsistencyTests
{
    [Fact]
    public void SwirlPlacement_ChamberLongerThanAnchor_HasZeroUpstreamOvershoot()
    {
        NozzleDesignInputs b = K320Baseline.CreateDesign();
        var d = new NozzleDesignInputs
        {
            InletDiameterMm = b.InletDiameterMm,
            SwirlChamberDiameterMm = b.SwirlChamberDiameterMm,
            SwirlChamberLengthMm = 81.5,
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
            SwirlChamberLengthDownstreamAnchorMm = 80.0,
            ShowInViewer = false,
            UsePhysicsInformedGeometry = false,
            UseAutotune = false,
            EnablePipelineProfiling = false,
            ApplyHardSiThrustAndPressureAssertions = false,
            SiVerbosityLevel = SiVerbosityLevel.Low
        };

        InletSegmentStations inlet = InletSegmentStations.Compute(d);
        SwirlChamberPlacement sp = SwirlChamberPlacement.Compute(d, inlet.XAfterInlet, run);

        Assert.Equal(0.0, sp.ChamberUpstreamOvershootMm, 9);
        Assert.Equal(SwirlChamberPlacementHealth.Pass, sp.PlacementHealth);
        Assert.Equal(81.5, sp.PhysicalChamberLengthBuiltMm, 9);
        double span = sp.MainChamberEndXMm - sp.InletChamberJunctionXMm;
        Assert.True(span >= 80.0 - 1e-6);
    }

    [Fact]
    public void GeometryPathBuildConsistency_K320_AllPathChecksPass()
    {
        NozzleDesignInputs d = K320Baseline.CreateDesign();
        RunConfiguration run = K320Baseline.CreateValidationRun();
        GeometryAssemblyPath p = GeometryAssemblyPath.Compute(d, run);

        IReadOnlyList<GeometryPathBuildCheckItem> items = GeometryPathBuildConsistencyValidator.Validate(p, null);
        Assert.All(items, c => Assert.True(c.Passed, c.Message));
    }

    [Fact]
    public void NozzleGeometryBuilder_TotalLengthMatchesPathEnd()
    {
        NozzleDesignInputs d = K320Baseline.CreateDesign();
        RunConfiguration run = new()
        {
            VoxelSizeMM = 0.3f,
            ShowInViewer = false,
            UsePhysicsInformedGeometry = false,
            UseAutotune = false,
            EnablePipelineProfiling = false,
            ApplyHardSiThrustAndPressureAssertions = true,
            SiVerbosityLevel = SiVerbosityLevel.Low,
            ValidateMarchStepInvariants = false
        };
        GeometryAssemblyPath path = GeometryAssemblyPath.Compute(d, run);

        var builder = new NozzleGeometryBuilder();
        NozzleGeometryResult geo = builder.Build(
            d,
            new NozzleSolvedState(),
            run);

        Assert.Equal(path.XAfterExit, geo.TotalLengthMm, 9);
        IReadOnlyList<GeometryPathBuildCheckItem> items = GeometryPathBuildConsistencyValidator.Validate(path, geo);
        Assert.All(items, c => Assert.True(c.Passed, c.Message));
    }

    [Fact]
    public void DownstreamRadialContinuity_K320_WithinDiameterTolerance()
    {
        NozzleDesignInputs d = K320Baseline.CreateDesign();
        GeometryAssemblyPath p = GeometryAssemblyPath.Compute(d, K320Baseline.CreateValidationRun());
        double tol = GeometryConsistencyTolerances.DiameterToleranceMm;
        Assert.InRange(Math.Abs(p.ExpanderEndInnerRadiusMm - p.RecoveryAnnulusInnerRadiusMm), 0.0, tol);
        Assert.InRange(Math.Abs(p.ExitInnerRadiusStartMm - p.RecoveryAnnulusInnerRadiusMm), 0.0, tol);
    }
}
