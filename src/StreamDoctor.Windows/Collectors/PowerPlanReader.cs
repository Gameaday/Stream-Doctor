using System.Management;
using StreamDoctor.Core.Usb;

namespace StreamDoctor.Windows.Collectors;

/// <summary>Reads the active power plan through WMI and recognizes high-performance schemes.</summary>
public static class PowerPlanReader
{
    private static readonly string[] HighPerformanceGuids =
    [
        "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", // High performance
        "e9a42b02-d5df-448d-aa00-03f14749eb61", // Ultimate performance
    ];

    public static PowerPlanInfo Read()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\cimv2\power",
                "SELECT InstanceID, ElementName, IsActive FROM Win32_PowerPlan WHERE IsActive = TRUE");

            using var results = searcher.Get();
            foreach (ManagementObject plan in results)
            {
                using (plan)
                {
                    var name = plan["ElementName"] as string;
                    var guid = ExtractGuid(plan["InstanceID"] as string);

                    return new PowerPlanInfo(
                        name,
                        guid,
                        guid is not null && HighPerformanceGuids.Contains(guid, StringComparer.OrdinalIgnoreCase));
                }
            }
        }
        catch (ManagementException)
        {
            // WMI can be disabled or broken; the audit then reports an unknown plan.
        }

        return PowerPlanInfo.Unknown;
    }

    private static string? ExtractGuid(string? instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
        {
            return null;
        }

        var start = instanceId.LastIndexOf('{');
        var end = instanceId.LastIndexOf('}');
        return start >= 0 && end > start ? instanceId[(start + 1)..end] : null;
    }
}
