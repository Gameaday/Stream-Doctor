using StreamDoctor.Core.Fixes;
using StreamDoctor.Fixes;

namespace StreamDoctor.Tests;

public sealed class SqliteChangeJournalStoreTests
{
    [Fact]
    public async Task AppendAndGet_RoundTripsEveryField()
    {
        await using var store = SqliteChangeJournalStore.InMemory();
        var entry = Entry("playbook-1", "target-a", ChangeState.Applied);

        await store.AppendAsync(entry);
        var loaded = Assert.Single(await store.GetAsync());

        Assert.Equal(entry.Id, loaded.Id);
        Assert.Equal(entry.PlaybookId, loaded.PlaybookId);
        Assert.Equal(entry.ActionKind, loaded.ActionKind);
        Assert.Equal(entry.Target, loaded.Target);
        Assert.Equal(entry.BeforeValue, loaded.BeforeValue);
        Assert.Equal(entry.AfterValue, loaded.AfterValue);
        Assert.Equal(entry.AppliedAt, loaded.AppliedAt);
        Assert.Equal(ChangeState.Applied, loaded.State);
        Assert.Equal(entry.DeviceInstanceId, loaded.DeviceInstanceId);
    }

    [Fact]
    public async Task Update_ChangesStateAndRollbackTimestamp()
    {
        await using var store = SqliteChangeJournalStore.InMemory();
        var entry = Entry("playbook-1", "target-a", ChangeState.Pending);
        await store.AppendAsync(entry);

        var reverted = entry with
        {
            State = ChangeState.RolledBack,
            RolledBackAt = entry.AppliedAt.AddMinutes(5),
        };

        await store.UpdateAsync(reverted);
        var loaded = await store.FindAsync(entry.Id);

        Assert.NotNull(loaded);
        Assert.Equal(ChangeState.RolledBack, loaded.State);
        Assert.Equal(reverted.RolledBackAt, loaded.RolledBackAt);
    }

    [Fact]
    public async Task Get_FiltersByPlaybookAndOrdersNewestFirst()
    {
        await using var store = SqliteChangeJournalStore.InMemory();
        await store.AppendAsync(Entry("playbook-1", "old", ChangeState.Applied, TestData.At(10)));
        await store.AppendAsync(Entry("playbook-2", "other", ChangeState.Applied, TestData.At(11)));
        await store.AppendAsync(Entry("playbook-1", "new", ChangeState.Applied, TestData.At(12)));

        var entries = await store.GetAsync("playbook-1");

        Assert.Equal(2, entries.Count);
        Assert.Equal("new", entries[0].Target);
        Assert.Equal("old", entries[1].Target);
    }

    [Fact]
    public async Task Get_RespectsTheLimit()
    {
        await using var store = SqliteChangeJournalStore.InMemory();
        for (var index = 0; index < 5; index++)
        {
            await store.AppendAsync(Entry("playbook-1", $"target-{index}", ChangeState.Applied, TestData.At(10).AddMinutes(index)));
        }

        Assert.Equal(2, (await store.GetAsync(limit: 2)).Count);
    }

    [Fact]
    public async Task Find_WithUnknownId_ReturnsNull()
    {
        await using var store = SqliteChangeJournalStore.InMemory();

        Assert.Null(await store.FindAsync("does-not-exist"));
    }

    [Fact]
    public async Task JournalSurvivesAReopen()
    {
        var path = Path.Combine(Path.GetTempPath(), $"streamdoctor-{Guid.NewGuid():N}.db");
        try
        {
            await using (var store = new SqliteChangeJournalStore(path))
            {
                await store.AppendAsync(Entry("playbook-1", "target-a", ChangeState.Applied));
            }

            await using (var reopened = new SqliteChangeJournalStore(path))
            {
                Assert.Single(await reopened.GetAsync());
            }
        }
        finally
        {
            DeleteIfExists(path);
            DeleteIfExists(path + "-wal");
            DeleteIfExists(path + "-shm");
        }
    }

    private static ChangeJournalEntry Entry(
        string playbookId,
        string target,
        ChangeState state,
        DateTimeOffset? appliedAt = null) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        PlaybookId = playbookId,
        ActionKind = FixActionKind.RegistryValue,
        Target = target,
        BeforeValue = "1",
        AfterValue = "0",
        AppliedAt = appliedAt ?? TestData.At(12),
        State = state,
        DeviceInstanceId = "USB\\VID_0000&PID_0001",
        MachineName = "STREAM-PC",
    };

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
