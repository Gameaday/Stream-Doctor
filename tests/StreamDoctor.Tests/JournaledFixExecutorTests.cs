using StreamDoctor.Core.Fixes;
using StreamDoctor.Fixes;

namespace StreamDoctor.Tests;

public sealed class JournaledFixExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_AppliesBothStepsAndJournalsTheBeforeValues()
    {
        var applier = new FakeChangeApplier();
        var store = new InMemoryChangeJournalStore();
        var executor = new JournaledFixExecutor(applier, store);

        var result = await executor.ExecuteAsync(
            FixTestData.TwoStepPlaybook(),
            FixTestData.Approved(),
            cancellationToken: CancellationToken.None);

        Assert.Equal(FixOutcome.Applied, result.Outcome);
        Assert.Equal(["a", "b"], applier.AppliedTargets);
        Assert.Equal("0", applier.Value("a"));
        Assert.Equal("1", applier.Value("b"));
        Assert.Equal(2, result.Journal.Count);
        Assert.All(result.Journal, entry => Assert.Equal(ChangeState.Applied, entry.State));

        var persisted = await store.GetAsync("test.two-step", cancellationToken: CancellationToken.None);
        Assert.Equal(2, persisted.Count);
        Assert.All(persisted, entry => Assert.Equal(ChangeState.Applied, entry.State));
    }

    [Fact]
    public async Task ExecuteAsync_RollsBackEarlierStepsWhenALaterStepFails()
    {
        var applier = new FakeChangeApplier();
        applier.FailOnApply.Add("b");
        var executor = new JournaledFixExecutor(applier, new InMemoryChangeJournalStore());

        var result = await executor.ExecuteAsync(
            FixTestData.TwoStepPlaybook(),
            FixTestData.Approved(),
            cancellationToken: CancellationToken.None);

        Assert.Equal(FixOutcome.RolledBack, result.Outcome);
        Assert.Contains("access denied", result.Error, StringComparison.Ordinal);
        Assert.Equal(["a"], applier.AppliedTargets);
        Assert.Equal(["a"], applier.RevertedTargets);
        Assert.Null(applier.Value("a"));

        var failed = Assert.Single(result.Journal, entry => entry.State == ChangeState.Failed);
        Assert.Equal("b", failed.Target);
        var rolledBack = Assert.Single(result.Journal, entry => entry.State == ChangeState.RolledBack);
        Assert.Equal("a", rolledBack.Target);
        Assert.NotNull(rolledBack.RolledBackAt);
    }

    [Fact]
    public async Task ExecuteAsync_DryRunChangesNothingAndWritesNoJournal()
    {
        var applier = new FakeChangeApplier();
        var store = new InMemoryChangeJournalStore();
        var executor = new JournaledFixExecutor(applier, store);

        var result = await executor.ExecuteAsync(
            FixTestData.TwoStepPlaybook(),
            FixTestData.Approved() with { DryRun = true },
            cancellationToken: CancellationToken.None);

        Assert.Equal(FixOutcome.DryRun, result.Outcome);
        Assert.Empty(applier.AppliedTargets);
        Assert.All(result.Journal, entry => Assert.Equal(ChangeState.Skipped, entry.State));
        Assert.Empty(await store.GetAsync(cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_WhileLive_IsBlockedWithoutTouchingTheMachine()
    {
        var applier = new FakeChangeApplier();
        var executor = new JournaledFixExecutor(applier, new InMemoryChangeJournalStore());

        var result = await executor.ExecuteAsync(
            FixTestData.TwoStepPlaybook(),
            FixTestData.Approved() with { IsLive = true },
            cancellationToken: CancellationToken.None);

        Assert.Equal(FixOutcome.Blocked, result.Outcome);
        Assert.Equal(FixGuard.CodeLive, result.BlockCode);
        Assert.Empty(applier.AppliedTargets);
    }

    [Fact]
    public async Task ExecuteAsync_WithAnUnsupportedAction_RollsBackWhatAlreadyApplied()
    {
        var applier = new FakeChangeApplier();
        applier.Unsupported.Add("b");
        var executor = new JournaledFixExecutor(applier, new InMemoryChangeJournalStore());

        var result = await executor.ExecuteAsync(
            FixTestData.TwoStepPlaybook(),
            FixTestData.Approved(),
            cancellationToken: CancellationToken.None);

        Assert.Equal(FixOutcome.RolledBack, result.Outcome);
        Assert.Contains("No effector", result.Error, StringComparison.Ordinal);
        Assert.Equal(["a"], applier.RevertedTargets);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheCurrentValueCannotBeRead_FailsSafely()
    {
        var applier = new FakeChangeApplier { FailReads = true };
        var executor = new JournaledFixExecutor(applier, new InMemoryChangeJournalStore());

        var result = await executor.ExecuteAsync(
            FixTestData.TwoStepPlaybook(),
            FixTestData.Approved(),
            cancellationToken: CancellationToken.None);

        Assert.Equal(FixOutcome.RolledBack, result.Outcome);
        Assert.Contains("Could not read", result.Error, StringComparison.Ordinal);
        Assert.Empty(applier.AppliedTargets);
        Assert.Empty(result.Journal);
    }
}
