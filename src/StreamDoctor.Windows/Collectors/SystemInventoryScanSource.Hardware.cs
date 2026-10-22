using System.Management;
using StreamDoctor.Core.Model;

namespace StreamDoctor.Windows.Collectors;

public sealed partial class SystemInventoryScanSource
{
    private static void AddHardware(DateTimeOffset now, List<ScanEvent> events)
    {
        TryQuery(
            "SELECT Name FROM Win32_Processor",
            now,
            events,
            item =>
            {
                var name = (item["Name"] as string)?.Trim();
                return name is null
                    ? null
                    : new ScanEvent
                    {
                        Timestamp = now,
                        Source = ScanSource.SystemInventory,
                        Module = ScanModule.Thermals,
                        Category = ScanEventCodes.HardwareObserved,
                        Severity = Severity.Info,
                        Message = $"CPU: {name}.",
                        Payload = Payload("component", "cpu"),
                    };
            });

        TryQuery(
            "SELECT Name, DriverVersion FROM Win32_VideoController",
            now,
            events,
            item =>
            {
                var name = (item["Name"] as string)?.Trim();
                if (name is null)
                {
                    return null;
                }

                var driver = item["DriverVersion"] as string ?? "unknown";

                return new ScanEvent
                {
                    Timestamp = now,
                    Source = ScanSource.SystemInventory,
                    Module = ScanModule.FramePacing,
                    Category = ScanEventCodes.DriverObserved,
                    Severity = Severity.Info,
                    Message = $"GPU: {name} (driver {driver}).",
                    Payload = Payload(ScanEventPayload.Version, driver),
                };
            });
    }

    private static void TryQuery(
        string query,
        DateTimeOffset now,
        List<ScanEvent> events,
        Func<ManagementObject, ScanEvent?> projector)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            using var results = searcher.Get();

            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    var evidence = projector(item);
                    if (evidence is not null)
                    {
                        events.Add(evidence);
                    }
                }
            }
        }
        catch (ManagementException)
        {
            // WMI is optional: a broken repository must not fail the sweep.
        }
    }
}
