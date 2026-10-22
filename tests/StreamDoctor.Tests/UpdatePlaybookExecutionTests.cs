using StreamDoctor.Core.Fixes;
using StreamDoctor.Core.Updates;
using StreamDoctor.Fixes;

namespace StreamDoctor.Tests;

public sealed class UpdatePlaybookExecutionTests
{
    [Fact]
    public void Guard_RefusesAutoFixForADriverBecauseItNeedsApproval()
    {
        var playbook = UpdatePlaybooks.Create(Driver())!;

        var block = new FixGuard().Evaluate(
            playbook,
            new FixExecutionOptions
            {
                Mode = FixMode.Auto,
                Confidence = 1,
                IsElevated = true,
                UserApproved = false,
            });

        Assert.NotNull(block);
        Assert.Equal(FixGuard.CodeAutoUnsupported, block.Code);
    }

    [Fact]
    public void Guard_AllowsAPatchLevelAppUpdateInAutoMode()
    {
        var playbook = UpdatePlaybooks.Create(App("31.0.1", "31.0.2"), null)!;

        var block = new FixGuard().Evaluate(
            playbook,
            new FixExecutionOptions
            {
                Mode = FixMode.Auto,
                Confidence = 1,
                AutoFixThreshold = 0.85,
                IsElevated = true,
            });

        Assert.Null(block);
    }

    [Fact]
    public void Guard_RefusesAnUpdateWhileLive()
    {
        var playbook = UpdatePlaybooks.Create(App("31.0.1", "31.0.2"), null)!;

        var block = new FixGuard().Evaluate(
            playbook,
            new FixExecutionOptions { IsLive = true, UserApproved = true, IsElevated = true });

        Assert.NotNull(block);
        Assert.Equal(FixGuard.CodeLive, block.Code);
    }

    [Fact]
    public void Guard_RefusesAnUpdateWithoutElevation()
    {
        var playbook = UpdatePlaybooks.Create(App("31.0.1", "31.0.2"), null)!;

        var block = new FixGuard().Evaluate(
            playbook,
            new FixExecutionOptions { UserApproved = true, IsElevated = false });

        Assert.NotNull(block);
        Assert.Equal(FixGuard.CodeElevation, block.Code);
    }

    [Fact]
    public async Task Executor_JournalsTheCreatedBackupAndRestoresItOnRollback()
    {
        var applier = new FakeChangeApplier
        {
            AppliedValueFactory = action =>
                action.Kind == FixActionKind.ConfigBackup ? "/backups/obs-2026-03-15" : action.DesiredValue,
        };
        var store = new InMemoryChangeJournalStore();
        var executor = new JournaledFixExecutor(applier, store);
        var playbook = UpdatePlaybooks.Create(App("31.0.1", "31.0.2"), "/config/obs")!;

        var result = await executor.ExecuteAsync(
            playbook,
            new FixExecutionOptions { UserApproved = true, IsElevated = true },
            cancellationToken: CancellationToken.None);

        Assert.Equal(FixOutcome.Applied, result.Outcome);
        Assert.Equal("/backups/obs-2026-03-15", result.Journal[0].AfterValue);
        Assert.Equal("31.0.2", result.Journal[1].AfterValue);

        var rollback = await executor.RollbackAsync(
            playbook.Id,
            new FixExecutionOptions { UserApproved = true },
            CancellationToken.None);

        Assert.Equal(FixOutcome.RolledBack, rollback.Outcome);
        Assert.Equal(["OBSProject.OBSStudio", "/config/obs"], applier.RevertedTargets);
        Assert.Equal("31.0.2", applier.RevertedDesiredValues[0]);
        Assert.Equal("/backups/obs-2026-03-15", applier.RevertedDesiredValues[1]);
    }

    [Fact]
    public async Task Executor_RollsBackTheBackupWhenTheUpdateFails()
    {
        var applier = new FakeChangeApplier
        {
            AppliedValueFactory = action =>
                action.Kind == FixActionKind.ConfigBackup ? "/backups/obs" : action.DesiredValue,
        };
        applier.FailOnApply.Add("OBSProject.OBSStudio");
        var executor = new JournaledFixExecutor(applier, new InMemoryChangeJournalStore());
        var playbook = UpdatePlaybooks.Create(App("31.0.1", "31.0.2"), "/config/obs")!;

        var result = await executor.ExecuteAsync(
            playbook,
            new FixExecutionOptions { UserApproved = true, IsElevated = true },
            cancellationToken: CancellationToken.None);

        Assert.Equal(FixOutcome.RolledBack, result.Outcome);
        Assert.Equal(["/config/obs"], applier.RevertedTargets);
        Assert.Equal("/backups/obs", applier.RevertedDesiredValues[0]);
    }

    private static UpdateCandidate App(string installed, string available) => new()
    {
        Id = "OBSProject.OBSStudio",
        Name = "OBS Studio",
        Kind = UpdateKind.Application,
        Source = UpdateSource.Winget,
        InstalledVersion = installed,
        AvailableVersion = available,
    };

    private static UpdateCandidate Driver() => new()
    {
        Id = "driver-1",
        Name = "Intel USB controller",
        Kind = UpdateKind.Driver,
        Source = UpdateSource.WindowsUpdate,
        InstalledVersion = "10.0.0",
        AvailableVersion = "10.0.1",
    };
}
