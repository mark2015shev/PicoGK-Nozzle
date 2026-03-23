using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace PicoGK_Run.Infrastructure;

/// <summary>
/// Lightweight wall-clock + heap + GC counters around major pipeline stages.
/// <para>
/// <b>Heap bytes</b> via <see cref="GC.GetTotalMemory"/> are approximate (no full collection unless you force one elsewhere).
/// <b>Per-thread allocation</b> is not tracked under <see cref="Parallel.For"/> workers — use this to locate dominant stages, not micro-optimize allocations inside PicoGK native calls.
/// </para>
/// </summary>
public static class PipelineProfiler
{
    private static readonly object Gate = new();
    private static bool _enabled;
    private static readonly List<PipelineStageRecord> Records = new(24);

    /// <summary>Call at the start of a full pipeline run (e.g. <see cref="NozzleFlowCompositionRoot.Run"/>).</summary>
    public static void ResetSession(bool enabled)
    {
        lock (Gate)
        {
            _enabled = enabled;
            Records.Clear();
        }
    }

    public static bool IsEnabled
    {
        get
        {
            lock (Gate)
                return _enabled;
        }
    }

    /// <summary>Scoped stage timing; no-op when profiling is disabled.</summary>
    public static IDisposable Stage(string name)
    {
        lock (Gate)
        {
            if (!_enabled)
                return NullScope.Instance;
        }

        return new StageScope(name);
    }

    /// <summary>Immutable snapshot for <see cref="PipelineRunResult"/> and logging.</summary>
    public static PipelineProfileReport? TryBuildReport()
    {
        lock (Gate)
        {
            if (!_enabled || Records.Count == 0)
                return null;
            var copy = new List<PipelineStageRecord>(Records);
            double totalMs = 0;
            foreach (PipelineStageRecord r in copy)
                totalMs += r.ElapsedMs;
            return new PipelineProfileReport(copy, totalMs);
        }
    }

    private sealed class StageScope : IDisposable
    {
        private readonly string _name;
        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private readonly long _heapBefore = GC.GetTotalMemory(false);
        private readonly int _g0 = GC.CollectionCount(0);
        private readonly int _g1 = GC.CollectionCount(1);
        private readonly int _g2 = GC.CollectionCount(2);

        public StageScope(string name) => _name = name;

        public void Dispose()
        {
            _sw.Stop();
            long heapAfter = GC.GetTotalMemory(false);
            int dg0 = GC.CollectionCount(0) - _g0;
            int dg1 = GC.CollectionCount(1) - _g1;
            int dg2 = GC.CollectionCount(2) - _g2;
            lock (Gate)
            {
                if (_enabled)
                {
                    Records.Add(new PipelineStageRecord(
                        _name,
                        _sw.Elapsed.TotalMilliseconds,
                        _heapBefore,
                        heapAfter,
                        dg0,
                        dg1,
                        dg2));
                }
            }
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }

    /// <summary>Human-readable table for console / Library.Log.</summary>
    public static void AppendReportText(PipelineProfileReport? report, StringBuilder sb)
    {
        if (report == null || report.Stages.Count == 0)
        {
            sb.AppendLine("(pipeline profiling disabled or no stages recorded)");
            return;
        }

        sb.AppendLine("Stage                          ms        Δheap(MB)   GC0/1/2");
        sb.AppendLine("---------------------------------------------------------------");
        foreach (PipelineStageRecord r in report.Stages)
        {
            double dMb = (r.HeapBytesAfter - r.HeapBytesBefore) / (1024.0 * 1024.0);
            sb.AppendLine(
                $"{r.Name,-30} {r.ElapsedMs,8:F1} {dMb,12:F2}   {r.Gc0Delta}/{r.Gc1Delta}/{r.Gc2Delta}");
        }

        sb.AppendLine("---------------------------------------------------------------");
        sb.AppendLine($"TOTAL (sum of stages)        {report.TotalElapsedMs,8:F1} ms");
        sb.AppendLine(
            "Note: Sum of geometry.segment.* ≈ voxel assembly wall time; native PicoGK dominates inside those rows.");
    }
}

/// <summary>One completed profiler interval.</summary>
public readonly struct PipelineStageRecord
{
    public string Name { get; }
    public double ElapsedMs { get; }
    public long HeapBytesBefore { get; }
    public long HeapBytesAfter { get; }
    public int Gc0Delta { get; }
    public int Gc1Delta { get; }
    public int Gc2Delta { get; }

    public PipelineStageRecord(
        string name,
        double elapsedMs,
        long heapBytesBefore,
        long heapBytesAfter,
        int gc0Delta,
        int gc1Delta,
        int gc2Delta)
    {
        Name = name;
        ElapsedMs = elapsedMs;
        HeapBytesBefore = heapBytesBefore;
        HeapBytesAfter = heapBytesAfter;
        Gc0Delta = gc0Delta;
        Gc1Delta = gc1Delta;
        Gc2Delta = gc2Delta;
    }
}

/// <summary>Snapshot attached to <see cref="PipelineRunResult"/>.</summary>
public sealed class PipelineProfileReport
{
    public IReadOnlyList<PipelineStageRecord> Stages { get; }
    public double TotalElapsedMs { get; }

    public PipelineProfileReport(IReadOnlyList<PipelineStageRecord> stages, double totalElapsedMs)
    {
        Stages = stages;
        TotalElapsedMs = totalElapsedMs;
    }

    public string FormatText()
    {
        var sb = new StringBuilder(512);
        PipelineProfiler.AppendReportText(this, sb);
        return sb.ToString();
    }
}
