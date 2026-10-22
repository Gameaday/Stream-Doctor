using StreamDoctor.Core.Model;
using StreamDoctor.Core.Obs;
using StreamDoctor.Core.Usb;

namespace StreamDoctor.Core.Scanning;

/// <summary>What a sweep is asked to do. Pre-flight and post-flight share this shape.</summary>
public sealed record ScanRequest
{
    public required DateTimeOffset Now { get; init; }

    /// <summary>How far back to read event logs and OBS logs for a pre-flight sweep.</summary>
    public TimeSpan Lookback { get; init; } = TimeSpan.FromDays(7);

    /// <summary>Per-source budget; a hung collector must not hang the sweep.</summary>
    public TimeSpan SourceTimeout { get; init; } = TimeSpan.FromSeconds(90);

    /// <summary>Optional analysis window (the stream session for post-flight).</summary>
    public DateTimeOffset? WindowStart { get; init; }

    public DateTimeOffset? WindowEnd { get; init; }

    /// <summary>When false, OBS log parsing is skipped (e.g. games-closed test bench).</summary>
    public bool IncludeObsLogs { get; init; } = true;

    public static ScanRequest PreFlight(DateTimeOffset now) => new() { Now = now };

    public static ScanRequest PostFlight(DateTimeOffset windowStart, DateTimeOffset windowEnd) => new()
    {
        Now = windowEnd,
        Lookback = windowEnd - windowStart + TimeSpan.FromMinutes(10),
        WindowStart = windowStart,
        WindowEnd = windowEnd,
    };
}

/// <summary>Everything one collector produced, normalized for the diagnosis engine.</summary>
public sealed record ScanSourceResult
{
    public IReadOnlyList<ScanEvent> Events { get; init; } = [];

    public UsbTopology? Topology { get; init; }

    public UsbTopology? PreviousTopology { get; init; }

    public IReadOnlyList<UsbPowerSnapshot> PowerSnapshots { get; init; } = [];

    public PowerPlanInfo? PowerPlan { get; init; }

    public ObsLogDocument? Obs { get; init; }

    /// <summary>Human-readable note for the UI (e.g. "OBS was not installed").</summary>
    public string? Note { get; init; }

    public static ScanSourceResult Empty { get; } = new();
}

/// <summary>
/// A read-only collector. Implementations live in <c>StreamDoctor.Windows</c> (or a
/// test bench later); they must never mutate the machine.
/// </summary>
public interface IScanSource
{
    /// <summary>Stable identifier used in failure reports and logs.</summary>
    string Id { get; }

    /// <summary>Module this collector feeds, used to mark it as scanned.</summary>
    ScanModule OwnerModule { get; }

    /// <summary>Optional sources may fail without failing the sweep.</summary>
    bool IsOptional { get; }

    Task<ScanSourceResult> CollectAsync(ScanRequest request, CancellationToken cancellationToken);
}

/// <summary>A collector that failed or timed out; surfaced instead of silently skipped.</summary>
public sealed record ScanSourceFailure(string SourceId, string Message, bool TimedOut, bool Optional);

/// <summary>Result of one sweep: raw evidence plus the diagnosis and any source failures.</summary>
public sealed record ScanReport
{
    public required DiagnosisReport Diagnosis { get; init; }

    public required IReadOnlyList<ScanEvent> Events { get; init; }

    public required IReadOnlyCollection<ScanModule> ScannedModules { get; init; }

    public required IReadOnlyList<ScanSourceFailure> Failures { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }

    public ObsLogDocument? Obs { get; init; }

    public UsbTopology? Topology { get; init; }

    /// <summary>Power flags read during the sweep, so post-flight can re-run the audit.</summary>
    public IReadOnlyList<UsbPowerSnapshot> PowerSnapshots { get; init; } = [];

    /// <summary>Active power plan read during the sweep.</summary>
    public PowerPlanInfo? PowerPlan { get; init; }

    public TimeSpan Duration => CompletedAt - StartedAt;

    /// <summary>True when every required collector produced data.</summary>
    public bool IsComplete => Failures.All(static failure => failure.Optional);
}
