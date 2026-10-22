using StreamDoctor.Core.Fixes;
using StreamDoctor.Fixes;

namespace StreamDoctor.Tests;

public sealed class JournaledFixExecutorRollbackTests
{
    [Fact]
    public async Task RollbackAsync_RevertsEverythingThatWasApplied()
    {
        var applier = new FakeChangeApplier();
        var store = new InMemoryChangeJournalStore();
        var executor = new JournaledFixExecutor(applier, store);

        await executor.ExecuteAsync(FixTestData.TwoStepPlaybook(), FixTestData.Approved(), cancellationToken: CancellationToken.None);
        var rollback = await executor.RollbackAsync("test.two-step", FixTestData.Approved(), CancellationToken.None);

        Assert.Equal(FixOutcome.RolledBack, rollback.Outcome);
        Assert.Equal(2, applier.RevertedTargets.Count);
        Assert.Null(applier.Value("a"));
        Assert.Null(applier.Value("b"));
        Assert.All(rollback.Journal, entry => Assert.Equal(ChangeState.RolledBack, entry.State));

        var persisted = await store.GetAsync("test.two-step", cancellationToken: CancellationToken.None);
        Assert.All(persisted, entry => Assert.Equal(ChangeState.RolledBack, entry.State));
    }

    [Fact]
    public async Task RollbackAsync_WithNothingApplied_ReportsNothingToDo()
    {
        var executor = new JournaledFixExecutor(new FakeChangeApplier(), new InMemoryChangeJournalStore());

        var result = await executor.RollbackAsync("test.two-step", FixTestData.Approved(), CancellationToken.None);

        Assert.Equal(FixOutcome.NothingToDo, result.Outcome);
        Assert.Empty(result.Journal);
    }

    [Fact]
    public async Task RollbackAsync_WhileLive_IsBlocked()
    {
        var executor = new JournaledFixExecutor(new FakeChangeApplier(), new InMemoryChangeJournalStore());

        var result = await executor.RollbackAsync(
            "test.two-step",
            FixTestData.Approved() with { IsLive = true },
            CancellationToken.None);

        Assert.Equal(FixOutcome.Blocked, result.Outcome);
        Assert.Equal(FixGuard.CodeLive, result.BlockCode);
    }

    [Fact]
    public async Task RollbackAsync_ReconstructsTheActionFromTheJournal()
    {
        var applier = new FakeChangeApplier();
        var store = new InMemoryChangeJournalStore();
        var executor = new JournaledFixExecutor(applier, store);

        await executor.ExecuteAsync(FixTestData.TwoStepPlaybook(), FixTestData.Approved(), cancellationToken: CancellationToken.None);
        await executor.RollbackAsync("test.two-step", FixTestData.Approved(), CancellationToken.None);

        // Reverting in reverse order is what keeps dependent changes safe.
        Assert.Equal(["b", "a"], applier.RevertedTargets);
    }
}
