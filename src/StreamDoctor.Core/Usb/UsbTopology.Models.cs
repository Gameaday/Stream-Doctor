using System.Collections.ObjectModel;

namespace StreamDoctor.Core.Usb;

/// <summary>A host controller and the devices enumerated beneath it.</summary>
public sealed record UsbController
{
    public required string InstanceId { get; init; }

    public required string Name { get; init; }

    public UsbControllerKind Kind { get; init; } = UsbControllerKind.Unknown;

    public string? Manufacturer { get; init; }

    public string? DriverVersion { get; init; }

    public IReadOnlyList<UsbDevice> Devices { get; init; } = [];

    /// <summary>Devices excluding hubs, i.e. the things that actually consume bandwidth.</summary>
    public IReadOnlyList<UsbDevice> Peripherals =>
        Devices.Where(static device => device.IsPeripheral).ToList();

    /// <summary>Total relative bandwidth demand on this controller.</summary>
    public double LoadUnits => Peripherals.Sum(static device => device.BandwidthWeight);
}

/// <summary>A snapshot of the machine's USB tree.</summary>
public sealed record UsbTopology
{
    public required DateTimeOffset CapturedAt { get; init; }

    public IReadOnlyList<UsbController> Controllers { get; init; } = [];

    public IEnumerable<UsbDevice> AllDevices => Controllers.SelectMany(static controller => controller.Devices);

    public IReadOnlyList<UsbDevice> Peripherals =>
        AllDevices.Where(static device => device.IsPeripheral).ToList();

    public int DeviceCount => Controllers.Sum(static controller => controller.Devices.Count);

    public UsbController? FindController(string instanceId) =>
        Controllers.FirstOrDefault(controller =>
            string.Equals(controller.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase));

    /// <summary>Finds a device by instance ID, else by (case-insensitive) friendly name.</summary>
    public UsbDevice? FindDevice(string instanceIdOrName)
    {
        var device = AllDevices.FirstOrDefault(candidate =>
            string.Equals(candidate.InstanceId, instanceIdOrName, StringComparison.OrdinalIgnoreCase));

        return device ?? AllDevices.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, instanceIdOrName, StringComparison.OrdinalIgnoreCase));
    }

    public static UsbTopology Empty(DateTimeOffset capturedAt) => new()
    {
        CapturedAt = capturedAt,
        Controllers = new ReadOnlyCollection<UsbController>([]),
    };
}

/// <summary>Infers a controller generation from its Windows description.</summary>
public static class UsbControllerKindDetector
{
    /// <summary>
    /// Detects USB2/USB3/USB4 from the controller name. Names are localized on some
    /// OEM builds, so the unknown bucket is expected and handled gracefully.
    /// </summary>
    public static UsbControllerKind FromName(string? controllerName)
    {
        var name = controllerName ?? string.Empty;

        if (name.Contains("USB4", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Thunderbolt", StringComparison.OrdinalIgnoreCase))
        {
            return UsbControllerKind.Usb4;
        }

        if (name.Contains("3.2", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("3.1", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("3.0", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("USB 3", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("xHCI", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("eXtensible", StringComparison.OrdinalIgnoreCase))
        {
            return UsbControllerKind.Usb3;
        }

        if (name.Contains("USB 2", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Enhanced", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("EHCI", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Open Host", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("OHCI", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("UHCI", StringComparison.OrdinalIgnoreCase))
        {
            return UsbControllerKind.Usb2;
        }

        return UsbControllerKind.Unknown;
    }

    /// <summary>
    /// Estimated bandwidth budget in the same arbitrary units as
    /// <see cref="UsbDeviceClassifier.BandwidthWeight"/>.
    /// </summary>
    public static double BudgetUnits(this UsbControllerKind kind) => kind switch
    {
        UsbControllerKind.Usb2 => 4.0,
        UsbControllerKind.Usb3 => 12.0,
        UsbControllerKind.Usb4 => 24.0,
        _ => 8.0,
    };
}
