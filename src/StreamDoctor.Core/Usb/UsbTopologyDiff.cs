namespace StreamDoctor.Core.Usb;

/// <summary>How the USB tree changed between two scans.</summary>
public enum UsbTopologyChangeKind
{
    Appeared,
    Disappeared,
    MovedToAnotherController,
    MovedToAnotherPort,
}

/// <summary>One device-level change between two topology snapshots.</summary>
public sealed record UsbTopologyChange(
    UsbTopologyChangeKind Kind,
    UsbDevice Device,
    string? FromController,
    string? ToController,
    int? FromPort,
    int? ToPort);

/// <summary>
/// Diffs two topology snapshots. A device that silently changed controllers
/// between scans is itself a finding (design doc §9, shared infrastructure).
/// </summary>
public static class UsbTopologyDiff
{
    public static IReadOnlyList<UsbTopologyChange> Compare(UsbTopology before, UsbTopology after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var previous = before.AllDevices
            .GroupBy(static device => device.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);

        var current = after.AllDevices
            .GroupBy(static device => device.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);

        var changes = new List<UsbTopologyChange>();

        foreach (var (instanceId, device) in previous)
        {
            if (!current.TryGetValue(instanceId, out var now))
            {
                changes.Add(new UsbTopologyChange(
                    UsbTopologyChangeKind.Disappeared,
                    device,
                    device.ControllerInstanceId,
                    null,
                    device.PortNumber,
                    null));
                continue;
            }

            if (!Same(device.ControllerInstanceId, now.ControllerInstanceId))
            {
                changes.Add(new UsbTopologyChange(
                    UsbTopologyChangeKind.MovedToAnotherController,
                    now,
                    device.ControllerInstanceId,
                    now.ControllerInstanceId,
                    device.PortNumber,
                    now.PortNumber));
                continue;
            }

            if (device.PortNumber != now.PortNumber || !Same(device.HubInstanceId, now.HubInstanceId))
            {
                changes.Add(new UsbTopologyChange(
                    UsbTopologyChangeKind.MovedToAnotherPort,
                    now,
                    now.ControllerInstanceId,
                    now.ControllerInstanceId,
                    device.PortNumber,
                    now.PortNumber));
            }
        }

        foreach (var (instanceId, device) in current)
        {
            if (!previous.ContainsKey(instanceId))
            {
                changes.Add(new UsbTopologyChange(
                    UsbTopologyChangeKind.Appeared,
                    device,
                    null,
                    device.ControllerInstanceId,
                    null,
                    device.PortNumber));
            }
        }

        return changes
            .OrderBy(static change => change.Device.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static change => change.Kind)
            .ToList();
    }

    private static bool Same(string? left, string? right) =>
        string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
}
