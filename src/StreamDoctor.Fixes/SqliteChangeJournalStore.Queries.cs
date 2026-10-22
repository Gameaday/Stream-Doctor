using System.Globalization;
using Microsoft.Data.Sqlite;
using StreamDoctor.Core.Fixes;

namespace StreamDoctor.Fixes;

public sealed partial class SqliteChangeJournalStore
{
    public async Task<IReadOnlyList<ChangeJournalEntry>> GetAsync(
        string? playbookId = null,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = playbookId is null
                ? "SELECT * FROM change_journal ORDER BY applied_at DESC LIMIT $limit;"
                : "SELECT * FROM change_journal WHERE playbook_id = $playbook ORDER BY applied_at DESC LIMIT $limit;";

            command.Parameters.AddWithValue("$limit", Math.Max(limit, 1));
            if (playbookId is not null)
            {
                command.Parameters.AddWithValue("$playbook", playbookId);
            }

            var results = new List<ChangeJournalEntry>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(Read(reader));
            }

            return results;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ChangeJournalEntry?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = "SELECT * FROM change_journal WHERE id = $id LIMIT 1;";
            command.Parameters.AddWithValue("$id", id);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(reader) : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private static void Bind(SqliteCommand command, ChangeJournalEntry entry)
    {
        command.Parameters.AddWithValue("$id", entry.Id);
        command.Parameters.AddWithValue("$playbook", entry.PlaybookId);
        command.Parameters.AddWithValue("$kind", entry.ActionKind.ToString());
        command.Parameters.AddWithValue("$target", entry.Target);
        command.Parameters.AddWithValue("$before", (object?)entry.BeforeValue ?? DBNull.Value);
        command.Parameters.AddWithValue("$after", (object?)entry.AfterValue ?? DBNull.Value);
        command.Parameters.AddWithValue("$applied", entry.AppliedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$state", entry.State.ToString());
        command.Parameters.AddWithValue("$failure", (object?)entry.FailureReason ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$rolledBack",
            entry.RolledBackAt?.ToString("O", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$device", (object?)entry.DeviceInstanceId ?? DBNull.Value);
        command.Parameters.AddWithValue("$machine", (object?)entry.MachineName ?? DBNull.Value);
    }

    private static ChangeJournalEntry Read(SqliteDataReader reader)
    {
        static string? NullableString(SqliteDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        return new ChangeJournalEntry
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            PlaybookId = reader.GetString(reader.GetOrdinal("playbook_id")),
            ActionKind = Enum.Parse<FixActionKind>(reader.GetString(reader.GetOrdinal("action_kind"))),
            Target = reader.GetString(reader.GetOrdinal("target")),
            BeforeValue = NullableString(reader, "before_value"),
            AfterValue = NullableString(reader, "after_value"),
            AppliedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("applied_at")), CultureInfo.InvariantCulture),
            State = Enum.Parse<ChangeState>(reader.GetString(reader.GetOrdinal("state"))),
            FailureReason = NullableString(reader, "failure_reason"),
            RolledBackAt = NullableString(reader, "rolled_back_at") is { } rolledBack
                ? DateTimeOffset.Parse(rolledBack, CultureInfo.InvariantCulture)
                : null,
            DeviceInstanceId = NullableString(reader, "device_instance_id"),
            MachineName = NullableString(reader, "machine_name"),
        };
    }
}
