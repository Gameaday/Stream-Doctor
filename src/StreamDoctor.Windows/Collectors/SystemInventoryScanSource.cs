using System.Globalization;
using System.Runtime.InteropServices;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Scanning;

namespace StreamDoctor.Windows.Collectors;

/// <summary>
/// Collects OS, CPU/GPU, disk space and power-plan facts. Everything here is
/// read-only; WMI failures degrade to notes instead of failing the sweep.
/// </summary>
public sealed partial class SystemInventoryScanSource : IScanSource
{
    public string Id => "system-inventory";

    public ScanModule OwnerModule => ScanModule.DiskRecording;

    public bool IsOptional => true;

    public Task<ScanSourceResult> CollectAsync(ScanRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.Run(
            () =>
            {
                var events = new List<ScanEvent>
                {
                    new()
                    {
                        Timestamp = request.Now,
                        Source = ScanSource.SystemInventory,
                        Module = ScanModule.WindowsInterference,
                        Category = ScanEventCodes.OperatingSystemObserved,
                        Severity = Severity.Info,
                        Message = RuntimeInformation.OSDescription.Trim(),
                        Payload = Payload(ScanEventPayload.Version, Environment.OSVersion.VersionString),
                    },
                };

                AddDisks(request.Now, events, cancellationToken);
                AddHardware(request.Now, events);

                var plan = PowerPlanReader.Read();
                if (plan.Name is not null)
                {
                    events.Add(new ScanEvent
                    {
                        Timestamp = request.Now,
                        Source = ScanSource.SystemInventory,
                        Module = ScanModule.WindowsInterference,
                        Category = ScanEventCodes.PowerPlanObserved,
                        Severity = Severity.Info,
                        Message = $"Active power plan: {plan.Name}.",
                        Payload = Payload(ScanEventPayload.Plan, plan.Name),
                    });
                }

                return new ScanSourceResult
                {
                    Events = events,
                    PowerPlan = plan,
                };
            },
            cancellationToken);
    }

    private static void AddDisks(DateTimeOffset now, List<ScanEvent> events, CancellationToken cancellationToken)
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                {
                    continue;
                }

                var total = drive.TotalSize;
                var free = drive.AvailableFreeSpace;

                events.Add(new ScanEvent
                {
                    Timestamp = now,
                    Source = ScanSource.SystemInventory,
                    Module = ScanModule.DiskRecording,
                    Category = ScanEventCodes.DiskSpaceObserved,
                    Severity = Severity.Info,
                    Message = $"{drive.Name.TrimEnd('\\')} has {free / (1024d * 1024 * 1024):0.#} GB free of {total / (1024d * 1024 * 1024):0.#} GB.",
                    Payload = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [ScanEventPayload.Drive] = drive.Name.TrimEnd('\\'),
                        [ScanEventPayload.FreeBytes] = free.ToString(CultureInfo.InvariantCulture),
                        [ScanEventPayload.TotalBytes] = total.ToString(CultureInfo.InvariantCulture),
                    },
                });
            }
            catch (IOException)
            {
                // A removable or dying disk can fail mid-read; skip it.
            }
            catch (UnauthorizedAccessException)
            {
                // No permission to query the volume; skip it.
            }
        }
    }

    private static Dictionary<string, string> Payload(string key, string value) =>
        new(StringComparer.Ordinal) { [key] = value };
}
