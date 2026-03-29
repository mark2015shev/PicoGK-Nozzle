using System;
using PicoGK;
using PicoGK_Run.Infrastructure;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Infrastructure.Services;

/// <summary>Library + console lines for autotune outcomes (orchestration calls these; logic stays out of AppPipeline).</summary>
internal static class AutotunePipelineReporting
{
    public static void LogWallTimeMs(double elapsedMs) =>
        Library.Log(
            $"Autotune wall time: {elapsedMs:F0} ms (SI-only trials; separate from final-run PipelineProfiler session).");

    public static void LogCoarseToFineLines(string? coarseToFineLog)
    {
        if (string.IsNullOrEmpty(coarseToFineLog))
            return;
        foreach (string line in coarseToFineLog.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            Library.Log(line.TrimEnd('\r'));
    }

    public static void LogBeforeFinalRun(NozzleDesignAutotune.Result tune, NozzleDesignInputs baselineTemplate, RunConfiguration run)
    {
        Library.Log("=== Autotune enabled (SI search — pre-CFD) ===");
        Library.Log($"Autotune strategy: {run.AutotuneStrategy} (same coupled SI scoring path for all trials).");
        Library.Log($"Autotune: trial count (SI-only evals): {tune.TrialsUsed}");
        Library.Log($"Autotune: best composite score [-]:      {tune.BestScore:F4}");
        Library.Log("Autotune: winning seed geometry [mm / deg]:");
        LogDesignToLibrary(tune.BestSeedDesign);
        Library.Log($"Autotune: winning injector Yaw / Pitch [deg]: {tune.BestSeedDesign.InjectorYawAngleDeg:F2} / {tune.BestSeedDesign.InjectorPitchAngleDeg:F2}");

        if (run.AutotuneStrategy == AutotuneStrategy.PhysicsControlledFiveParameter && tune.BestGeometryGenome != null)
        {
            NozzleGeometryGenome baseG = NozzleGeometryGenome.FromDesignInputs(baselineTemplate);
            foreach (string line in NozzleGeometryGenomeDiagnostics.FormatAutotuneReport(
                         baseG,
                         tune.BestGeometryGenome,
                         run,
                         tune.PhysicsAutotuneBestDetail))
                Library.Log(line);
        }
    }

    public static void LogGeometryDeltaToConsole(NozzleDesignInputs baseline, NozzleDesignInputs tuned)
    {
        const double eps = 0.02;
        bool inlet = Math.Abs(tuned.InletDiameterMm - baseline.InletDiameterMm) > eps;
        bool chD = Math.Abs(tuned.SwirlChamberDiameterMm - baseline.SwirlChamberDiameterMm) > eps;
        bool chL = Math.Abs(tuned.SwirlChamberLengthMm - baseline.SwirlChamberLengthMm) > eps;
        bool exit = Math.Abs(tuned.ExitDiameterMm - baseline.ExitDiameterMm) > eps;
        bool exL = Math.Abs(tuned.ExpanderLengthMm - baseline.ExpanderLengthMm) > eps;
        bool exA = Math.Abs(tuned.ExpanderHalfAngleDeg - baseline.ExpanderHalfAngleDeg) > 0.05;
        bool st = Math.Abs(tuned.StatorVaneAngleDeg - baseline.StatorVaneAngleDeg) > 0.05;
        bool ax = Math.Abs(tuned.InjectorAxialPositionRatio - baseline.InjectorAxialPositionRatio) > 0.02;
        bool yaw = Math.Abs(tuned.InjectorYawAngleDeg - baseline.InjectorYawAngleDeg) > 0.05;
        bool pitch = Math.Abs(tuned.InjectorPitchAngleDeg - baseline.InjectorPitchAngleDeg) > 0.05;
        bool any = inlet || chD || chL || exit || exL || exA || st || ax || yaw || pitch;

        ConsoleStatusWriter.WriteLine("[Autotune] Geometry delta vs hand template: " + (any ? "YES (at least one knob changed)" : "NO — winner matches template within tolerance"), StatusLevel.Normal);
        if (!any)
            ConsoleReportColor.WriteWarning(
                "[Autotune] WARNING: widen search bounds or increase trials if you expected visible geometry changes.");
    }

    private static void LogDesignToLibrary(NozzleDesignInputs d)
    {
        Library.Log($"  InletDiameterMm:            {d.InletDiameterMm:F2}");
        Library.Log($"  SwirlChamber D x L:         {d.SwirlChamberDiameterMm:F2} x {d.SwirlChamberLengthMm:F2}");
        Library.Log($"  InjectorAxialPositionRatio: {d.InjectorAxialPositionRatio:F3}");
        Library.Log($"  InjectorYaw / Pitch [deg]:  {d.InjectorYawAngleDeg:F2} / {d.InjectorPitchAngleDeg:F2}");
        Library.Log($"  ExpanderLength / HalfAngle: {d.ExpanderLengthMm:F2} / {d.ExpanderHalfAngleDeg:F2}");
        Library.Log($"  ExitDiameterMm:             {d.ExitDiameterMm:F2}");
        Library.Log($"  StatorVaneAngleDeg:         {d.StatorVaneAngleDeg:F2}");
        Library.Log($"  StatorHubDiameterMm:        {d.StatorHubDiameterMm:F2}  AxialLength: {d.StatorAxialLengthMm:F2}  BladeChord: {d.StatorBladeChordMm:F2}");
    }

    public static NozzleDesignInputs CloneDesignForLog(NozzleDesignInputs d) => new()
    {
        InletDiameterMm = d.InletDiameterMm,
        SwirlChamberDiameterMm = d.SwirlChamberDiameterMm,
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
