using System.Globalization;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Scanning;
using StreamDoctor.Core.Usb;

namespace StreamDoctor.Windows.Collectors;

/// <summary>
/// Enumerates the USB tree and emits one evidence event per controller and device.
/// Read-only; design doc §9 shared infrastructure.
/// </summary>
public sealed class UsbTopologyScanSource : IScanSource
{
    public string Id => "usb-topology";

    public ScanModule OwnerModule => ScanModule.UsbSentinel;

    public bool IsOptional => false;

    public Task<ScanSourceResult> CollectAsync(ScanRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.Run(
            () =>
            {
                var topology = Usb.UsbTopologyReader.Read(request.Now);
                cancellationToken.ThrowIfCancellationRequested();

                var events = UsbTopologyEvents.FromTopology(topology).ToList();

                foreach (var device in topology.AllDevices.Where(static device => device.ProblemCode is not null))
                {
                    var problemCode = device.ProblemCode!.Value.ToString(CultureInfo.InvariantCulture);

                    events.Add(new ScanEvent
                    {
                        Timestamp = topology.CapturedAt,
                        Source = ScanSource.UsbTopology,
                        Module = ScanModule.UsbSentinel,
                        Category = ScanEventCodes.UsbDeviceProblem,
                        Severity = Severity.Warning,
                        Message = $"{device.Name} reports a Windows device problem (code {problemCode}), so it may not work at all.",
                        Payload = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            [ScanEventPayload.Device] = device.Name,
                            [ScanEventPayload.DeviceInstanceId] = device.InstanceId,
                            [ScanEventPayload.DeviceClass] = device.Class.ToString(),
                            ["problemCode"] = problemCode,
                        },
                        Sequence = events.Count + 1,
                    });
                }

                return new ScanSourceResult
                {
                    Events = events,
                    Topology = topology,
                };
            },
            cancellationToken);
    }
}
