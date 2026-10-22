using System.Globalization;
using StreamDoctor.Core.Diagnosis;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.PostFlight;

namespace StreamDoctor.Tests;

/// <summary>Evidence builders shared by the rule tests.</summary>
internal static class TestEvents
{
    public static DiagnosisReport Analyze(DiagnosisContext context) =>
        new DiagnosisEngine(DefaultRuleSet.Create()).Analyze(context, TestData.At(23));

    public static ScanEvent Disconnect(DateTimeOffset timestamp, string device) => new()
    {
        Timestamp = timestamp,
        Source = ScanSource.ObsLog,
        Module = ScanModule.CaptureCard,
        Category = ScanEventCodes.ObsSourceDisconnected,
        Severity = Severity.Warning,
        Message = $"OBS lost {device}",
        Payload = new Dictionary<string, string>(StringComparer.Ordinal) { [ScanEventPayload.Device] = device },
    };

    public static ScanEvent UsbReset(DateTimeOffset timestamp) => new()
    {
        Timestamp = timestamp,
        Source = ScanSource.WindowsEventLog,
        Module = ScanModule.UsbSentinel,
        Category = ScanEventCodes.UsbDeviceReset,
        Severity = Severity.Warning,
        Message = "USB controller reset a device.",
    };

    public static ScanEvent Disk(string drive, int freeGb, int totalGb) => new()
    {
        Timestamp = TestData.At(12),
        Source = ScanSource.SystemInventory,
        Module = ScanModule.DiskRecording,
        Category = ScanEventCodes.DiskSpaceObserved,
        Severity = Severity.Info,
        Payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ScanEventPayload.Drive] = drive,
            [ScanEventPayload.FreeBytes] = (freeGb * 1024L * 1024 * 1024).ToString(CultureInfo.InvariantCulture),
            [ScanEventPayload.TotalBytes] = (totalGb * 1024L * 1024 * 1024).ToString(CultureInfo.InvariantCulture),
        },
    };

    public static ScanEvent WindowsEvent(DateTimeOffset timestamp, string category) => new()
    {
        Timestamp = timestamp,
        Source = ScanSource.WindowsEventLog,
        Module = ScanModule.WindowsInterference,
        Category = category,
        Severity = Severity.Warning,
        Message = "Windows did something.",
    };
}
