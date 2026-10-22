using System.Collections.ObjectModel;
using System.Globalization;

namespace StreamDoctor.Core.Usb;

/// <summary>Negotiated link speed of a device, as far as the registry reports it.</summary>
public enum UsbConnectionSpeed
{
    Unknown = 0,
    Low,
    Full,
    High,
    Super,
    SuperPlus,
}

/// <summary>Controller generation, used to estimate available bandwidth.</summary>
public enum UsbControllerKind
{
    Unknown = 0,
    Usb2,
    Usb3,
    Usb4,
}

/// <summary>
/// Where a device physically sits: controller, then hub, then port. The guided
/// hot-plug test (design doc §9, Test 3) upgrades these into human labels such as
/// "rear port 3".
/// </summary>
public sealed record UsbAddress(string? ControllerInstanceId, string? HubInstanceId, int? PortNumber)
{
    public override string ToString() =>
        (ControllerInstanceId ?? "unknown-controller") + "/" +
        (HubInstanceId ?? "-") + "/" +
        (PortNumber is null ? "-" : PortNumber.Value.ToString(CultureInfo.InvariantCulture));
}

/// <summary>A USB device (or hub) attached to a controller.</summary>
public sealed record UsbDevice
{
    public required string InstanceId { get; init; }

    public required string Name { get; init; }

    public UsbDeviceClass Class { get; init; } = UsbDeviceClass.Unknown;

    public string? Manufacturer { get; init; }

    public string? DriverVersion { get; init; }

    public string? DriverKey { get; init; }

    public string? Service { get; init; }

    /// <summary>Instance ID of the host controller this device ultimately hangs off.</summary>
    public string? ControllerInstanceId { get; init; }

    /// <summary>Instance ID of the immediate parent hub, when the parent is a hub.</summary>
    public string? HubInstanceId { get; init; }

    /// <summary>Port number on <see cref="HubInstanceId"/>, when known.</summary>
    public int? PortNumber { get; init; }

    public UsbConnectionSpeed Speed { get; init; } = UsbConnectionSpeed.Unknown;

    public bool IsPresent { get; init; } = true;

    /// <summary>Windows location information, e.g. "Port_#0003.Hub_#0005".</summary>
    public string? LocationInfo { get; init; }

    public IReadOnlyList<string> CompatibleIds { get; init; } = [];

    /// <summary>Relative bandwidth demand, see <see cref="UsbDeviceClassifier.BandwidthWeight"/>.</summary>
    public double BandwidthWeight => Class.BandwidthWeight();

    public UsbAddress Address => new(ControllerInstanceId, HubInstanceId, PortNumber);

    /// <summary>True when the device is a peripheral rather than a hub or controller.</summary>
    public bool IsPeripheral => Class is not (UsbDeviceClass.Hub or UsbDeviceClass.HostController);
}
