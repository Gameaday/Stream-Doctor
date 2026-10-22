using StreamDoctor.Core.Usb;

namespace StreamDoctor.Windows.Usb;

public static partial class UsbTopologyReader
{
    private static UsbDevice ToUsbDevice(DeviceNode node, string controllerId, string? parentHubId, bool isHub)
    {
        var deviceClass = isHub
            ? UsbDeviceClass.Hub
            : UsbDeviceClassifier.Classify(new UsbClassificationInput
            {
                Name = node.DisplayName,
                DeviceClass = node.ClassName,
                Service = node.Service,
                CompatibleIds = node.CompatibleIds,
                HardwareIds = node.HardwareIds,
            });

        return new UsbDevice
        {
            InstanceId = node.InstanceId,
            Name = node.DisplayName,
            Class = deviceClass,
            Manufacturer = node.Manufacturer,
            DriverVersion = ReadDriverVersion(node.DriverKey),
            DriverKey = node.DriverKey,
            Service = node.Service,
            ControllerInstanceId = controllerId,
            HubInstanceId = parentHubId,
            PortNumber = node.PortNumber,
            Speed = ReadSpeed(node.InstanceId),
            LocationInfo = node.LocationInfo,
            CompatibleIds = node.CompatibleIds,
            ProblemCode = node.ProblemCode,
        };
    }

    private static string? ReadDriverVersion(string? driverKey)
    {
        if (string.IsNullOrEmpty(driverKey))
        {
            return null;
        }

        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
            $@"SYSTEM\CurrentControlSet\Control\Class\{driverKey}");

        return key?.GetValue("DriverVersion") as string;
    }

    private sealed class ControllerBuilder(string instanceId)
    {
        public string InstanceId { get; } = instanceId;

        public DeviceNode? Node { get; set; }

        public List<UsbDevice> Devices { get; } = [];

        public UsbController Build()
        {
            var name = Node?.DisplayName ?? InstanceId;

            return new UsbController
            {
                InstanceId = InstanceId,
                Name = name,
                Kind = UsbControllerKindDetector.FromName(name),
                Manufacturer = Node?.Manufacturer,
                DriverVersion = ReadDriverVersion(Node?.DriverKey),
                Devices = Devices,
            };
        }
    }
}
