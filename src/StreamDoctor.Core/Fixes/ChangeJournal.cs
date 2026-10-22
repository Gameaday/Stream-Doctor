namespace StreamDoctor.Core.Fixes;

/// <summary>Lifecycle of one journaled change.</summary>
public enum ChangeState
{
    /// <summary>Recorded before the write; a crash here still leaves a recoverable record.</summary>
    Pending = 0,

    /// <summary>The write succeeded; the before value is recoverable.</summary>
    Applied,

    /// <summary>Reverted to the recorded before value.</summary>
    RolledBack,

    /// <summary>The write failed; nothing was changed for this entry.</summary>
    Failed,

    /// <summary>Planned but intentionally not executed (dry run).</summary>
    Skipped,
}

/// <summary>
/// One reversible change: what was written, what it was before, and whether it was
/// rolled back. Design doc §3: "every mutation is recorded with before/after values
/// so rollback is exact and instantaneous".
/// </summary>
public sealed record ChangeJournalEntry
{
    public required string Id { get; init; }

    public required string PlaybookId { get; init; }

    public required FixActionKind ActionKind { get; init; }

    public required string Target { get; init; }

    /// <summary>Value before the change; null when the value did not exist.</summary>
    public string? BeforeValue { get; init; }

    /// <summary>Value written by the fix.</summary>
    public string? AfterValue { get; init; }

    public required DateTimeOffset AppliedAt { get; init; }

    public ChangeState State { get; init; } = ChangeState.Pending;

    public string? FailureReason { get; init; }

    public DateTimeOffset? RolledBackAt { get; init; }

    /// <summary>Device this change applies to, when device-scoped.</summary>
    public string? DeviceInstanceId { get; init; }

    public string? MachineName { get; init; }
}

/// <summary>Persistence for the change journal. Implementations live in <c>StreamDoctor.Fixes</c>.</summary>
public interface IChangeJournalStore
{
    Task AppendAsync(ChangeJournalEntry entry, CancellationToken cancellationToken = default);

    Task UpdateAsync(ChangeJournalEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Entries for one playbook (or all), newest first.</summary>
    Task<IReadOnlyList<ChangeJournalEntry>> GetAsync(
        string? playbookId = null,
        int limit = 200,
        CancellationToken cancellationToken = default);

    Task<ChangeJournalEntry?> FindAsync(string id, CancellationToken cancellationToken = default);
}
