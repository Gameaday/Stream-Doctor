namespace StreamDoctor.Core.Model;

/// <summary>Traffic-light state used by the dashboard board.</summary>
public enum HealthState
{
    /// <summary>The module was not scanned, so nothing can be said.</summary>
    Unknown = 0,

    /// <summary>No actionable problems.</summary>
    Green = 1,

    /// <summary>Degraded, or problems that need attention before going live.</summary>
    Yellow = 2,

    /// <summary>Broken right now; going live is not advisable.</summary>
    Red = 3,
}

/// <summary>Per-module rollup rendered as one gauge on the health board.</summary>
public sealed record ModuleHealth(
    ScanModule Module,
    HealthState State,
    int FindingCount,
    Severity HighestSeverity,
    bool WasScanned);

/// <summary>A rule that failed to evaluate; surfaced instead of silently vanishing.</summary>
public sealed record RuleFailure(string RuleId, string Message);

/// <summary>
/// Output of one diagnosis pass: ranked findings, the health score and the
/// per-module board.
/// </summary>
public sealed record DiagnosisReport
{
    /// <summary>Findings ranked by confidence (descending), then severity.</summary>
    public required IReadOnlyList<Finding> Findings { get; init; }

    /// <summary>One entry per known module, including modules that were not scanned.</summary>
    public required IReadOnlyList<ModuleHealth> Modules { get; init; }

    /// <summary>0 (broken) to 100 (healthy). See <c>HealthScore</c> for the formula.</summary>
    public required int HealthScore { get; init; }

    /// <summary>Traffic-light state derived from <see cref="HealthScore"/>.</summary>
    public required HealthState State { get; init; }

    /// <summary>When the diagnosis ran.</summary>
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>Rules that threw; a scan never fails hard because one rule is faulty.</summary>
    public IReadOnlyList<RuleFailure> RuleFailures { get; init; } = [];

    /// <summary>Findings that come with an executable or guided playbook.</summary>
    public int ActionableCount => Findings.Count(static finding => finding.IsActionable);

    /// <summary>The single most likely problem, used for the dashboard headline.</summary>
    public Finding? TopFinding => Findings.Count == 0 ? null : Findings[0];

    /// <summary>An empty report used when a scan could not collect anything.</summary>
    public static DiagnosisReport Empty(DateTimeOffset generatedAt, IReadOnlyList<ModuleHealth>? modules = null) => new()
    {
        Findings = [],
        Modules = modules ?? [],
        HealthScore = 100,
        State = HealthState.Unknown,
        GeneratedAt = generatedAt,
    };
}
