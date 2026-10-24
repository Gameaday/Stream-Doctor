using StreamDoctor.Core.Fixes;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Usb;
using StreamDoctor.Fixes;

namespace StreamDoctor.Tests;

public sealed class FixCoordinatorExecutionTests
{
    [Fact]
    public async Task ApplyAsync_ResolvesAndJournalsTheChange()
    {
        var mic = TestData.Device("Microphone", UsbDeviceClass.Microphone);
        var finding = Finding(PlaybookIds.UsbDisableSelectiveSuspend, mic.InstanceId, mic.Name);
        var coordinator = new FixCoordinator(new FakeChangeApplier(), new InMemoryChangeJournalStore());

        var result = await coordinator.ApplyAsync(
            finding,
            new FixExecutionOptions { UserApproved = true, IsElevated = true },
            [mic],
            [],
            cancellationToken: CancellationToken.None);

        Assert.Equal(FixOutcome.Applied, result.Outcome);
        Assert.Single(result.Journal);
        Assert.Single(await coordinator.RecentChangesAsync());
    }

    [Fact]
    public async Task ApplyAsync_RefusesToRunWhileLive()
    {
        var mic = TestData.Device("Microphone", UsbDeviceClass.Microphone);
        var finding = Finding(PlaybookIds.UsbDisableSelectiveSuspend, mic.InstanceId, mic.Name);
        var coordinator = new FixCoordinator(new FakeChangeApplier(), new InMemoryChangeJournalStore());

        var result = await coordinator.ApplyAsync(
            finding,
            new FixExecutionOptions { IsLive = true, UserApproved = true, IsElevated = true },
            [mic],
            [],
            cancellationToken: CancellationToken.None);

        Assert.Equal(FixOutcome.Blocked, result.Outcome);
        Assert.Equal(FixGuard.CodeLive, result.BlockCode);
    }

    [Fact]
    public async Task ApplyAsync_UnresolvableFinding_IsBlockedWithGuidance()
    {
        var finding = Finding(PlaybookIds.UpdatePlaybook("gone.package"), "gone.package", deviceName: null);
        var coordinator = new FixCoordinator(new FakeChangeApplier(), new InMemoryChangeJournalStore());

        var result = await coordinator.ApplyAsync(
            finding,
            new FixExecutionOptions { UserApproved = true, IsElevated = true },
            [],
            [],
            cancellationToken: CancellationToken.None);

        Assert.Equal(FixOutcome.Blocked, result.Outcome);
        Assert.Equal("fix.unresolved", result.BlockCode);
        Assert.Contains("Re-run the sweep", result.BlockedReason!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RollbackAsync_RevertsThePlaybookThatWasApplied()
    {
        var mic = TestData.Device("Microphone", UsbDeviceClass.Microphone);
        var finding = Finding(PlaybookIds.UsbDisableSelectiveSuspend, mic.InstanceId, mic.Name);
        var applier = new FakeChangeApplier();
        var coordinator = new FixCoordinator(applier, new InMemoryChangeJournalStore());

        await coordinator.ApplyAsync(
            finding,
            new FixExecutionOptions { UserApproved = true, IsElevated = true },
            [mic],
            [],
            cancellationToken: CancellationToken.None);

        var rollback = await coordinator.RollbackAsync(
            PlaybookIds.UsbDisableSelectiveSuspend,
            new FixExecutionOptions { UserApproved = true },
            CancellationToken.None);

        Assert.Equal(FixOutcome.RolledBack, rollback.Outcome);
        Assert.Single(applier.RevertedTargets);
    }

    private static Finding Finding(string playbookId, string target, string? deviceName) => new()
    {
        HypothesisId = "test.finding",
        Module = ScanModule.UsbSentinel,
        Title = "Test finding",
        Explanation = "Test explanation",
        Severity = Severity.Warning,
        Confidence = 0.9,
        Evidence = [],
        FixPlaybookId = playbookId,
        Data = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["target"] = target,
            [ScanEventPayload.Device] = deviceName ?? string.Empty,
        },
    };
}
