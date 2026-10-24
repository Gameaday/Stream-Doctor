using StreamDoctor.Core.Fixes;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Updates;
using StreamDoctor.Core.Usb;

namespace StreamDoctor.Fixes;

/// <summary>
/// Turns a finding into a concrete, executable playbook and runs it through the
/// guards and journal. This is the layer the UI (and any future automation) calls;
/// it keeps playbook resolution out of the view models.
/// </summary>
public sealed class FixCoordinator
{
    private readonly JournaledFixExecutor _executor;
    private readonly IChangeJournalStore _store;

    public FixCoordinator(IChangeApplier applier, IChangeJournalStore store, FixGuard? guard = null)
    {
        ArgumentNullException.ThrowIfNull(applier);
        ArgumentNullException.ThrowIfNull(store);

        _executor = new JournaledFixExecutor(applier, store, guard);
        _store = store;
    }

    /// <summary>
    /// Resolves the playbook a finding points at: an update playbook for
    /// <c>update:</c> ids, otherwise the static catalog (device-scoped when needed).
    /// Returns null when the target cannot be resolved, never throws for a missing one.
    /// </summary>
    public static FixPlaybook? Resolve(
        Finding finding,
        IReadOnlyList<UsbDevice> devices,
        IReadOnlyList<UpdateCandidate> updates,
        string? configBackupSource = null)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(updates);

        var playbookId = finding.FixPlaybookId;
        if (string.IsNullOrWhiteSpace(playbookId))
        {
            return null;
        }

        if (playbookId.StartsWith(PlaybookIds.UpdatePlaybookPrefix, StringComparison.Ordinal))
        {
            var candidateId = playbookId[PlaybookIds.UpdatePlaybookPrefix.Length..];
            var candidate = updates.FirstOrDefault(update =>
                string.Equals(update.Id, candidateId, StringComparison.OrdinalIgnoreCase));

            return candidate is null ? null : UpdatePlaybooks.Create(candidate, configBackupSource);
        }

        return PlaybookCatalog.Resolve(playbookId, FindDevice(finding, devices));
    }

    /// <summary>Resolves and executes, returning the journaled result.</summary>
    public async Task<FixExecutionResult> ApplyAsync(
        Finding finding,
        FixExecutionOptions options,
        IReadOnlyList<UsbDevice> devices,
        IReadOnlyList<UpdateCandidate> updates,
        string? configBackupSource = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(options);

        var playbook = Resolve(finding, devices, updates, configBackupSource);
        if (playbook is null)
        {
            return new FixExecutionResult
            {
                Outcome = FixOutcome.Blocked,
                BlockCode = "fix.unresolved",
                BlockedReason = "Stream Doctor could not resolve an executable playbook for this finding. Re-run the sweep so the device and update lists are current.",
            };
        }

        var guardContext = new FixGuardOptions { Devices = devices };
        return await _executor
            .ExecuteAsync(playbook, options, guardContext, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<FixExecutionResult> RollbackAsync(
        string playbookId,
        FixExecutionOptions options,
        CancellationToken cancellationToken = default) =>
        _executor.RollbackAsync(playbookId, options, cancellationToken);

    public Task<IReadOnlyList<ChangeJournalEntry>> RecentChangesAsync(
        int limit = 50,
        CancellationToken cancellationToken = default) =>
        _store.GetAsync(playbookId: null, limit, cancellationToken);

    private static UsbDevice? FindDevice(Finding finding, IReadOnlyList<UsbDevice> devices)
    {
        var target = finding.Data.TryGetValue("target", out var value) ? value : null;
        var named = finding.Data.TryGetValue(ScanEventPayload.Device, out var deviceName) ? deviceName : null;

        return devices.FirstOrDefault(device =>
                   !string.IsNullOrEmpty(target) &&
                   string.Equals(device.InstanceId, target, StringComparison.OrdinalIgnoreCase))
               ?? devices.FirstOrDefault(device =>
                   !string.IsNullOrEmpty(named) &&
                   string.Equals(device.Name, named, StringComparison.OrdinalIgnoreCase))
               ?? devices.FirstOrDefault(device =>
                   !string.IsNullOrEmpty(named) &&
                   device.Name.Contains(named, StringComparison.OrdinalIgnoreCase));
    }
}
