using StreamDoctor.Core.Fixes;

namespace StreamDoctor.Fixes;

/// <summary>Thread-safe in-memory journal used by tests, dry runs and preview builds.</summary>
public sealed class InMemoryChangeJournalStore : IChangeJournalStore
{
    private readonly List<ChangeJournalEntry> _entries = [];
    private readonly Lock _gate = new();

    public Task AppendAsync(ChangeJournalEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_gate)
        {
            _entries.RemoveAll(existing => string.Equals(existing.Id, entry.Id, StringComparison.Ordinal));
            _entries.Add(entry);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(ChangeJournalEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_gate)
        {
            var index = _entries.FindIndex(existing => string.Equals(existing.Id, entry.Id, StringComparison.Ordinal));
            if (index >= 0)
            {
                _entries[index] = entry;
            }
            else
            {
                _entries.Add(entry);
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ChangeJournalEntry>> GetAsync(
        string? playbookId = null,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<ChangeJournalEntry> result = _entries
                .Where(entry => playbookId is null || string.Equals(entry.PlaybookId, playbookId, StringComparison.Ordinal))
                .OrderByDescending(entry => entry.AppliedAt)
                .Take(Math.Max(limit, 1))
                .ToList();

            return Task.FromResult(result);
        }
    }

    public Task<ChangeJournalEntry?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_entries.FirstOrDefault(entry =>
                string.Equals(entry.Id, id, StringComparison.Ordinal)));
        }
    }
}
