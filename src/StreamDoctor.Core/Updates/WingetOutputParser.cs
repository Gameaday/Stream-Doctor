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
/// Parses winget's table output. winget has no stable JSON output for list/upgrade,
/// so this parser is deliberately tolerant: ANSI escapes, progress noise, missing
/// columns and localized-ish spacing are all handled, and anything ambiguous is skipped.
/// </summary>
public static partial class WingetOutputParser
{
    public static IReadOnlyList<WingetPackage> Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var lines = Clean(output).Split('\n');

        var headerIndex = Array.FindIndex(lines, static line =>
            line.Contains("Name", StringComparison.OrdinalIgnoreCase) &&
            line.Contains("Id", StringComparison.OrdinalIgnoreCase) &&
            line.Contains("Version", StringComparison.OrdinalIgnoreCase));

        var packages = new List<WingetPackage>();

        if (headerIndex >= 0)
        {
            // Header found, use positional parsing for data lines
            return ParsePositional(lines, headerIndex + 1);
        }

        // Fallback: no recognizable header (older winget or a filtered query).
        foreach (var line in lines)
        {
            var cells = SplitCells(line);
            if (cells.Length < 4 || !LooksLikePackageId(cells[1]))
            {
                continue;
            }

            packages.Add(new WingetPackage(cells[0], cells[1], CleanCell(cells[2]), CleanCell(cells[3]), null));
        }

        return packages;
    }

    private static int[] ComputeColumnBoundsFromHeader(string header)
    {
        // Find column separator positions in the header (runs of 3+ spaces)
        var bounds = new List<int> { 0 };
        
        var matches = System.Text.RegularExpressions.Regex.Matches(header, @"\s{3,}");
        foreach (Match match in matches)
        {
            // The column starts after the separator
            var colStart = match.Index + match.Length;
            if (colStart < header.Length)
            {
                bounds.Add(colStart);
            }
        }

        // Also add the header text start positions for robustness
        var names = new[] { "Id", "Version", "Available", "Source" };
        foreach (var name in names)
        {
            var pos = header.IndexOf(name, StringComparison.OrdinalIgnoreCase);
            if (pos > 0 && !bounds.Contains(pos))
            {
                bounds.Add(pos);
            }
        }

        bounds.Sort();
        return bounds.Distinct().ToArray();
    }

    private static string[] ExtractCellsByBounds(string line, int[] bounds)
    {
        var cells = new List<string>();
        for (var i = 0; i < bounds.Length; i++)
        {
            var start = bounds[i];
            var end = i + 1 < bounds.Length ? bounds[i + 1] : line.Length;
            if (start < line.Length)
            {
                var cell = line[start..Math.Min(end, line.Length)].Trim();
                if (cell.Length > 0)
                {
                    cells.Add(cell);
                }
            }
        }
        return cells.ToArray();
    }

    private static string? TryExtractIdFromName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !name.Contains('.'))
        {
            return null;
        }
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && LooksLikePackageId(parts[^1]) ? parts[^1] : null;
    }

    private static List<WingetPackage> ParsePositional(string[] lines, int startIndex)
    {
        var packages = new List<WingetPackage>();
        for (var index = startIndex; index < lines.Length; index++)
        {
            var cells = SplitCells(lines[index]);
            Console.WriteLine($"  PARSE: '{lines[index]}' -> [{string.Join(" | ", cells)}]");
            var needsFix = cells.Length < 4 || !LooksLikePackageId(cells[1]) || TryExtractIdFromName(cells[0]) != null;
            if (needsFix)
            {
                var originalCells = cells;
                cells = TryFixCells(cells);
                Console.WriteLine($"  FIX: [{string.Join(" | ", cells)}] -> [{string.Join(" | ", cells)}]");
                if (cells.Length < 4 || !LooksLikePackageId(cells[1]))
                {
                    continue;
                }
            }

            packages.Add(new WingetPackage(
                cells[0],
                cells[1],
                cells.Length > 2 ? CleanCell(cells[2]) : null,
                cells.Length > 3 ? CleanCell(cells[3]) : null,
                cells.Length > 4 ? CleanCell(cells[4]) : null));
        }
        return packages;
    }

    private static string[] TryFixCells(string[] cells)
    {
        // If we have 4 cells and the first cell contains a package ID at the end, split it
        if (cells.Length == 4 && cells[0].Contains('.'))
        {
            var parts = cells[0].Split(' ');
            if (parts.Length >= 2 && LooksLikePackageId(parts[^1]))
            {
                var id = parts[^1];
                var name = string.Join(' ', parts[..^1]);
                // Insert the ID at position 1, shifting the rest right
                return [name, id, cells[1], cells[2], cells[3]];
            }
        }
        return cells;
    }

    /// <summary>Finds the installed version of one package id in <c>winget list</c> output.</summary>
    public static string? FindInstalledVersion(string output, string packageId)
    {
        ArgumentNullException.ThrowIfNull(output);

        return Parse(output)
            .FirstOrDefault(package => string.Equals(package.Id, packageId, StringComparison.OrdinalIgnoreCase))
            ?.InstalledVersion;
    }

    private static string[] SplitCells(string line) =>
        CellSeparator().Split(line.Trim()).Where(static cell => cell.Length > 0).ToArray();

    private static bool LooksLikePackageId(string value) =>
        value.Length > 2 &&
        value.Contains('.', StringComparison.Ordinal) &&
        !value.Contains(' ', StringComparison.Ordinal) &&
        value.All(static character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' or '+');

    private static string? CleanCell(string value)
    {
        var cleaned = value.Trim();
        return cleaned.Length == 0 || cleaned.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ? null : cleaned;
    }

    private static string Clean(string output) => AnsiEscape().Replace(output, string.Empty).Replace("\r", string.Empty, StringComparison.Ordinal);

    [GeneratedRegex(@"\x1B\[[0-9;?]*[ -/]*[@-~]")]
    private static partial Regex AnsiEscape();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex CellSeparator();
}