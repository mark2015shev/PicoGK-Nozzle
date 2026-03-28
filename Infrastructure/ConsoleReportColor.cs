using System;

namespace PicoGK_Run.Infrastructure;

/// <summary>Semantic level for terminal status lines (PASS / WARNING / ERROR families).</summary>
public enum StatusLevel
{
    Normal,
    Pass,
    Warning,
    Error
}

/// <summary>
/// Colored <see cref="Console"/> output for scan-friendly status. Saves/restores <see cref="Console.ForegroundColor"/> per write.
/// </summary>
public static class ConsoleReportColor
{
    public static void WriteNormalLine(string text) => Console.WriteLine(text);

    public static void WriteLine(StatusLevel level, string text)
    {
        if (level == StatusLevel.Normal)
        {
            Console.WriteLine(text);
            return;
        }

        ConsoleColor previous = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = level switch
            {
                StatusLevel.Pass => ConsoleColor.Green,
                StatusLevel.Warning => ConsoleColor.Yellow,
                StatusLevel.Error => ConsoleColor.Red,
                _ => previous
            };
            Console.WriteLine(text);
        }
        finally
        {
            Console.ForegroundColor = previous;
        }
    }

    public static void WritePass(string text) => WriteLine(StatusLevel.Pass, text);

    public static void WriteWarning(string text) => WriteLine(StatusLevel.Warning, text);

    public static void WriteError(string text) => WriteLine(StatusLevel.Error, text);

    public static void WriteStatusLine(string label, StatusLevel level, string details) =>
        WriteLine(level, $"{label}: {details}");

    /// <summary>Optional library sink (e.g. <see cref="PicoGK.Library.Log"/>) then colored console when <paramref name="level"/> is not normal.</summary>
    public static void LogLibraryAndConsole(Action<string> libraryLog, StatusLevel level, string line)
    {
        libraryLog(line);
        if (level != StatusLevel.Normal)
            WriteLine(level, line);
    }

    /// <summary>Always calls <paramref name="libraryLog"/>; console is colored only when the line classifies as non-normal.</summary>
    public static void LogLibraryAndConsoleIfSignificant(Action<string> libraryLog, string line)
    {
        libraryLog(line);
        StatusLevel s = ClassifyReportingLine(line);
        if (s != StatusLevel.Normal)
            WriteLine(s, line);
    }

    /// <summary>Console + optional library with the same classification rule as <see cref="LogLibraryAndConsoleIfSignificant"/>.</summary>
    public static void WriteLineToConsoleWithOptionalLibrary(Action<string>? libraryLog, string line)
    {
        libraryLog?.Invoke(line);
        WriteLine(ClassifyReportingLine(line), line);
    }

    /// <summary>Console only, color from <see cref="ClassifyReportingLine"/>.</summary>
    public static void WriteClassifiedLine(string line) => WriteLine(ClassifyReportingLine(line), line);

    /// <summary>Heuristic classifier for known report strings — conservative; returns <see cref="StatusLevel.Normal"/> when unsure.</summary>
    public static StatusLevel ClassifyReportingLine(string line)
    {
        if (string.IsNullOrEmpty(line))
            return StatusLevel.Normal;

        string t = line.TrimStart();

        if (t.StartsWith("WARNING", StringComparison.OrdinalIgnoreCase))
            return StatusLevel.Warning;

        if (t.StartsWith("FAIL:", StringComparison.OrdinalIgnoreCase))
            return StatusLevel.Error;

        if (t.Contains("CAPACITY FAIL", StringComparison.OrdinalIgnoreCase))
            return StatusLevel.Error;

        if (t.Contains("NON-FINITE", StringComparison.OrdinalIgnoreCase))
            return StatusLevel.Error;

        if (t.Contains("REJECTED", StringComparison.OrdinalIgnoreCase))
            return StatusLevel.Error;

        if (t.Contains("choking risk", StringComparison.OrdinalIgnoreCase))
            return StatusLevel.Error;

        if (t.StartsWith("INVALID", StringComparison.OrdinalIgnoreCase))
            return StatusLevel.Error;

        if (t.Contains("INVALID", StringComparison.OrdinalIgnoreCase) &&
            (t.Contains("Thrust CV", StringComparison.Ordinal) ||
             t.Contains("SI thrust", StringComparison.OrdinalIgnoreCase)))
            return StatusLevel.Error;

        if (t.StartsWith("SI HARD ASSERT", StringComparison.OrdinalIgnoreCase))
            return StatusLevel.Error;

        if (t.StartsWith("CAUTION", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("BORDERLINE", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("DEGRADED", StringComparison.OrdinalIgnoreCase))
            return StatusLevel.Warning;

        if (t.StartsWith("SUCCESS", StringComparison.OrdinalIgnoreCase))
            return StatusLevel.Pass;

        if (t.StartsWith("HEALTHY", StringComparison.OrdinalIgnoreCase))
            return StatusLevel.Pass;

        if (line.Contains("derived-state physics:", StringComparison.Ordinal) ||
            line.Contains("choking vs mdot", StringComparison.Ordinal) ||
            line.Contains("overall live consistency:", StringComparison.Ordinal) ||
            line.Contains("choking @ P0_derived:", StringComparison.Ordinal) ||
            line.Contains("overall consistency:", StringComparison.Ordinal))
            return ClassifyPassFailTail(line);

        if (line.Contains("combined classification:", StringComparison.OrdinalIgnoreCase))
            return ClassifyPassFailTail(line);

        if (line.Contains("  result:", StringComparison.Ordinal))
            return ClassifyPassFailTail(line);

        if (line.Contains("Post-hoc capacity classification", StringComparison.Ordinal))
            return ClassifyPassFailTail(line);

        if (line.Contains("shaping invariants OK:", StringComparison.OrdinalIgnoreCase))
        {
            if (line.Contains("False", StringComparison.Ordinal))
                return StatusLevel.Error;
            if (line.Contains("True", StringComparison.Ordinal))
                return StatusLevel.Pass;
        }

        if (line.Contains("Governor trimmed entrainment:", StringComparison.Ordinal) &&
            line.Contains("YES", StringComparison.Ordinal))
            return StatusLevel.Warning;

        if (line.Contains("Choked step:", StringComparison.Ordinal) && line.Contains("True", StringComparison.Ordinal))
            return StatusLevel.Error;

        if (line.Contains("Any entrainment step choked:", StringComparison.Ordinal) && line.Contains("True", StringComparison.Ordinal))
            return StatusLevel.Error;

        if (line.Contains("Entrainment capped by passage governor:", StringComparison.Ordinal))
        {
            int idx = line.IndexOf("governor:", StringComparison.Ordinal);
            if (idx >= 0 && int.TryParse(line.AsSpan(idx + "governor:".Length).TrimStart(), out int n) && n > 0)
                return StatusLevel.Warning;
        }

        if (line.Contains("OK=False", StringComparison.OrdinalIgnoreCase))
            return StatusLevel.Error;

        if (line.Contains("OK=True", StringComparison.OrdinalIgnoreCase))
            return StatusLevel.Pass;

        if (line.Contains("Swirl-passage ṁ cap steps:", StringComparison.Ordinal))
        {
            int idx = line.IndexOf("steps:", StringComparison.Ordinal);
            if (idx >= 0 && int.TryParse(line.AsSpan(idx + "steps:".Length).TrimStart(), out int n) && n > 0)
                return StatusLevel.Warning;
        }

        return StatusLevel.Normal;
    }

    private static StatusLevel ClassifyPassFailTail(string line)
    {
        int c = line.LastIndexOf(':');
        if (c < 0)
            return StatusLevel.Normal;
        ReadOnlySpan<char> tail = line.AsSpan(c + 1).Trim();
        if (tail.StartsWith("PASS", StringComparison.OrdinalIgnoreCase))
            return StatusLevel.Pass;
        if (tail.StartsWith("WARNING", StringComparison.OrdinalIgnoreCase))
            return StatusLevel.Warning;
        if (tail.StartsWith("FAIL", StringComparison.OrdinalIgnoreCase))
            return StatusLevel.Error;
        if (tail.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
            return StatusLevel.Pass;
        if (tail.StartsWith("VALID", StringComparison.OrdinalIgnoreCase))
            return StatusLevel.Pass;
        return StatusLevel.Normal;
    }
}
