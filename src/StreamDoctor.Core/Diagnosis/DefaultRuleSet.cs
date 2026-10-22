using StreamDoctor.Core.Model;

namespace StreamDoctor.Core.Diagnosis;

/// <summary>
/// The rules v1 ships with, in one place. Ordering does not matter (the engine
/// ranks by confidence), but every rule must have a unique id.
/// </summary>
public static class DefaultRuleSet
{
    public static IReadOnlyList<IFindingRule> Create() =>
    [
        new Rules.UsbBandwidthRule(),
        new Rules.UsbPowerRule(),
        new Rules.UsbDropoutRule(),
        new Rules.UsbTopologyChangeRule(),
        new Rules.UsbDeviceProblemRule(),
        new Rules.ObsFrameHealthRule(),
        new Rules.ObsPluginFailureRule(),
        new Rules.ObsSampleRateRule(),
        new Rules.DiskSpaceRule(),
        new Rules.WindowsInterferenceRule(),
    ];
}
