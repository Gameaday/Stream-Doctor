using System.Globalization;
using Microsoft.Win32;

namespace StreamDoctor.Windows.Fixes;

public sealed partial class WindowsChangeApplier
{
    private static string? ReadRegistry(string target)
    {
        var (hive, subKey, valueName) = ParseRegistryTarget(target);

        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = baseKey.OpenSubKey(subKey);
        var value = key?.GetValue(valueName);

        return value switch
        {
            null => null,
            int number => number.ToString(CultureInfo.InvariantCulture),
            long number => number.ToString(CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };
    }

    private static void WriteRegistry(string target, string desiredValue)
    {
        var (hive, subKey, valueName) = ParseRegistryTarget(target);

        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = baseKey.OpenSubKey(subKey, writable: true)
            ?? throw new InvalidOperationException($"Registry key not found: {subKey}");

        if (int.TryParse(desiredValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            key.SetValue(valueName, number, RegistryValueKind.DWord);
        }
        else
        {
            key.SetValue(valueName, desiredValue, RegistryValueKind.String);
        }
    }

    private static void RestoreRegistry(string target, string? beforeValue)
    {
        var (hive, subKey, valueName) = ParseRegistryTarget(target);

        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = baseKey.OpenSubKey(subKey, writable: true)
            ?? throw new InvalidOperationException($"Registry key not found: {subKey}");

        if (beforeValue is null)
        {
            key.DeleteValue(valueName, throwOnMissingValue: false);
            return;
        }

        if (int.TryParse(beforeValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            key.SetValue(valueName, number, RegistryValueKind.DWord);
        }
        else
        {
            key.SetValue(valueName, beforeValue, RegistryValueKind.String);
        }
    }

    private static (RegistryHive Hive, string SubKey, string ValueName) ParseRegistryTarget(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        var separator = target.LastIndexOf('|');
        var path = separator > 0 ? target[..separator] : target;
        var valueName = separator > 0
            ? target[(separator + 1)..]
            : throw new ArgumentException("Registry targets must be 'HIVE\\path|ValueName'.", nameof(target));

        var hive = path.StartsWith(@"HKLM\", StringComparison.OrdinalIgnoreCase)
            ? RegistryHive.LocalMachine
            : path.StartsWith(@"HKCU\", StringComparison.OrdinalIgnoreCase)
                ? RegistryHive.CurrentUser
                : throw new ArgumentException($"Unsupported registry hive in '{target}'.", nameof(target));

        var firstSlash = path.IndexOf('\\');
        return (hive, path[(firstSlash + 1)..], valueName);
    }
}
