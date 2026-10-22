using System.Globalization;

namespace StreamDoctor.Core.Updates;

/// <summary>
/// A dot-separated version (`31.0.1`, `2024.10.2.1`, `1.2.0-beta1`). Missing parts
/// compare as zero; a pre-release sorts before its release, matching SemVer.
/// </summary>
public readonly record struct SoftwareVersion : IComparable<SoftwareVersion>
{
    private readonly int[] _parts;

    private SoftwareVersion(int[] parts, string? preRelease)
    {
        _parts = parts;
        PreRelease = preRelease;
    }

    public string? PreRelease { get; }

    public IReadOnlyList<int> Parts => _parts ?? [];

    public int Major => Part(0);

    public int Minor => Part(1);

    public int Patch => Part(2);

    public static bool TryParse(string? text, out SoftwareVersion version)
    {
        version = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var span = text.Trim();
        if (span.StartsWith('v') || span.StartsWith('V'))
        {
            span = span[1..];
        }

        // Drop build metadata, keep pre-release.
        var plus = span.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
        {
            span = span[..plus];
        }

        string? preRelease = null;
        var dash = span.IndexOf('-', StringComparison.Ordinal);
        if (dash >= 0)
        {
            preRelease = span[(dash + 1)..];
            span = span[..dash];
        }

        var tokens = span.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return false;
        }

        var parts = new int[tokens.Length];
        for (var index = 0; index < tokens.Length; index++)
        {
            if (!int.TryParse(tokens[index], NumberStyles.None, CultureInfo.InvariantCulture, out parts[index]))
            {
                return false;
            }
        }

        version = new SoftwareVersion(parts, string.IsNullOrEmpty(preRelease) ? null : preRelease);
        return true;
    }

    public static SoftwareVersion Parse(string text) =>
        TryParse(text, out var version)
            ? version
            : throw new FormatException($"'{text}' is not a software version.");

    public int CompareTo(SoftwareVersion other)
    {
        var left = _parts ?? [];
        var right = other._parts ?? [];
        var length = Math.Max(left.Length, right.Length);

        for (var index = 0; index < length; index++)
        {
            var a = index < left.Length ? left[index] : 0;
            var b = index < right.Length ? right[index] : 0;
            if (a != b)
            {
                return a.CompareTo(b);
            }
        }

        // No pre-release beats a pre-release of the same numbers.
        if (PreRelease is null && other.PreRelease is not null)
        {
            return 1;
        }

        if (PreRelease is not null && other.PreRelease is null)
        {
            return -1;
        }

        return string.CompareOrdinal(PreRelease ?? string.Empty, other.PreRelease ?? string.Empty);
    }

    public static bool operator >(SoftwareVersion left, SoftwareVersion right) => left.CompareTo(right) > 0;

    public static bool operator <(SoftwareVersion left, SoftwareVersion right) => left.CompareTo(right) < 0;

    public static bool operator >=(SoftwareVersion left, SoftwareVersion right) => left.CompareTo(right) >= 0;

    public static bool operator <=(SoftwareVersion left, SoftwareVersion right) => left.CompareTo(right) <= 0;

    /// <summary>True when both versions share major.minor and this one is newer (a patch bump).</summary>
    public bool IsPatchLevelUpgradeFrom(SoftwareVersion installed) =>
        this > installed && Major == installed.Major && Minor == installed.Minor;

    /// <summary>True when both share the same major version.</summary>
    public bool IsSameMajor(SoftwareVersion other) => Major == other.Major;

    public override string ToString() => string.Join('.', (_parts ?? []).Select(static part => part.ToString(CultureInfo.InvariantCulture)));

    private int Part(int index) => _parts is not null && index < _parts.Length ? _parts[index] : 0;
}
