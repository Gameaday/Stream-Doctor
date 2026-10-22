using StreamDoctor.Core.Model;

namespace StreamDoctor.Core.Usb;

/// <summary>
/// Tri-state power-management flags read from a device's registry key. Unknown
/// means the value was absent or unreadable; absence is never treated as "safe".
/// </summary>
public sealed record UsbPowerSettings
{
    /// <summary>True when Windows may suspend the device (selective suspend).</summary>
    public bool? SelectiveSuspendEnabled { get; init; }

    /// <summary>True when the device may enter D3 while idle (the "mic goes silent" cause).</summary>
    public bool? AllowIdleIrpInD3 { get; init; }

    /// <summary>True when the hub is allowed to power devices down.</summary>
    public bool? EnhancedPowerManagementEnabled { get; init; }

    /// <summary>Registry path the values were read from, for the change journal.</summary>
    public string? SourceKey { get; init; }

    public bool IsUnknown =>
        SelectiveSuspendEnabled is null &&
        AllowIdleIrpInD3 is null &&
        EnhancedPowerManagementEnabled is null;
}

/// <summary>A device plus the power flags found for it.</summary>
public sealed record UsbPowerSnapshot(UsbDevice Device, UsbPowerSettings Settings);

/// <summary>Active Windows power plan, resolved by the system inventory collector.</summary>
public sealed record PowerPlanInfo(string? Name, string? SchemeGuid, bool IsHighPerformance)
{
    public static PowerPlanInfo Unknown { get; } = new(null, null, false);
}

/// <summary>One power-management risk on one device (or on the system power plan).</summary>
public sealed record UsbPowerRisk(
    string Code,
    UsbDevice? Device,
    Severity Severity,
    string Explanation,
    string FixPlaybookId);

/// <summary>
/// Static power-management audit (design doc §9, Test 5). It performs no writes;
/// every risky setting becomes a risk the wizard can fix through the journal.
/// </summary>
public static class UsbPowerAudit
{
    public const string SelectiveSuspendCode = "usb.power.selective-suspend";
    public const string IdleIrpCode = "usb.power.idle-irp-d3";
    public const string HubPowerSavingCode = "usb.power.hub-power-saving";
    public const string BalancedPlanCode = "system.power.balanced-plan";

    public static IReadOnlyList<UsbPowerRisk> Evaluate(
        UsbTopology topology,
        IEnumerable<UsbPowerSnapshot> snapshots,
        PowerPlanInfo? plan = null)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(snapshots);

        var byInstanceId = topology.AllDevices
            .GroupBy(static device => device.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);

        var risks = new List<UsbPowerRisk>();

        foreach (var snapshot in snapshots)
        {
            // Only audit devices that are actually enumerated right now; stale snapshots
            // for unplugged gear would produce noise the user cannot act on.
            if (!byInstanceId.TryGetValue(snapshot.Device.InstanceId, out var device))
            {
                continue;
            }

            var settings = snapshot.Settings;

            if (settings.IsUnknown)
            {
                continue;
            }

            if (device.Class.IsStreamCritical() && settings.SelectiveSuspendEnabled == true)
            {
                risks.Add(new UsbPowerRisk(
                    SelectiveSuspendCode,
                    device,
                    Severity.Warning,
                    $"{device.Name} is allowed to go to sleep when it looks idle. That is the classic \"my mic goes silent after a quiet stretch\" bug.",
                    Fixes.PlaybookIds.UsbDisableSelectiveSuspend));
            }

            if (device.Class.IsIsochronous() && settings.AllowIdleIrpInD3 == true)
            {
                risks.Add(new UsbPowerRisk(
                    IdleIrpCode,
                    device,
                    Severity.Warning,
                    $"{device.Name} can power down between packets, which shows up as crackle or a one-second dropout.",
                    Fixes.PlaybookIds.UsbDisableIdleIrpD3));
            }

            if (device.Class == UsbDeviceClass.Hub && settings.EnhancedPowerManagementEnabled == true)
            {
                risks.Add(new UsbPowerRisk(
                    HubPowerSavingCode,
                    device,
                    Severity.Advisory,
                    $"Hub {device.Name} is allowed to power its ports down, so anything plugged into it can disappear mid-stream.",
                    Fixes.PlaybookIds.UsbDisableHubPowerSaving));
            }
        }

        if (plan?.IsHighPerformance == false && !string.IsNullOrEmpty(plan.Name))
        {
            risks.Add(new UsbPowerRisk(
                BalancedPlanCode,
                null,
                Severity.Advisory,
                $"Your power plan is \"{plan.Name}\". Windows parks USB controllers more aggressively on balanced plans.",
                Fixes.PlaybookIds.SystemHighPerformancePowerPlan));
        }

        return risks
            .OrderByDescending(static risk => risk.Severity)
            .ThenBy(static risk => risk.Device?.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
