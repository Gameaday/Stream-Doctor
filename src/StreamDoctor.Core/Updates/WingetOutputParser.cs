using System.Text.RegularExpressions;

namespace StreamDoctor.Core.Updates;

/// <summary>A row from <c>winget list</c> or <c>winget upgrade</c>.</summary>
public sealed record WingetPackage(
    string Name,
    string Id,
    string? InstalledVersion,
    string? AvailableVersion,
    string? Source);

/// <summary>
/// Parses winget table output. winget has no stable JSON for list/upgrade, so this
/// parser is right-anchored: the source, available and installed versions are the
/// last tokens on a row, and the package id is the right-most token before them that
/// looks like an id. That survives names wider than the header column, missing
/// sources and ANSI/progress noise, which a fixed-width header splitter does not.
/// </summary>
public static partial class WingetOutputParser
{
    private static readonly string[] Sources = ["winget", "msstore"];

    public static IReadOnlyList<WingetPackage> Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var packages = new List<WingetPackage>();

        foreach (var rawLine in Clean(output).Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || IsNoise(line))
            {
                continue;
            }

            var package = ParseRow(line);
            if (package is not null)
            {
                packages.Add(package);
            }
        }

        return packages;
    }

    /// <summary>Finds the installed version of one package id in <c>winget list</c> output.</summary>
    public static string? FindInstalledVersion(string output, string packageId)
    {
        ArgumentNullException.ThrowIfNull(output);

        return Parse(output)
            .FirstOrDefault(package => string.Equals(package.Id, packageId, StringComparison.OrdinalIgnoreCase))
            ?.InstalledVersion;
    }

    private static WingetPackage? ParseRow(string line)
    {
        if (TryParseRightAnchored(line, out var package))
        {
            return package;
        }

        // Well-formed tables with a single-space name can still be split on wide gaps.
        var cells = CellSeparator().Split(line).Where(static cell => cell.Length > 0).ToArray();
        if (cells.Length >= 4 && LooksLikePackageId(cells[1]) && !IsVersionToken(cells[1]))
        {
            return new WingetPackage(
                cells[0],
                cells[1],
                CleanCell(cells[2]),
                CleanCell(cells[3]),
                cells.Length > 4 ? CleanCell(cells[4]) : null);
        }

        return null;
    }

    private static bool TryParseRightAnchored(string line, out WingetPackage? package)
    {
        package = null;

        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 4)
        {
            return false;
        }

        var index = tokens.Length - 1;
        string? source = null;
        if (Sources.Contains(tokens[index], StringComparer.OrdinalIgnoreCase))
        {
            source = tokens[index];
            index--;
        }

        if (index < 3 || !IsVersionToken(tokens[index]))
        {
            return false;
        }

        var available = CleanCell(tokens[index]);
        index--;

        if (!IsVersionToken(tokens[index]))
        {
            return false;
        }

        var installed = CleanCell(tokens[index]);
        index--;

        for (var idIndex = index; idIndex >= 1; idIndex--)
        {
            var candidate = tokens[idIndex];
            if (!LooksLikePackageId(candidate) || IsVersionToken(candidate))
            {
                continue;
            }

            var name = string.Join(' ', tokens[..idIndex]).Trim();
            if (name.Length == 0)
            {
                continue;
            }

            package = new WingetPackage(name, candidate, installed, available, source);
            return true;
        }

        return false;
    }

    private static bool IsNoise(string line) =>
        line.Contains("---", StringComparison.Ordinal) ||
        line.Contains("No installed package", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("No applicable update", StringComparison.OrdinalIgnoreCase) ||
        (line.Contains("Name", StringComparison.OrdinalIgnoreCase) &&
         line.Contains("Id", StringComparison.OrdinalIgnoreCase) &&
         line.Contains("Version", StringComparison.OrdinalIgnoreCase));

    private static bool IsVersionToken(string token) =>
        token.Equals("Unknown", StringComparison.OrdinalIgnoreCase) || SoftwareVersion.TryParse(token, out _);

    private static bool LooksLikePackageId(string value) =>
        value.Length > 2 &&
        value.Contains('.', StringComparison.Ordinal) &&
        !value.Contains(' ', StringComparison.Ordinal) &&
        value.All(static character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' or '+');

    private static string? CleanCell(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 || trimmed.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ? null : trimmed;
    }

    private static string Clean(string output) =>
        AnsiEscape().Replace(output, string.Empty).Replace("\r", string.Empty, StringComparison.Ordinal);

    [GeneratedRegex(@"\x1B\[[0-9;?]*[ -/]*[@-~]")]
    private static partial Regex AnsiEscape();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex CellSeparator();
}
