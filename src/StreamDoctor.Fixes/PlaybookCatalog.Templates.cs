using StreamDoctor.Core.Fixes;

namespace StreamDoctor.Fixes;

public static partial class PlaybookCatalog
{
    private static List<FixPlaybook> BuildTemplates() =>
    [
        new FixPlaybook
        {
            Id = PlaybookIds.UsbDisableSelectiveSuspend,
            Title = "Stop Windows from putting this device to sleep",
            Summary = "Clears the selective-suspend flag for this device so it cannot quietly power down mid-stream.",
            Risk = FixRisk.Medium,
            Actions =
            [
                RegistryFlag(DeviceParametersRoot, "SelectiveSuspendEnabled", "Disable USB selective suspend for the device"),
            ],
            Prerequisites = ["OBS and games are closed", "A restore point is created before the change"],
            VerificationHint = "Re-run the power audit. The device should no longer be flagged for selective suspend.",
        },
        new FixPlaybook
        {
            Id = PlaybookIds.UsbDisableIdleIrpD3,
            Title = "Stop this device from powering down while idle",
            Summary = "Turns off AllowIdleIrpInD3, the setting behind the classic 'my mic goes silent after a quiet stretch' bug.",
            Risk = FixRisk.Medium,
            Actions =
            [
                RegistryFlag(DeviceParametersRoot, "AllowIdleIrpInD3", "Disallow idle IRPs in D3"),
            ],
            Prerequisites = ["OBS and games are closed"],
            VerificationHint = "Record a minute of silence, then speak: the first syllable should not be lost.",
        },
        new FixPlaybook
        {
            Id = PlaybookIds.UsbDisableHubPowerSaving,
            Title = "Stop this hub from powering its ports down",
            Summary = "Disables enhanced power management on the hub so devices below it cannot be dropped when the bus looks idle.",
            Risk = FixRisk.Medium,
            Actions =
            [
                RegistryFlag(DeviceParametersRoot, "EnhancedPowerManagementEnabled", "Disable hub power saving"),
            ],
            Prerequisites = ["Every device below the hub is identified"],
            VerificationHint = "Re-run the USB sweep and confirm the hub is no longer flagged.",
        },
        new FixPlaybook
        {
            Id = PlaybookIds.SystemHighPerformancePowerPlan,
            Title = "Switch to the High performance power plan",
            Summary = "Stops Windows from parking USB controllers and CPU cores aggressively while you stream.",
            Risk = FixRisk.Low,
            Actions =
            [
                new FixAction
                {
                    Kind = FixActionKind.PowerPlan,
                    Target = HighPerformanceSchemeGuid,
                    DesiredValue = HighPerformanceSchemeGuid,
                    Description = "Activate the High performance power scheme",
                    RequiresElevation = true,
                },
            ],
            VerificationHint = "Run 'powercfg /getactivescheme' and confirm High performance is active.",
        },
        new FixPlaybook
        {
            Id = PlaybookIds.UsbReEnumerateDevice,
            Title = "Power-cycle this device",
            Summary = "Soft-disables and re-enables the device to clear a hung USB endpoint. Equivalent to unplugging and replugging it.",
            Risk = FixRisk.High,
            AutoFixSupported = false,
            Actions =
            [
                new FixAction
                {
                    Kind = FixActionKind.DeviceReEnumeration,
                    Target = DevicePlaceholder,
                    Description = "Soft-disable and re-enable the device",
                    RequiresElevation = true,
                },
            ],
            Prerequisites =
            [
                "The device is not a keyboard, mouse, hub or host controller",
                "OBS is closed",
                "You are not live",
            ],
            VerificationHint = "The device should disappear and come back within two seconds in the USB map.",
        },
    ];
}
