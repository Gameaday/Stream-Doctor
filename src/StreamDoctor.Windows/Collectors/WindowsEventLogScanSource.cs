using System.Diagnostics.Eventing.Reader;
using System.Xml;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Scanning;
using StreamDoctor.Core.Windows;

namespace StreamDoctor.Windows.Collectors;

/// <summary>
/// Reads the System and Application event logs and normalizes the entries that
/// matter for diagnosis. Read-only and non-elevated (no Security log access).
/// </summary>
public sealed class WindowsEventLogScanSource : IScanSource
{
    private const int MaxEventsPerLog = 4000;

    private static readonly string[] LogNames = ["System", "Application"];

    public string Id => "windows-event-log";

    public ScanModule OwnerModule => ScanModule.WindowsInterference;

    public bool IsOptional => true;

    public Task<ScanSourceResult> CollectAsync(ScanRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.Run(
            () =>
            {
                var since = request.WindowStart ?? request.Now - request.Lookback;
                var records = new List<WindowsEventRecord>();

                foreach (var logName in LogNames)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    records.AddRange(ReadLog(logName, since, request.Now, cancellationToken));
                }

                return new ScanSourceResult
                {
                    Events = WindowsEventLogInterpreter.InterpretMany(records),
                    Note = $"Read {records.Count} event log entries since {since:u}.",
                };
            },
            cancellationToken);
    }

    private static IEnumerable<WindowsEventRecord> ReadLog(
        string logName,
        DateTimeOffset since,
        DateTimeOffset until,
        CancellationToken cancellationToken)
    {
        var xpath = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"*[System[TimeCreated[@SystemTime>='{XmlConvert.ToString(since.UtcDateTime, XmlDateTimeSerializationMode.Utc)}' and @SystemTime<='{XmlConvert.ToString(until.UtcDateTime, XmlDateTimeSerializationMode.Utc)}']]]");

        var query = new EventLogQuery(logName, PathType.LogName, xpath) { ReverseDirection = true };
        using var reader = new EventLogReader(query);

        var count = 0;
        for (var record = reader.ReadEvent(); record is not null && count < MaxEventsPerLog; record = reader.ReadEvent())
        {
            using (record)
            {
                cancellationToken.ThrowIfCancellationRequested();
                count++;

                yield return new WindowsEventRecord
                {
                    Timestamp = record.TimeCreated is { } created
                        ? new DateTimeOffset(created.ToUniversalTime())
                        : since,
                    LogName = record.LogName ?? logName,
                    Provider = record.ProviderName ?? string.Empty,
                    Id = record.Id,
                    Level = record.Level,
                    Message = ReadMessage(record),
                    RecordId = record.RecordId,
                };
            }
        }
    }

    private static string? ReadMessage(EventRecord record)
    {
        try
        {
            return record.FormatDescription();
        }
        catch (EventLogException)
        {
            // Missing provider metadata is common for third-party drivers.
            return null;
        }
    }
}
