using System;

namespace PicoGK_Run.Infrastructure;

/// <summary>
/// Back-compat facade for classified status console output; all color changes go through <see cref="ConsoleStatusWriter"/>.
/// </summary>
public static class ConsoleReportColor
{
    public static void WriteNormalLine(string text) =>
        ConsoleStatusWriter.WriteLine(text ?? string.Empty, StatusLevel.Normal);

    public static void WriteLine(StatusLevel level, string text) =>
        ConsoleStatusWriter.WriteLine(text ?? string.Empty, level);

    public static void WritePass(string text) =>
        ConsoleStatusWriter.WriteLine(text ?? string.Empty, StatusLevel.Pass);

    public static void WriteWarning(string text) =>
        ConsoleStatusWriter.WriteLine(text ?? string.Empty, StatusLevel.Warning);

    public static void WriteError(string text) =>
        ConsoleStatusWriter.WriteLine(text ?? string.Empty, StatusLevel.Error);

    public static void WriteStatusLine(string label, StatusLevel level, string details) =>
        ConsoleStatusWriter.WriteLabeledLine(label, details, level);

    public static void LogLibraryAndConsole(Action<string> libraryLog, StatusLevel level, string line)
    {
        libraryLog(line);
        if (level != StatusLevel.Normal)
            ConsoleStatusWriter.WriteLine(line, level);
    }

    public static void LogLibraryAndConsoleIfSignificant(Action<string> libraryLog, string line)
    {
        libraryLog(line);
        if (ConsoleStatusWriter.ClassifyReportingLine(line) != StatusLevel.Normal)
            ConsoleStatusWriter.WriteClassifiedLine(line);
        else
            ConsoleStatusWriter.WriteLine(line, StatusLevel.Normal);
    }

    public static void WriteLineToConsoleWithOptionalLibrary(Action<string>? libraryLog, string line) =>
        ConsoleStatusWriter.WriteLineToConsoleWithOptionalLibrary(libraryLog, line);

    public static void WriteClassifiedLine(string line) => ConsoleStatusWriter.WriteClassifiedLine(line);

    public static StatusLevel ClassifyReportingLine(string line) => ConsoleStatusWriter.ClassifyReportingLine(line);
}
