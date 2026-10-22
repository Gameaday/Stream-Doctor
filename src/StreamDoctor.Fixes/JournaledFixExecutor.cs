using StreamDoctor.Core.Fixes;

namespace StreamDoctor.Fixes;

/// <summary>
/// Applies a playbook step by step, journaling every change before it happens.
/// A failure halfway through reverts the steps that already applied, so the machine
/// never ends up in a half-fixed state (design doc §3 and §8).
/// </summary>
public sealed partial class JournaledFixExecutor
{
    private readonly IChangeApplier _applier;
    private readonly IChangeJournalStore _store;
    private readonly FixGuard _guard;
    private readonly TimeProvider _time;

    public JournaledFixExecutor(
        IChangeApplier applier,
        IChangeJournalStore store,
        FixGuard? guard = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(applier);
        ArgumentNullException.ThrowIfNull(store);

        _applier = applier;
        _store = store;
        _guard = guard ?? new FixGuard();
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task<FixExecutionResult> ExecuteAsync(
        FixPlaybook playbook,
        FixExecutionOptions options,
        FixGuardOptions? guardContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(playbook);
        ArgumentNullException.ThrowIfNull(options);

        var block = _guard.Evaluate(playbook, options, guardContext);
        if (block is not null)
        {
            return new FixExecutionResult
            {
                Outcome = FixOutcome.Blocked,
                BlockCode = block.Code,
                BlockedReason = block.Reason,
            };
        }

        var journal = new List<ChangeJournalEntry>(playbook.Actions.Count);

        foreach (var action in playbook.Actions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_applier.CanApply(action))
            {
                return await RollbackAppliedAsync(
                    playbook,
                    journal,
                    $"No effector on this machine can perform {action.Kind} on '{action.Target}'.",
                    cancellationToken).ConfigureAwait(false);
            }

            string? before;
            try
            {
                before = action.RollbackValue ?? await _applier.ReadAsync(action, cancellationToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Effectors throw platform-specific exceptions; all mean "cannot fix".
            catch (Exception ex)
#pragma warning restore CA1031
            {
                return await RollbackAppliedAsync(
                    playbook,
                    journal,
                    $"Could not read the current value of '{action.Target}': {ex.Message}",
                    cancellationToken).ConfigureAwait(false);
            }

            var entry = new ChangeJournalEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                PlaybookId = playbook.Id,
                ActionKind = action.Kind,
                Target = action.Target,
                BeforeValue = before,
                AfterValue = action.DesiredValue,
                AppliedAt = _time.GetUtcNow(),
                State = options.DryRun ? ChangeState.Skipped : ChangeState.Pending,
                DeviceInstanceId = action.DeviceInstanceId,
                MachineName = Environment.MachineName,
            };

            journal.Add(entry);

            if (options.DryRun)
            {
                continue;
            }

            await _store.AppendAsync(entry, cancellationToken).ConfigureAwait(false);

            try
            {
                await _applier.ApplyAsync(action, before, cancellationToken).ConfigureAwait(false);

                entry = entry with { State = ChangeState.Applied };
                await _store.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
                journal[^1] = entry;
            }
#pragma warning disable CA1031 // Same as above: record the failure, then roll back.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                entry = entry with { State = ChangeState.Failed, FailureReason = ex.Message };
                await _store.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
                journal[^1] = entry;

                return await RollbackAppliedAsync(
                    playbook,
                    journal,
                    $"'{action.Description}' failed: {ex.Message}",
                    cancellationToken).ConfigureAwait(false);
            }
        }

        return new FixExecutionResult
        {
            Outcome = options.DryRun ? FixOutcome.DryRun : FixOutcome.Applied,
            Journal = journal,
        };
    }
}
