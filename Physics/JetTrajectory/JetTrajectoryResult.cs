using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Physics.JetTrajectory;

/// <summary>
/// Full output of the optional jet-trajectory march: per-injector polylines, counters, and comparison vs legacy axial path.
/// </summary>
public sealed class JetTrajectoryResult
{
    public IReadOnlyList<IReadOnlyList<JetTrajectorySample>> TrajectoriesByInjector { get; init; } =
        Array.Empty<IReadOnlyList<JetTrajectorySample>>();

    /// <summary>Mean axial distance used as legacy 1-D proxy (injector plane → chamber downstream face) [mm].</summary>
    public double LegacyMeanPathLengthMm { get; init; }

    /// <summary>Mean accumulated traced arc length per injector [mm].</summary>
    public double TracedMeanPathLengthMm { get; init; }

    public int TotalWallDeflectionSteps { get; init; }
    public int TotalJetInteractionSteps { get; init; }

    /// <summary>Mean dot(final direction, +X) over injectors that produced at least two samples.</summary>
    public double MeanFinalAxisAlignment { get; init; }

    public IReadOnlyList<string> GeometryGuidanceHints { get; init; } = Array.Empty<string>();

    /// <summary>Emits comparison block: legacy vs traced, lengths, interaction counts, axis alignment.</summary>
    public static void LogComparisonToLibrary(
        JetTrajectoryResult? r,
        RunConfiguration run,
        Action<string> log)
    {
        if (r == null)
        {
            log("=== JET TRAJECTORY COMPARISON (reduced-order model; not CFD) ===");
            log("  Active mode: legacy injector path (1-D SI march / injector station).");
            log("  Physics-traced trajectory mode: OFF (UsePhysicsTracedJetTrajectory=false).");
            log("  Legacy mean axial path length [mm]: n/a (traced march not run).");
            log("  Traced mean path length (arc) [mm]: n/a");
            log("  Wall deflection steps: n/a  |  Jet–jet interaction steps: n/a");
            log("  Mean final exit direction alignment toward nozzle axis (+X), cos(θ): n/a");
            return;
        }

        log("=== JET TRAJECTORY COMPARISON (reduced-order engineering trace; not CFD) ===");
        log("  Active mode: physics-traced jet trajectories (optional 3-D step march).");
        log($"  Legacy injector path mode — mean axial span (injector plane → chamber downstream face) [mm]: {r.LegacyMeanPathLengthMm:F2}");
        log($"  Traced trajectory mode — mean arc length per injector [mm]:              {r.TracedMeanPathLengthMm:F2}");
        log($"  Sub-models: wall deflection {run.UseWallDeflection}  |  jet–jet {run.UseJetJetInteraction}  |  envelope {run.UseTrajectoryExpansionEnvelope}");
        log($"  Wall deflection steps (cumulative):     {r.TotalWallDeflectionSteps}");
        log($"  Jet–jet interaction steps (cumulative): {r.TotalJetInteractionSteps}");
        log($"  Mean final exit direction alignment toward +X, cos(θ) [-]: {r.MeanFinalAxisAlignment:F4}");
        if (r.GeometryGuidanceHints.Count > 0)
        {
            log("  Geometry guidance hints:");
            foreach (string h in r.GeometryGuidanceHints)
                log("    · " + h);
        }
    }
}
