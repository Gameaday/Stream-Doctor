using System.Globalization;
using Microsoft.Win32;
using StreamDoctor.Core.Fixes;
using StreamDoctor.Fixes;

namespace StreamDoctor.Windows.Fixes;

/// <summary>
/// Real effector for the journaled fixes: registry values, power plans, config backups,
/// software updates via winget, and Windows Update installs.
/// Device re-enumeration is deliberately not implemented here yet — it lands with
/// the USB test bench (design doc M3) together with its denylist.
/// </summary>
public sealed partial class WindowsChangeApplier : IChangeApplier
{
    public bool CanApply(FixAction action) =>
        action.Kind is FixActionKind.RegistryValue
            or FixActionKind.PowerPlan
            or FixActionKind.ConfigBackup
            or FixActionKind.SoftwareUpdate
            or FixActionKind.WindowsUpdate
            or FixActionKind.RestorePoint
            or FixActionKind.DeviceReEnumeration;

    public Task<string?> ReadAsync(FixAction action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        return Task.Run(
            () =>
            {
                return action.Kind switch
                {
                    FixActionKind.RegistryValue => Task.FromResult<string?>(ReadRegistry(action.Target)),
                    FixActionKind.PowerPlan => Task.FromResult<string?>(ReadActivePowerPlan()),
                    FixActionKind.ConfigBackup => Task.FromResult<string?>(null),
                    FixActionKind.SoftwareUpdate => Task.FromResult<string?>(GetInstalledVersion(action.Target)),
                    FixActionKind.WindowsUpdate => Task.FromResult<string?>(null),
                    FixActionKind.RestorePoint => Task.FromResult<string?>(null),
                    FixActionKind.DeviceReEnumeration => Task.FromResult<string?>(null),
                    _ => throw new NotSupportedException($"Cannot read a {action.Kind} action."),
                };
            },
            cancellationToken);
    }

    public Task<string?> ApplyAsync(FixAction action, string? beforeValue, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        return Task.Run(
            () =>
            {
                return action.Kind switch
                {
                    FixActionKind.RegistryValue => Task.FromResult<string?>(WriteRegistry(action.Target, action.DesiredValue ?? "0")),
                    FixActionKind.PowerPlan => Task.FromResult<string?>(SetActivePowerPlan(action.DesiredValue ?? action.Target)),
                    FixActionKind.ConfigBackup => Task.FromResult<string?>(BackupConfig(action.Target)),
                    FixActionKind.SoftwareUpdate => Task.FromResult<string?>(UpdateSoftware(action.Target, action.DesiredValue ?? "")),
                    FixActionKind.WindowsUpdate => Task.FromResult<string?>(InstallWindowsUpdate(action.Target, action.DesiredValue ?? "")),
                    FixActionKind.RestorePoint => Task.FromResult<string?>(CreateRestorePoint(action.Target)),
                    FixActionKind.DeviceReEnumeration => Task.FromResult<string?>(ReEnumerateDevice(action.Target)),
                    _ => throw new NotSupportedException($"Cannot apply a {action.Kind} action."),
                };
            },
            cancellationToken);
    }

    public Task RevertAsync(FixAction action, string? beforeValue, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        return Task.Run(
            () =>
            {
                switch (action.Kind)
                {
                    case FixActionKind.RegistryValue:
                        RestoreRegistry(action.Target, beforeValue);
                        break;
                    case FixActionKind.PowerPlan:
                        if (!string.IsNullOrWhiteSpace(beforeValue))
                        {
                            SetActivePowerPlan(beforeValue);
                        }
                        break;
                    case FixActionKind.ConfigBackup:
                        RestoreConfig(action.Target, beforeValue);
                        break;
                    case FixActionKind.SoftwareUpdate:
                        // Rollback handled by winget uninstall or config restore
                        break;
                    case FixActionKind.WindowsUpdate:
                        // Rollback via restore point
                        break;
                    case FixActionKind.RestorePoint:
                        // A restore point is append-only; there is nothing to undo.
                        break;
                    case FixActionKind.DeviceReEnumeration:
                        // Re-enumeration is not a persistent state change.
                        break;
                    default:
                        throw new NotSupportedException($"Cannot revert a {action.Kind} action.");
                }
            },
            cancellationToken);
    }
}
