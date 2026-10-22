using Microsoft.Win32;
using StreamDoctor.Core.Usb;

namespace StreamDoctor.Windows.Usb;

/// <summary>
/// Enumerates the USB tree from controller to hub to port using CfgMgr32.
/// Read-only: this never changes device state.
/// </summary>
public static partial class UsbTopologyReader
{
    private const int MaxDepth = 12;
    private const int MaxNodes = 4000;

    /// <summary>Walks the device tree and returns every USB controller with its devices.</summary>
    public static UsbTopology Read(DateTimeOffset capturedAt)
    {
        if (!CfgMgr32.TryGetRoot(out var root))
        {
            return UsbTopology.Empty(capturedAt);
        }

        var controllers = new Dictionary<string, ControllerBuilder>(StringComparer.OrdinalIgnoreCase);
        var budget = MaxNodes;

        Walk(root, depth: 0, controllerId: null, hubId: null, controllers, ref budget);

        var built = controllers.Values
            .Select(static builder => builder.Build())
            .OrderBy(static controller => controller.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new UsbTopology
        {
            CapturedAt = capturedAt,
            Controllers = built,
        };
    }

    /// <summary>Reads a device's negotiated link speed from its registry key, when present.</summary>
    public static UsbConnectionSpeed ReadSpeed(string instanceId)
    {
        using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\{instanceId}");
        var speed = key?.GetValue("DeviceSpeed") as string;

        return speed?.ToUpperInvariant() switch
        {
            "LOW" => UsbConnectionSpeed.Low,
            "FULL" => UsbConnectionSpeed.Full,
            "HIGH" => UsbConnectionSpeed.High,
            "SUPER" => UsbConnectionSpeed.Super,
            "SUPERPLUS" => UsbConnectionSpeed.SuperPlus,
            _ => UsbConnectionSpeed.Unknown,
        };
    }

    private static void Walk(
        uint devInst,
        int depth,
        string? controllerId,
        string? hubId,
        Dictionary<string, ControllerBuilder> controllers,
        ref int budget)
    {
        if (depth > MaxDepth || budget <= 0)
        {
            return;
        }

        var child = CfgMgr32.GetChild(devInst);
        while (child is not null && budget-- > 0)
        {
            var node = CfgMgr32.ReadNode(child.Value);
            var isController = IsHostController(node);
            var currentController = isController ? node.InstanceId : controllerId;
            var isHub = IsHub(node, isController);
            var currentHub = isHub ? node.InstanceId : hubId;

            if (currentController is not null)
            {
                if (!controllers.TryGetValue(currentController, out var builder))
                {
                    builder = new ControllerBuilder(currentController);
                    controllers[currentController] = builder;
                }

                if (isController)
                {
                    builder.Node = node;
                }
                else
                {
                    builder.Devices.Add(ToUsbDevice(node, currentController, hubId, isHub));
                }
            }

            Walk(child.Value, depth + 1, currentController, currentHub, controllers, ref budget);

            child = CfgMgr32.GetSibling(child.Value);
        }
    }

    private static bool IsHostController(DeviceNode node) =>
        string.Equals(node.ClassName, "USB", StringComparison.OrdinalIgnoreCase) &&
        (node.InstanceId.StartsWith(@"PCI\", StringComparison.OrdinalIgnoreCase) ||
         node.InstanceId.StartsWith(@"ACPI\", StringComparison.OrdinalIgnoreCase));

    private static bool IsHub(DeviceNode node, bool isController) =>
        !isController &&
        (string.Equals(node.ClassName, "USB", StringComparison.OrdinalIgnoreCase) ||
         node.InstanceId.Contains("ROOT_HUB", StringComparison.OrdinalIgnoreCase)) &&
        (node.InstanceId.Contains("ROOT_HUB", StringComparison.OrdinalIgnoreCase) ||
         node.CompatibleIds.Any(id => id.Contains(@"Class_09", StringComparison.OrdinalIgnoreCase)) ||
         (node.Service?.Contains("usbhub", StringComparison.OrdinalIgnoreCase) ?? false));
}
