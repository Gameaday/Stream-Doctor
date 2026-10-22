using StreamDoctor.Core.Usb;

namespace StreamDoctor.Fixes;

/// <summary>Whether a device may be power-cycled by the test bench.</summary>
public sealed record ReEnumerationDecision(bool Allowed, string Code, string Reason)
{
    public static ReEnumerationDecision Allow { get; } = new(true, "ok", "The device is safe to re-enumerate.");
}

/// <summary>
/// Device reset denylist (design doc §9, Test 2): never power-cycle the things the
/// user needs to recover, and never reset a topology node that everything hangs off.
/// </summary>
public static class DeviceReEnumerationGuard
{
    public const string CodeInputDevice = "device.input";
    public const string CodeTopologyNode = "device.topology";
    public const string CodeUnknown = "device.unknown";

    public static ReEnumerationDecision Evaluate(UsbDevice device, IReadOnlyCollection<UsbDevice> allDevices)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(allDevices);

        if (device.Class is UsbDeviceClass.Keyboard or UsbDeviceClass.Mouse or UsbDeviceClass.GameController)
        {
            return new ReEnumerationDecision(false, CodeInputDevice,
                $"'{device.Name}' is an input device. Resetting it can leave you without a keyboard or mouse, so the test refuses.");
        }

        if (device.Class is UsbDeviceClass.Hub or UsbDeviceClass.HostController ||
            device.InstanceId.Contains("ROOT_HUB", StringComparison.OrdinalIgnoreCase) ||
            device.InstanceId.StartsWith("ROOT\\", StringComparison.OrdinalIgnoreCase))
        {
            return new ReEnumerationDecision(false, CodeTopologyNode,
                $"'{device.Name}' is part of the USB topology. Resetting a hub or controller can disconnect everything below it.");
        }

        if (device.Name.Contains("keyboard", StringComparison.OrdinalIgnoreCase) ||
            device.Name.Contains("mouse", StringComparison.OrdinalIgnoreCase))
        {
            return new ReEnumerationDecision(false, CodeInputDevice,
                $"'{device.Name}' looks like an input device. Reset it manually if you really need to.");
        }

        if (device.Class == UsbDeviceClass.Unknown)
        {
            return new ReEnumerationDecision(false, CodeUnknown,
                $"'{device.Name}' could not be classified, so Stream Doctor will not reset it.");
        }

        return ReEnumerationDecision.Allow;
    }
}
