namespace StreamDoctor.Core.Fixes;

/// <summary>Stable identifiers for fix playbooks shipped with v1.</summary>
public static class PlaybookIds
{
    public const string UsbDisableSelectiveSuspend = "usb.disable-selective-suspend";
    public const string UsbDisableIdleIrpD3 = "usb.disable-idle-irp-d3";
    public const string UsbDisableHubPowerSaving = "usb.disable-hub-power-saving";
    public const string SystemHighPerformancePowerPlan = "system.high-performance-power-plan";
    public const string UsbReEnumerateDevice = "usb.re-enumerate-device";

    /// <summary>Prefix for dynamically generated update playbooks.</summary>
    public const string UpdatePlaybookPrefix = "update:";

    /// <summary>Stable playbook id for one update candidate.</summary>
    public static string UpdatePlaybook(string candidateId) =>
        UpdatePlaybookPrefix + candidateId;
}

/// <summary>How risky a playbook is to apply.</summary>
public enum FixRisk
{
    /// <summary>Reversible setting with no plausible failure mode.</summary>
    Low = 0,

    /// <summary>Reversible, but can interrupt a device for a second.</summary>
    Medium = 1,

    /// <summary>Can interrupt the whole machine or a critical device; always confirmed.</summary>
    High = 2,
}

/// <summary>The kind of change a fix action performs; drives which effector handles it.</summary>
public enum FixActionKind
{
    RegistryValue,
    PowerPlan,
    DeviceReEnumeration,
    ServiceStartup,
    ObsWebSocketSetting,
    Manual,

    /// <summary>Copy a configuration directory to a timestamped backup before an update.</summary>
    ConfigBackup,

    /// <summary>Upgrade a package through winget (signature-verified by Windows).</summary>
    SoftwareUpdate,

    /// <summary>Install a Windows Update driver, firmware or patch.</summary>
    WindowsUpdate,

    /// <summary>Create a System Restore point before a driver, firmware or patch install.</summary>
    RestorePoint,
}

/// <summary>
/// A single, reversible change. <see cref="RollbackValue"/> is only set for values
/// known ahead of time; otherwise the executor captures the current value before
/// applying and journals it.
/// </summary>
public sealed record FixAction
{
    public required FixActionKind Kind { get; init; }

    /// <summary>
    /// Effector-specific target, e.g. <c>HKLM\SYSTEM\CurrentControlSet\Enum\...\Device Parameters|SelectiveSuspendEnabled</c>
    /// or a power-plan scheme GUID.
    /// </summary>
    public required string Target { get; init; }

    /// <summary>Value to write. Null means "the effector's safe default".</summary>
    public string? DesiredValue { get; init; }

    /// <summary>Value to restore on rollback when known without reading the system.</summary>
    public string? RollbackValue { get; init; }

    /// <summary>What the user sees in the wizard before approving.</summary>
    public string Description { get; init; } = string.Empty;

    public bool RequiresElevation { get; init; }

    /// <summary>Optional device instance ID this action applies to (for audit + denylists).</summary>
    public string? DeviceInstanceId { get; init; }
}

/// <summary>
/// A fix playbook: ordered actions, prerequisites and the verification hint the
/// wizard shows after applying. Playbooks are data; execution lives in
/// <c>StreamDoctor.Fixes</c>.
/// </summary>
public sealed record FixPlaybook
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    /// <summary>Streamer-facing description of what will change and why.</summary>
    public required string Summary { get; init; }

    public required FixRisk Risk { get; init; }

    public IReadOnlyList<FixAction> Actions { get; init; } = [];

    /// <summary>Conditions checked before offering the fix, in plain language.</summary>
    public IReadOnlyList<string> Prerequisites { get; init; } = [];

    /// <summary>How to confirm the fix worked, shown after applying.</summary>
    public string? VerificationHint { get; init; }

    /// <summary>False when the fix always requires a human decision.</summary>
    public bool AutoFixSupported { get; init; } = true;
}
