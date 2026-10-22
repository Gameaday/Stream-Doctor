using StreamDoctor.Core.Fixes;

namespace StreamDoctor.Fixes;

public sealed partial class JournaledFixExecutor
{
    /// <summary>Reverts every entry this executor wrote for a playbook, newest first.</summary>
    public async Task<FixExecutionResult> RollbackAsync(
        string playbookId,
        FixExecutionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playbookId);
        ArgumentNullException.ThrowIfNull(options);

        if (options.IsLive)
        {
            return new FixExecutionResult
            {
                Outcome = FixOutcome.Blocked,
                BlockCode = FixGuard.CodeLive,
                BlockedReason = "Rollback also waits until you are offline; a mid-stream registry change is worse than the bug.",
            };
        }

        var entries = await _store.GetAsync(playbookId, limit: 500, cancellationToken).ConfigureAwait(false);
        var applied = entries
            .Where(static entry => entry.State == ChangeState.Applied)
            .OrderByDescending(static entry => entry.AppliedAt)
            .ToList();

        if (applied.Count == 0)
        {
            return new FixExecutionResult { Outcome = FixOutcome.NothingToDo };
        }

        var journal = new List<ChangeJournalEntry>(applied.Count);
        var failures = new List<string>();

        foreach (var entry in applied)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var action = new FixAction
            {
                Kind = entry.ActionKind,
                Target = entry.Target,
                Description = "Rollback of " + entry.Target,
                DeviceInstanceId = entry.DeviceInstanceId,
            };

            try
            {
                await _applier.RevertAsync(action, entry.BeforeValue, cancellationToken).ConfigureAwait(false);

                var reverted = entry with
                {
                    State = ChangeState.RolledBack,
                    RolledBackAt = _time.GetUtcNow(),
                };

                await _store.UpdateAsync(reverted, cancellationToken).ConfigureAwait(false);
                journal.Add(reverted);
            }
#pragma warning disable CA1031 // Keep going: one failed revert must not strand the rest.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                failures.Add($"{entry.Target}: {ex.Message}");
                journal.Add(entry with { FailureReason = ex.Message });
            }
        }

        return new FixExecutionResult
        {
            Outcome = failures.Count == 0 ? FixOutcome.RolledBack : FixOutcome.Failed,
            Error = failures.Count == 0 ? null : "Rollback incomplete: " + string.Join("; ", failures),
            Journal = journal,
        };
    }

    private async Task<FixExecutionResult> RollbackAppliedAsync(
        FixPlaybook playbook,
        List<ChangeJournalEntry> journal,
        string error,
        CancellationToken cancellationToken)
    {
        var failures = new List<string>();

        for (var index = journal.Count - 1; index >= 0; index--)
        {
            var entry = journal[index];
            if (entry.State != ChangeState.Applied)
            {
                continue;
            }

            var action = playbook.Actions.FirstOrDefault(candidate =>
                candidate.Kind == entry.ActionKind &&
                string.Equals(candidate.Target, entry.Target, StringComparison.Ordinal));

            if (action is null)
            {
                failures.Add($"{entry.Target}: the playbook no longer contains this action.");
                continue;
            }

            try
            {
                await _applier.RevertAsync(action, entry.BeforeValue, cancellationToken).ConfigureAwait(false);

                var reverted = entry with
                {
                    State = ChangeState.RolledBack,
                    RolledBackAt = _time.GetUtcNow(),
                };

                await _store.UpdateAsync(reverted, cancellationToken).ConfigureAwait(false);
                journal[index] = reverted;
            }
#pragma warning disable CA1031 // Report and continue rolling back the rest.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                failures.Add($"{entry.Target}: {ex.Message}");
            }
        }

        return new FixExecutionResult
        {
            Outcome = failures.Count == 0 ? FixOutcome.RolledBack : FixOutcome.Failed,
            Error = failures.Count == 0 ? error : $"{error} Rollback incomplete: {string.Join("; ", failures)}",
            Journal = journal,
        };
    }
}
