using StreamDoctor.Core.Model;
using StreamDoctor.Core.Scanning;
using StreamDoctor.Core.Usb;
using Microsoft.Win32;

namespace StreamDoctor.Windows.Collectors;

/// <summary>Reads per-device USB power-management flags from the registry (no writes).</summary>
public sealed class UsbPowerScanSource : IScanSource
{
    public string Id => "usb-power";

    public ScanModule OwnerModule => ScanModule.UsbSentinel;

    public bool IsOptional => true;

    public Task<ScanSourceResult> CollectAsync(ScanRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.Run(
            () =>
            {
                var topology = Usb.UsbTopologyReader.Read(request.Now);
                cancellationToken.ThrowIfCancellationRequested();

                var snapshots = topology.AllDevices
                    .Where(static device => device.IsPeripheral || device.Class == UsbDeviceClass.Hub)
                    .Select(device => new UsbPowerSnapshot(device, ReadSettings(device)))
                    .ToList();

                return new ScanSourceResult
                {
                    PowerSnapshots = snapshots,
                    PowerPlan = PowerPlanReader.Read(),
                };
            },
            cancellationToken);
    }

    private static UsbPowerSettings ReadSettings(UsbDevice device)
    {
        // These flags live either under the device's Enum key or under its driver key,
        // depending on the driver. Read both; first non-null value per flag wins.
        var enumKey = $@"SYSTEM\CurrentControlSet\Enum\{device.InstanceId}\Device Parameters";
        var driverKey = device.DriverKey is null
            ? null
            : $@"SYSTEM\CurrentControlSet\Control\Class\{device.DriverKey}\Device Parameters";

        return UsbPowerSettings.Merge(ReadKey(enumKey), driverKey is null ? null : ReadKey(driverKey));
    }

    private static UsbPowerSettings? ReadKey(string path)
    {
        using var key = Registry.LocalMachine.OpenSubKey(path);
        return key is null
            ? null
            : new UsbPowerSettings
            {
                SelectiveSuspendEnabled = ReadFlag(key, "SelectiveSuspendEnabled"),
                AllowIdleIrpInD3 = ReadFlag(key, "AllowIdleIrpInD3"),
                EnhancedPowerManagementEnabled = ReadFlag(key, "EnhancedPowerManagementEnabled"),
                SourceKey = path,
            };
    }

    private static bool? ReadFlag(RegistryKey key, string name) => key.GetValue(name) switch
    {
        int value => value != 0,
        long value => value != 0,
        _ => null,
    };
}
