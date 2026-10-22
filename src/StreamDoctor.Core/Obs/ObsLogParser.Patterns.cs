using System.Text.RegularExpressions;

namespace StreamDoctor.Core.Obs;

/// <summary>Statistics and configuration line patterns, kept apart from event patterns.</summary>
public static partial class ObsLogParser
{
    [GeneratedRegex(
        """(?:Output '(?<output>[^']+)':\s*)?Number of dropped frames due to insufficient bandwidth:\s*(?<count>\d+)(?:\s*\((?<percent>\d+(?:[.,]\d+)?)%\))?$""",
        RegexOptions.CultureInvariant)]
    private static partial Regex DroppedFramesRegex();

    [GeneratedRegex(
        """(?:Output '(?<output>[^']+)':\s*)?Number of lagged frames due to rendering lag/stalls:\s*(?<count>\d+)(?:\s*\((?<percent>\d+(?:[.,]\d+)?)%\))?$""",
        RegexOptions.CultureInvariant)]
    private static partial Regex LaggedFramesRegex();

    [GeneratedRegex(
        """(?:Output '(?<output>[^']+)':\s*)?[Vv]ideo stopped, number of skipped frames due to encoding lag:\s*(?<count>\d+)/(?<total>\d+)(?:\s*\((?<percent>\d+(?:[.,]\d+)?)%\))?$""",
        RegexOptions.CultureInvariant)]
    private static partial Regex SkippedFramesRegex();

    [GeneratedRegex(
        """(?:Output '(?<output>[^']+)':\s*)?Total frames output:\s*(?<count>\d+)$""",
        RegexOptions.CultureInvariant)]
    private static partial Regex TotalFramesRegex();

    [GeneratedRegex("""samples per sec:\s*(?<rate>\d+)$""", RegexOptions.CultureInvariant)]
    private static partial Regex AudioRateRegex();

    [GeneratedRegex("""base resolution:\s*(?<w>\d+)x(?<h>\d+)$""", RegexOptions.CultureInvariant)]
    private static partial Regex BaseResolutionRegex();

    [GeneratedRegex("""output resolution:\s*(?<w>\d+)x(?<h>\d+)$""", RegexOptions.CultureInvariant)]
    private static partial Regex OutputResolutionRegex();

    [GeneratedRegex("""\bfps:\s*(?<fps>\d+(?:\.\d+)?)$""", RegexOptions.CultureInvariant)]
    private static partial Regex FpsRegex();

    [GeneratedRegex(
        """Connecting to (?:(?:[A-Za-z]+ )?URL )?(?<scheme>[a-z][a-z0-9+.\-]*)://(?<host>[^/\s:]+)""",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex StreamTargetRegex();
}
