using StreamDoctor.Core.Model;

namespace StreamDoctor.Core.Usb;

/// <summary>
/// Converts topology snapshots into evidence events. Keeping this in Core means
/// every collector (Windows today, a test bench later) emits identical evidence
/// shapes, so rules and golden-file tests stay stable.
/// </summary>
public static class UsbTopologyEvents
{
    public static IReadOnlyList<ScanEvent> FromTopology(UsbTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);

        var events = new List<ScanEvent>();
        var sequence = 0L;

        foreach (var controller in topology.Controllers)
        {
            events.Add(new ScanEvent
            {
                Timestamp = topology.CapturedAt,
                Source = ScanSource.UsbTopology,
                Module = ScanModule.UsbSentinel,
                Category = ScanEventCodes.UsbControllerObserved,
                Severity = Severity.Info,
                Message = $"{controller.Name} hosts {controller.Devices.Count} device(s).",
                Sequence = ++sequence,
                Payload = Payload((
                    ScanEventPayload.Controller, controller.Name),
                    (ScanEventPayload.ControllerKind, controller.Kind.ToString()),
                    (ScanEventPayload.Count, controller.Devices.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))),
            });

            foreach (var device in controller.Devices)
            {
                events.Add(ForDevice(device, topology.CapturedAt, ++sequence));
            }
        }

        return events;
    }

    /// <summary>Builds the single-device evidence event used by topology-based rules.</summary>
    public static ScanEvent ForDevice(UsbDevice device, DateTimeOffset timestamp, long sequence = 0)
    {
        ArgumentNullException.ThrowIfNull(device);

        var pairs = new List<KeyValuePair<string, string>>(8)
        {
            new(ScanEventPayload.DeviceInstanceId, device.InstanceId),
            new(ScanEventPayload.Device, device.Name),
            new(ScanEventPayload.DeviceClass, device.Class.ToString()),
            new(ScanEventPayload.Speed, device.Speed.ToString()),
        };

        if (!string.IsNullOrEmpty(device.ControllerInstanceId))
        {
            pairs.Add(new(ScanEventPayload.Controller, device.ControllerInstanceId));
        }

        if (device.PortNumber is not null)
        {
            pairs.Add(new(ScanEventPayload.Port, device.PortNumber.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        if (!string.IsNullOrEmpty(device.HubInstanceId))
        {
            pairs.Add(new(ScanEventPayload.Hub, device.HubInstanceId));
        }

        return new ScanEvent
        {
            Timestamp = timestamp,
            Source = ScanSource.UsbTopology,
            Module = ScanModule.UsbSentinel,
            Category = ScanEventCodes.UsbDeviceObserved,
            Severity = Severity.Info,
            Message = $"{device.Name} is attached as {device.Class} on controller {DeviceName(device.ControllerInstanceId)} port {device.PortNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?"}.",
            Sequence = sequence,
            Payload = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
                pairs.GroupBy(static pair => pair.Key, StringComparer.Ordinal)
                     .ToDictionary(static group => group.Key, static group => group.Last().Value, StringComparer.Ordinal)),
        };
    }

    private static string DeviceName(string? instanceId) => string.IsNullOrEmpty(instanceId) ? "unknown" : instanceId;

    private static System.Collections.ObjectModel.ReadOnlyDictionary<string, string> Payload(params (string Key, string Value)[] pairs) =>
        new(pairs.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal));
}
