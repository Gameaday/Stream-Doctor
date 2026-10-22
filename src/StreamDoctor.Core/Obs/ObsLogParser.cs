using System.Text.RegularExpressions;

namespace StreamDoctor.Core.Obs;

/// <summary>
/// Parses OBS Studio log files into sessions, statistics and normalized evidence.
/// Pure string processing (no file I/O) so golden-file fixtures drive the tests.
/// </summary>
/// <remarks>
/// OBS log timestamps are local wall-clock with millisecond precision and no date.
/// Callers pass the log's date and the local time zone; timestamps that move
/// backwards by more than twelve hours are treated as a midnight rollover.
/// </remarks>
public static partial class ObsLogParser
{
    /// <summary>Parses a complete log file.</summary>
    /// <param name="logText">Raw log contents (LF or CRLF).</param>
    /// <param name="logDate">Date the log belongs to, usually from the file name or write time.</param>
    /// <param name="timeZone">Local time zone the log was written in; defaults to the system zone.</param>
    /// <param name="logFileName">Optional file name, recorded for the debrief.</param>
    public static ObsLogDocument Parse(
        string logText,
        DateOnly logDate,
        TimeZoneInfo? timeZone = null,
        string? logFileName = null)
    {
        ArgumentNullException.ThrowIfNull(logText);

        var builder = new LogBuilder(logDate, timeZone ?? TimeZoneInfo.Local, logFileName);

        foreach (var line in logText.Split('\n'))
        {
            builder.Consume(line.TrimEnd('\r'));
        }

        return builder.Build();
    }

    // ---- Line and session markers -------------------------------------------------

    [GeneratedRegex("""^(?<time>\d{2}:\d{2}:\d{2}\.\d{3}):\s?(?<message>.*)$""", RegexOptions.CultureInvariant)]
    private static partial Regex LineRegex();

    [GeneratedRegex("""\bStreaming Start\b""", RegexOptions.CultureInvariant)]
    private static partial Regex StreamingStartRegex();

    [GeneratedRegex("""\bStreaming Stop\b""", RegexOptions.CultureInvariant)]
    private static partial Regex StreamingStopRegex();

    [GeneratedRegex("""\bRecording Start\b""", RegexOptions.CultureInvariant)]
    private static partial Regex RecordingStartRegex();

    [GeneratedRegex("""\bRecording Stop\b""", RegexOptions.CultureInvariant)]
    private static partial Regex RecordingStopRegex();

    [GeneratedRegex("""\bOBS\s+(?<version>\d+\.\d+(?:\.\d+)?)""", RegexOptions.CultureInvariant)]
    private static partial Regex ObsVersionRegex();

    // ---- OBS events ---------------------------------------------------------------

    [GeneratedRegex("""Switched to scene '(?<scene>.+)'""", RegexOptions.CultureInvariant)]
    private static partial Regex SceneRegex();

    [GeneratedRegex(
        """\[(?<module>[^\]:]+): '(?<source>[^']+)'\]\s+Device '(?<device>[^']+)'\s+(?:invalidated|disconnected|lost)""",
        RegexOptions.CultureInvariant)]
    private static partial Regex SourceDisconnectedWithModuleRegex();

    [GeneratedRegex(
        """Device '(?<device>[^']+)'\s+(?:invalidated|disconnected|lost)""",
        RegexOptions.CultureInvariant)]
    private static partial Regex SourceDisconnectedRegex();

    [GeneratedRegex("""device_removed|Device unplugged""", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex DeviceRemovedRegex();

    [GeneratedRegex(
        """Device '(?<device>[^']+)'\s+(?:found|reconnected|connected)""",
        RegexOptions.CultureInvariant)]
    private static partial Regex SourceReconnectedRegex();

    [GeneratedRegex(
        """(?:Failed to load module|LoadLibrary failed for|Failed to load plugin)\s*'?(?<plugin>[^'\s]+)'?""",
        RegexOptions.CultureInvariant)]
    private static partial Regex PluginLoadFailedRegex();

    [GeneratedRegex("""^\s+(?<plugin>[\w\-.]+\.(?:dll|so))\s*$""", RegexOptions.CultureInvariant)]
    private static partial Regex PluginListEntryRegex();

    [GeneratedRegex("""^Loaded Modules:\s*$""", RegexOptions.CultureInvariant)]
    private static partial Regex PluginListHeaderRegex();
}
