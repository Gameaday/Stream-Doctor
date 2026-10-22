using StreamDoctor.Core.Fixes;
using StreamDoctor.Core.Usb;

namespace StreamDoctor.Fixes;

/// <summary>The v1 playbook catalog. Playbooks are data; nothing here executes.</summary>
public static partial class PlaybookCatalog
{
    public const string DevicePlaceholder = "{deviceInstanceId}";

    /// <summary>Windows "High performance" power scheme.</summary>
    public const string HighPerformanceSchemeGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    internal const string DeviceParametersRoot =
        @"HKLM\SYSTEM\CurrentControlSet\Enum\" + DevicePlaceholder + @"\Device Parameters";

    public static IReadOnlyList<FixPlaybook> Templates { get; } = BuildTemplates();

    public static FixPlaybook? Find(string id) =>
        Templates.FirstOrDefault(playbook => string.Equals(playbook.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// Resolves placeholders for a concrete device. Returns null when the playbook is
    /// device-scoped and no device was supplied.
    /// </summary>
    public static FixPlaybook? Resolve(string id, UsbDevice? device)
    {
        var template = Find(id);
        if (template is null)
        {
            return null;
        }

        if (device is null)
        {
            return template.Actions.Any(static action => action.Target.Contains(DevicePlaceholder, StringComparison.Ordinal))
                ? null
                : template;
        }

        return template with
        {
            Actions = template.Actions
                .Select(action => action with
                {
                    Target = action.Target.Replace(DevicePlaceholder, device.InstanceId, StringComparison.Ordinal),
                    DeviceInstanceId = action.DeviceInstanceId ?? device.InstanceId,
                })
                .ToList(),
        };
    }

    private static FixAction RegistryFlag(string keyTemplate, string valueName, string description) => new()
    {
        Kind = FixActionKind.RegistryValue,
        Target = keyTemplate + "|" + valueName,
        DesiredValue = "0",
        Description = description,
        RequiresElevation = true,
    };
}
