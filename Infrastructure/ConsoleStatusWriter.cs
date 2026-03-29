using System;
using System.Text.RegularExpressions;

namespace PicoGK_Run.Infrastructure;

/// <summary>
/// Single entry point for colored console status output. Saves/restores colors in <c>finally</c> on every fragment.
/// Does not use ANSI escapes; uses <see cref="Console.ForegroundColor"/> only inside guarded regions.
/// </summary>
public static class ConsoleStatusWriter
{
    /// <summary>Default foreground for non-status text (bright white on typical Windows consoles).</summary>
    private const ConsoleColor NormalForeground = ConsoleColor.White;

    /// <summary>Call once at startup so the first lines are white before any status color runs.</summary>
    public static void PrimeConsoleForStatusOutput() => Console.ForegroundColor = NormalForeground;

    /// <summary>Reset console colors to defaults (call once at process exit as a safety net).</summary>
    public static void SafetyResetConsoleColors() => Console.ResetColor();

    private static ConsoleColor MapStatus(StatusLevel level) => level switch
    {
        StatusLevel.Pass => ConsoleColor.Green,
        StatusLevel.Warning => ConsoleColor.Yellow,
        StatusLevel.Error => ConsoleColor.Red,
        StatusLevel.Normal => NormalForeground,
        _ => NormalForeground
    };

    /// <summary>Writes text then leaves foreground white so later output does not inherit status colors.</summary>
    public static void Write(string text, StatusLevel level)
    {
        if (string.IsNullOrEmpty(text))
            return;

        ConsoleColor pb = Console.BackgroundColor;
        try
        {
            Console.ForegroundColor = MapStatus(level);
            Console.Write(text);
        }
        finally
        {
            Console.ForegroundColor = NormalForeground;
            Console.BackgroundColor = pb;
        }
    }

    public static void WriteLine(string text, StatusLevel level)
    {
        if (level == StatusLevel.Normal)
        {
            ConsoleColor pb = Console.BackgroundColor;
            try
            {
                Console.ForegroundColor = NormalForeground;
                Console.WriteLine(text ?? string.Empty);
            }
            finally
            {
                Console.ForegroundColor = NormalForeground;
                Console.BackgroundColor = pb;
            }

            return;
        }

        if (!string.IsNullOrEmpty(text) && TryWriteTrailingStatusLine(text))
            return;

        Write(text ?? string.Empty, level);
        ConsoleColor pb2 = Console.BackgroundColor;
        try
        {
            Console.ForegroundColor = NormalForeground;
            Console.WriteLine();
        }
        finally
        {
            Console.ForegroundColor = NormalForeground;
            Console.BackgroundColor = pb2;
        }
    }

    public static void WriteLabeledLine(string label, string text, StatusLevel labelLevel)
    {
        Write(label ?? string.Empty, labelLevel);
        Write(": ", StatusLevel.Normal);
        WriteLine(text ?? string.Empty, StatusLevel.Normal);
    }

    private static readonly Regex TrailingStatusRx = new(
        "^(?<prefix>.*)(?<sep>:\\s*)(?<tok>PASS|WARNING|FAIL|INVALID)\\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static bool TryWriteTrailingStatusLine(string line)
    {
        Match m = TrailingStatusRx.Match(line);
        if (!m.Success)
            return false;

        string tok = m.Groups["tok"].Value;
        StatusLevel tokLevel = ClassifyStatusToken(tok);
        if (tokLevel == StatusLevel.Normal)
            return false;

        string prefix = m.Groups["prefix"].Value;
        string sep = m.Groups["sep"].Value;
        Write(prefix, StatusLevel.Normal);
        Write(sep, StatusLevel.Normal);
        Write(tok, tokLevel);
        ConsoleColor pb = Console.BackgroundColor;
        try
        {
            Console.ForegroundColor = NormalForeground;
            Console.WriteLine();
        }
        finally
        {
            Console.ForegroundColor = NormalForeground;
            Console.BackgroundColor = pb;
        }

        return true;
    }

    private static StatusLevel ClassifyStatusToken(string tok)
    {
        if (tok.Equals("PASS", StringComparison.OrdinalIgnoreCase))
            return StatusLevel.Pass;
        if (tok.Equals("WARNING", StringComparison.OrdinalIgnoreCase))
            return StatusLevel.Warning;
        if (tok.Equals("FAIL", StringComparison.OrdinalIgnoreCase))
            return StatusLevel.Error;
        if (tok.Equals("INVALID", StringComparison.OrdinalIgnoreCase))
            return StatusLevel.Error;
        return StatusLevel.Normal;
    }

    private static readonly Regex LeadingWarningRx = new(
        "^(?<pre>\\s*)(?<w>WARNING)(?<post>\\b.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static bool TryWriteLeadingWarningLine(string line)
    {
        Match m = LeadingWarningRx.Match(line);
        if (!m.Success)
            return false;

        Write(m.Groups["pre"].Value, StatusLevel.Normal);
        Write(m.Groups["w"].Value, StatusLevel.Warning);
        string suffix = line.Substring(m.Groups["w"].Index + m.Groups["w"].Length);
        WriteLine(suffix, ClassifyReportingLine(suffix));
        return true;
    }

    public static void WriteClassifiedLine(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            ConsoleColor pb = Console.BackgroundColor;
            try
            {
                Console.ForegroundColor = NormalForeground;
                Console.WriteLine();
            }
            finally
            {
                Console.ForegroundColor = NormalForeground;
                Console.BackgroundColor = pb;
            }

            return;
        }

        if (TryWriteTrailingStatusLine(line))
            return;

        if (TryWriteLeadingWarningLine(line))
            return;

        WriteLine(line, ClassifyReportingLine(line));
    }

    /// <summary>Trailing <c>: PASS|WARNING|FAIL|INVALID</c> wins over mid-line keywords so PASS stays green.</summary>
    private static StatusLevel ClassifyTrailingColonToken(string line)
    {
        int c = line.LastIndexOf(':');
        if (c < 0 || c >= line.Length - 1)
            return StatusLevel.Normal;

        ReadOnlySpan<char> tail = line.AsSpan(c + 1).Trim();
        if (tail.IsEmpty)
            return StatusLevel.Normal;

        if (tail.StartsWith("PASS", StringComparison.OrdinalIgnoreCase)
            && (tail.Length == 4 || !char.IsLetterOrDigit(tail[4])))
            return StatusLevel.Pass;

        if (tail.StartsWith("WARNING", StringComparison.OrdinalIgnoreCase)
            && (tail.Length == 7 || !char.IsLetterOrDigit(tail[7])))
            return StatusLevel.Warning;

        if (tail.StartsWith("FAIL", StringComparison.OrdinalIgnoreCase)
            && (tail.Length == 4 || !char.IsLetterOrDigit(tail[4])))
            return StatusLevel.Error;

        if (tail.StartsWith("INVALID", StringComparison.OrdinalIgnoreCase)
            && (tail.Length == 7 || !char.IsLetterOrDigit(tail[7])))
            return StatusLevel.Error;

        return StatusLevel.Normal;
    }

    /// <summary>Pattern-based classifier for known report strings — conservative; returns <see cref="StatusLevel.Normal"/> when unsure.</summary>
    public static StatusLevel ClassifyReportingLine(string line)
    {
        if (string.IsNullOrEmpty(line))
            return StatusLevel.Normal;

        StatusLevel trailing = ClassifyTrailingColonToken(line);
        if (trailing != StatusLevel.Normal)
            return trailing;

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

    public static void LogLibraryAndConsole(Action<string> libraryLog, StatusLevel level, string line)
    {
        libraryLog(line);
        if (level != StatusLevel.Normal)
            WriteLine(line, level);
    }

    public static void LogLibraryAndConsoleIfSignificant(Action<string> libraryLog, string line)
    {
        libraryLog(line);
        StatusLevel s = ClassifyReportingLine(line);
        if (s != StatusLevel.Normal)
            WriteClassifiedLine(line);
        else
            WriteLine(line, StatusLevel.Normal);
    }

    public static void WriteLineToConsoleWithOptionalLibrary(Action<string>? libraryLog, string line)
    {
        libraryLog?.Invoke(line);
        WriteClassifiedLine(line);
    }
}
