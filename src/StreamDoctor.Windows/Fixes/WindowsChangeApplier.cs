using System.Globalization;
using Microsoft.Win32;
using StreamDoctor.Core.Fixes;
using StreamDoctor.Fixes;

namespace StreamDoctor.Windows.Fixes;

/// <summary>
/// Real effector for the journaled fixes: registry values and power plans.
/// Device re-enumeration is deliberately not implemented here yet — it lands with
/// the USB test bench (design doc M3) together with its denylist.
/// </summary>
public sealed partial class WindowsChangeApplier : IChangeApplier
{
    public bool CanApply(FixAction action) =>
        action.Kind is FixActionKind.RegistryValue or FixActionKind.PowerPlan;

    public Task<string?> ReadAsync(FixAction action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        return Task.Run(
            () => action.Kind switch
            {
                FixActionKind.RegistryValue => ReadRegistry(action.Target),
                FixActionKind.PowerPlan => ReadActivePowerPlan(),
                _ => throw new NotSupportedException($"Cannot read a {action.Kind} action."),
            },
            cancellationToken);
    }

    public Task ApplyAsync(FixAction action, string? beforeValue, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        return Task.Run(
            () =>
            {
                switch (action.Kind)
                {
                    case FixActionKind.RegistryValue:
                        WriteRegistry(action.Target, action.DesiredValue ?? "0");
                        break;
                    case FixActionKind.PowerPlan:
                        SetActivePowerPlan(action.DesiredValue ?? action.Target);
                        break;
                    default:
                        throw new NotSupportedException($"Cannot apply a {action.Kind} action.");
                }
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
                    default:
                        throw new NotSupportedException($"Cannot revert a {action.Kind} action.");
                }
            },
            cancellationToken);
    }
}
