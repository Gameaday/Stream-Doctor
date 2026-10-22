using System.Globalization;
using Microsoft.Data.Sqlite;
using StreamDoctor.Core.Fixes;

namespace StreamDoctor.Fixes;

/// <summary>
/// SQLite-backed change journal. Design doc §3 and §10: the journal is the app's
/// memory of every mutation, and it must survive a crash mid-fix.
/// </summary>
public sealed partial class SqliteChangeJournalStore : IChangeJournalStore, IAsyncDisposable
{
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS change_journal (
            id TEXT NOT NULL PRIMARY KEY,
            playbook_id TEXT NOT NULL,
            action_kind TEXT NOT NULL,
            target TEXT NOT NULL,
            before_value TEXT NULL,
            after_value TEXT NULL,
            applied_at TEXT NOT NULL,
            state TEXT NOT NULL,
            failure_reason TEXT NULL,
            rolled_back_at TEXT NULL,
            device_instance_id TEXT NULL,
            machine_name TEXT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_change_journal_playbook
            ON change_journal (playbook_id, applied_at DESC);
        """;

    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public SqliteChangeJournalStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        };

        _connection = new SqliteConnection(builder.ToString());
    }

    /// <summary>Creates a private in-memory journal, mostly for tests and previews.</summary>
    public static SqliteChangeJournalStore InMemory() => new(":memory:");

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var command = _connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;" + Schema;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AppendAsync(ChangeJournalEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = """
                INSERT INTO change_journal
                    (id, playbook_id, action_kind, target, before_value, after_value, applied_at,
                     state, failure_reason, rolled_back_at, device_instance_id, machine_name)
                VALUES
                    ($id, $playbook, $kind, $target, $before, $after, $applied,
                     $state, $failure, $rolledBack, $device, $machine);
                """;

            Bind(command, entry);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateAsync(ChangeJournalEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = """
                UPDATE change_journal SET
                    before_value = $before,
                    after_value = $after,
                    state = $state,
                    failure_reason = $failure,
                    rolled_back_at = $rolledBack
                WHERE id = $id;
                """;

            Bind(command, entry);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
