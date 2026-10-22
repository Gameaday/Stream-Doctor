namespace StreamDoctor.Core.Updates;

/// <summary>
/// A range of affected versions, written in the same compact syntax the knowledge
/// base will use: <c>*</c>, <c>1.2.3</c>, <c>1.2.*</c>, <c>1.2.0..1.2.4</c>,
/// <c>&gt;=1.2.0 &lt;1.3.0</c>.
/// </summary>
public sealed record VersionRange
{
    private VersionRange(SoftwareVersion? exact, SoftwareVersion? minimum, SoftwareVersion? maximumExclusive, int[]? prefix)
    {
        Exact = exact;
        Minimum = minimum;
        MaximumExclusive = maximumExclusive;
        Prefix = prefix;
    }

    public SoftwareVersion? Exact { get; }

    public SoftwareVersion? Minimum { get; }

    public SoftwareVersion? MaximumExclusive { get; }

    /// <summary>Numeric prefix for wildcard ranges such as <c>1.2.*</c>.</summary>
    public int[]? Prefix { get; }

    public static VersionRange All { get; } = new(null, null, null, null);

    public static VersionRange Parse(string spec)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spec);

        var text = spec.Trim();

        if (text == "*")
        {
            return All;
        }

        if (text.Contains("..", StringComparison.Ordinal))
        {
            var bounds = text.Split("..", 2, StringSplitOptions.None);
            var minimum = ParseBound(bounds[0], "minimum");
            SoftwareVersion? maximum = bounds.Length > 1 && bounds[1].Length > 0
                ? ParseBound(bounds[1], "maximum")
                : null;
            return new VersionRange(null, minimum, maximum, null);
        }

        if (text.Contains(">=", StringComparison.Ordinal) || text.Contains('<', StringComparison.Ordinal))
        {
            SoftwareVersion? minimumVersion = null;
            SoftwareVersion? maximumVersion = null;

            foreach (var token in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.StartsWith(">=", StringComparison.Ordinal))
                {
                    minimumVersion = ParseBound(token[2..], ">=");
                }
                else if (token.StartsWith('<'))
                {
                    maximumVersion = ParseBound(token[1..], "<");
                }
                else if (token.StartsWith('>'))
                {
                    // Exclusive minimum is not needed by the catalog; reject loudly.
                    throw new FormatException($"Unsupported version token '{token}'.");
                }
                else
                {
                    throw new FormatException($"Unsupported version token '{token}'.");
                }
            }

            return new VersionRange(null, minimumVersion, maximumVersion, null);
        }

        if (text.EndsWith(".*", StringComparison.Ordinal))
        {
            var prefixText = text[..^2];
            var parts = prefixText.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var numbers = new int[parts.Length];

            for (var index = 0; index < parts.Length; index++)
            {
                if (!int.TryParse(parts[index], out numbers[index]))
                {
                    throw new FormatException($"'{spec}' is not a version range.");
                }
            }

            return new VersionRange(null, null, null, numbers);
        }

        return new VersionRange(SoftwareVersion.Parse(text), null, null, null);
    }

    public bool Matches(SoftwareVersion version)
    {
        if (Exact is not null)
        {
            return version.CompareTo(Exact.Value) == 0;
        }

        if (Prefix is not null)
        {
            var parts = version.Parts;
            if (parts.Count < Prefix.Length)
            {
                return false;
            }

            for (var index = 0; index < Prefix.Length; index++)
            {
                if (parts[index] != Prefix[index])
                {
                    return false;
                }
            }

            return true;
        }

        if (Minimum is not null && version < Minimum.Value)
        {
            return false;
        }

        return MaximumExclusive is null || version < MaximumExclusive.Value;
    }

    public bool Matches(string? version) =>
        SoftwareVersion.TryParse(version, out var parsed) && Matches(parsed);

    private static SoftwareVersion ParseBound(string text, string label) =>
        SoftwareVersion.TryParse(text.Trim(), out var version)
            ? version
            : throw new FormatException($"'{text}' is not a valid {label} version.");
}

/// <summary>
/// A version that is known to misbehave. Supplied by the knowledge base; the local
/// copy starts empty because Stream Doctor must not invent advisories.
/// </summary>
public sealed record KnownIssue
{
    public required string Id { get; init; }

    /// <summary>Package or update id this affects (e.g. <c>OBSProject.OBSStudio</c>).</summary>
    public required string PackageId { get; init; }

    public required VersionRange AffectedVersions { get; init; }

    public required string Summary { get; init; }

    public UpdateSeverity Severity { get; init; } = UpdateSeverity.Recommended;

    public string? ReferenceUrl { get; init; }

    public string? SafeVersionHint { get; init; }

    public bool Matches(string? version) => AffectedVersions.Matches(version);
}
