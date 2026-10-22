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
        var instanceKey = $@"SYSTEM\CurrentControlSet\Enum\{device.InstanceId}\Device Parameters";

        using var key = Registry.LocalMachine.OpenSubKey(instanceKey);
        if (key is null)
        {
            return new UsbPowerSettings { SourceKey = instanceKey };
        }

        return new UsbPowerSettings
        {
            SelectiveSuspendEnabled = ReadFlag(key, "SelectiveSuspendEnabled"),
            AllowIdleIrpInD3 = ReadFlag(key, "AllowIdleIrpInD3"),
            EnhancedPowerManagementEnabled = ReadFlag(key, "EnhancedPowerManagementEnabled"),
            SourceKey = instanceKey,
        };
    }

    private static bool? ReadFlag(RegistryKey key, string name) => key.GetValue(name) switch
    {
        int value => value != 0,
        long value => value != 0,
        _ => null,
    };
}
