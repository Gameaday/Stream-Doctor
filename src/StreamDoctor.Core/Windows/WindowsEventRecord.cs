namespace StreamDoctor.Core.Windows;

/// <summary>
/// A raw Windows event log entry, normalized so the interpretation logic stays
/// testable without a Windows machine. The collector only reads these fields.
/// </summary>
public sealed record WindowsEventRecord
{
    public required DateTimeOffset Timestamp { get; init; }

    public required string LogName { get; init; }

    public required string Provider { get; init; }

    public required int Id { get; init; }

    /// <summary>Standard event level: 1 critical, 2 error, 3 warning, 4 information, 5 verbose.</summary>
    public int? Level { get; init; }

    public string? Message { get; init; }

    public long? RecordId { get; init; }
}
