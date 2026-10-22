using System.Globalization;
using StreamDoctor.Core.Model;

namespace StreamDoctor.Core.Windows;

public static partial class WindowsEventLogInterpreter
{
    private static ScanEvent Event(
        WindowsEventRecord record,
        ScanModule module,
        string category,
        Severity severity,
        string message,
        string kind,
        string? application = null) => new()
    {
        Timestamp = record.Timestamp,
        Source = ScanSource.WindowsEventLog,
        Module = module,
        Category = category,
        Severity = severity,
        Message = message,
        Payload = Payload(record, kind, application),
    };

    private static Dictionary<string, string> Payload(
        WindowsEventRecord record,
        string kind,
        string? application)
    {
        var payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ScanEventPayload.Provider] = record.Provider,
            [ScanEventPayload.EventId] = record.Id.ToString(CultureInfo.InvariantCulture),
            [ScanEventPayload.LogName] = record.LogName,
            ["kind"] = kind,
        };

        if (application is not null)
        {
            payload[ScanEventPayload.Device] = application;
        }

        if (record.RecordId is not null)
        {
            payload["windows.recordId"] = record.RecordId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(record.Message))
        {
            var message = record.Message.Trim();
            payload["windows.message"] = message.Length <= MaxMessageLength
                ? message
                : message[..MaxMessageLength];
        }

        return payload;
    }

    private static bool IsUsbProvider(string provider) =>
        UsbProviders.Any(candidate => provider.Contains(candidate, StringComparison.OrdinalIgnoreCase));

    private static bool IsProvider(string provider, string expected) =>
        provider.Contains(expected, StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string? value, string needle) =>
        value?.Contains(needle, StringComparison.OrdinalIgnoreCase) == true;

    private static string? ExtractExecutableName(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var marker = message.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (marker < 0)
        {
            return null;
        }

        var candidate = message[..(marker + 4)].Trim();
        var separator = candidate.LastIndexOfAny(['\\', '/', ' ', '"']);
        return separator >= 0 ? candidate[(separator + 1)..] : candidate;
    }
}
