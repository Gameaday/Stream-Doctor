using StreamDoctor.Core.Model;

namespace StreamDoctor.Core.Windows;

/// <summary>
/// Maps Windows event log entries onto normalized evidence. Only well-known,
/// documented provider/ID pairs are interpreted; everything else is ignored on
/// purpose, because guessing from message text produces false findings.
/// </summary>
public static partial class WindowsEventLogInterpreter
{
    private const int MaxMessageLength = 400;

    /// <summary>Provider fragments that indicate USB host controller / hub drivers.</summary>
    private static readonly string[] UsbProviders = ["USBXHCI", "USBHUB3", "USBHUB", "Microsoft-Windows-USB"];

    public static ScanEvent? Interpret(WindowsEventRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (IsUsbProvider(record.Provider))
        {
            return record.Id switch
            {
                400 => UsbEvent(
                    record,
                    ScanEventCodes.UsbControllerError,
                    Severity.Warning,
                    "The USB controller reported a hardware error.",
                    "controller-error"),
                411 => UsbEvent(
                    record,
                    ScanEventCodes.UsbDeviceReset,
                    Severity.Warning,
                    "Windows reset a USB device, which is what a dropout looks like from the PC side.",
                    "device-reset"),
                _ => null,
            };
        }

        if (IsProvider(record.Provider, "Kernel-PnP") && Contains(record.Message, "surprise removal"))
        {
            return UsbEvent(
                record,
                ScanEventCodes.UsbDeviceRemoved,
                Severity.Warning,
                "A USB device disappeared without being unplugged properly.",
                "surprise-removal");
        }

        if (IsProvider(record.Provider, "Display") && record.Id == 4101)
        {
            return Event(
                record,
                ScanModule.FramePacing,
                ScanEventCodes.GpuDriverReset,
                Severity.Warning,
                "The GPU driver stopped responding and recovered, freezing the stream for a moment.",
                "gpu-reset");
        }

        if (IsProvider(record.Provider, "Application Error") && record.Id == 1000)
        {
            var application = ExtractExecutableName(record.Message);
            return Event(
                record,
                ScanModule.WindowsInterference,
                ScanEventCodes.AppCrashed,
                Severity.Warning,
                application is null
                    ? "An application crashed while you were streaming."
                    : $"{application} crashed while you were streaming.",
                "app-crash",
                application);
        }

        if (IsProvider(record.Provider, "Service Control Manager") &&
            record.Id is 7000 or 7009 or 7031 or 7034)
        {
            return Event(
                record,
                ScanModule.WindowsInterference,
                ScanEventCodes.ServiceFailed,
                Severity.Warning,
                "A Windows service crashed or failed to start.",
                "service-failure");
        }

        if (IsProvider(record.Provider, "Microsoft-Windows-Kernel-Power") && record.Id == 41)
        {
            return Event(
                record,
                ScanModule.WindowsInterference,
                ScanEventCodes.SystemPowerEvent,
                Severity.Critical,
                "Windows logged an unexpected shutdown or power loss.",
                "kernel-power");
        }

        if (IsProvider(record.Provider, "Microsoft-Windows-WindowsUpdateClient") && record.Id is 19 or 20)
        {
            return Event(
                record,
                ScanModule.WindowsInterference,
                ScanEventCodes.WindowsUpdateInstalled,
                Severity.Advisory,
                "Windows installed an update.",
                "windows-update");
        }

        return null;
    }

    /// <summary>Interprets a batch, dropping entries that carry no meaning for v1.</summary>
    public static IReadOnlyList<ScanEvent> InterpretMany(IEnumerable<WindowsEventRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        return records
            .Select(Interpret)
            .Where(static evidence => evidence is not null)
            .Select(static evidence => evidence!)
            .OrderBy(static evidence => evidence.Timestamp)
            .ToList();
    }

    private static ScanEvent UsbEvent(
        WindowsEventRecord record,
        string category,
        Severity severity,
        string message,
        string kind) => Event(record, ScanModule.UsbSentinel, category, severity, message, kind);
}
