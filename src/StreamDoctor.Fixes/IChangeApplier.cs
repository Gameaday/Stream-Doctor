using StreamDoctor.Core.Fixes;

namespace StreamDoctor.Fixes;

/// <summary>
/// Platform effector for journaled changes. <c>StreamDoctor.Windows</c> implements
/// this with registry, powercfg and SetupAPI; tests implement it in memory.
/// </summary>
public interface IChangeApplier
{
    /// <summary>True when this applier knows how to perform the action.</summary>
    bool CanApply(FixAction action);

    /// <summary>Reads the current value (used when the playbook has no known rollback value).</summary>
    Task<string?> ReadAsync(FixAction action, CancellationToken cancellationToken);

    /// <summary>
    /// Applies the action and returns the value that was actually written (the new
    /// installed version, the backup directory, the activated plan…). The executor
    /// journals that value so rollback has something exact to restore.
    /// </summary>
    Task<string?> ApplyAsync(FixAction action, string? beforeValue, CancellationToken cancellationToken);

    Task RevertAsync(FixAction action, string? beforeValue, CancellationToken cancellationToken);
}

/// <summary>Result of one playbook execution.</summary>
public enum FixOutcome
{
    /// <summary>Every action applied and journaled.</summary>
    Applied,

    /// <summary>Nothing was changed; the plan was validated only.</summary>
    DryRun,

    /// <summary>A guard refused the change.</summary>
    Blocked,

    /// <summary>A step failed and the applied steps were reverted.</summary>
    RolledBack,

    /// <summary>Rollback itself failed; the journal shows what is still applied.</summary>
    Failed,

    /// <summary>There was nothing to do (for example, no applied changes to roll back).</summary>
    NothingToDo,
}

/// <summary>Outcome of a fix attempt, including the journal entries it produced.</summary>
public sealed record FixExecutionResult
{
    public required FixOutcome Outcome { get; init; }

    public string? BlockCode { get; init; }

    public string? BlockedReason { get; init; }

    public string? Error { get; init; }

    public IReadOnlyList<ChangeJournalEntry> Journal { get; init; } = [];

    public bool IsSuccess => Outcome is FixOutcome.Applied or FixOutcome.DryRun;
}
