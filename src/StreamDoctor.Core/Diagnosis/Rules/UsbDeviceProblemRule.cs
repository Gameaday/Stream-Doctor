using System.Collections.ObjectModel;
using System.Globalization;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Usb;

namespace StreamDoctor.Core.Diagnosis.Rules;

/// <summary>
/// Surfaces Windows device problem codes ("This device cannot start", code 10/28/31/43).
/// These are the cases where a device is enumerated but unusable, so every other
/// USB analysis would silently treat it as present and healthy.
/// </summary>
public sealed class UsbDeviceProblemRule : IFindingRule
{
    private const double BaseWeight = 0.6;

    private static readonly Dictionary<int, string> CodeExplanations = new()
    {
        [10] = "Windows cannot start this device",
        [18] = "the drivers must be reinstalled",
        [22] = "the device is disabled in Device Manager",
        [28] = "the drivers are not installed",
        [31] = "Windows cannot load the drivers for this device",
        [43] = "Windows stopped the device after it reported a problem",
    };

    public string Id => "usb.device.problem";

    public ScanModule OwnerModule => ScanModule.UsbSentinel;

    public IEnumerable<Finding> Evaluate(DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var events = context.ByCategoryInWindow(ScanEventCodes.UsbDeviceProblem);

        foreach (var group in events.GroupBy(static evidence =>
                     evidence.GetPayload(ScanEventPayload.DeviceInstanceId) ?? evidence.DeviceKey ?? "unknown-device"))
        {
            var latest = group.OrderBy(static evidence => evidence.Timestamp).Last();
            var code = ParseCode(latest);
            var device = latest.GetPayload(ScanEventPayload.Device) ?? group.Key;
            var explanation = code is not null && CodeExplanations.TryGetValue(code.Value, out var known)
                ? known
                : "Windows reports a problem with it";

            var tally = new EvidenceTally();
            tally.Support($"problem:{group.Key}:{code}", latest);

            yield return new Finding
            {
                HypothesisId = code is null ? Id : $"{Id}.code-{code.Value.ToString(CultureInfo.InvariantCulture)}",
                Module = ScanModule.UsbSentinel,
                Title = $"{device} is not working: {explanation}",
                Explanation = $"Windows enumerates {device} but cannot use it ({explanation}). Anything that depends on it — OBS sources, game audio, your mic — is silent or missing.",
                Severity = SeverityFor(ParseClass(latest), code),
                Confidence = tally.Score(BaseWeight),
                Evidence = tally.Evidence,
                RecommendedAction = RecommendedActionFor(ParseClass(latest)),
                Data = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target"] = group.Key,
                    [ScanEventPayload.Device] = device,
                    ["problemCode"] = code?.ToString(CultureInfo.InvariantCulture) ?? "unknown",
                }),
            };
        }
    }

    /// <summary>
    /// A broken printer must not turn the board red. Only classes the stream depends on
    /// are critical; storage/network are warnings; everything else is advisory.
    /// </summary>
    private static Severity SeverityFor(UsbDeviceClass? deviceClass, int? code)
    {
        if (code == 43)
        {
            return Severity.Warning;
        }

        return deviceClass switch
        {
            null => Severity.Advisory,
            { } cls when cls.IsStreamCritical() => Severity.Critical,
            UsbDeviceClass.Storage or UsbDeviceClass.Network => Severity.Warning,
            _ => Severity.Advisory,
        };
    }

    private static string RecommendedActionFor(UsbDeviceClass? deviceClass) => deviceClass is
        UsbDeviceClass.Webcam or UsbDeviceClass.CaptureCard or UsbDeviceClass.AudioInterface or UsbDeviceClass.Microphone
        ? "Unplug it, try another port on a different controller, then update or reinstall its driver. Until it works, your stream has no camera or no audio."
        : "Unplug it, try another port, then update or reinstall its driver. If code 28 or 31 persists, the driver package is missing.";

    private static UsbDeviceClass? ParseClass(ScanEvent evidence) =>
        Enum.TryParse<UsbDeviceClass>(evidence.GetPayload(ScanEventPayload.DeviceClass), ignoreCase: true, out var cls)
            ? cls
            : null;

    private static int? ParseCode(ScanEvent evidence) =>
        int.TryParse(evidence.GetPayload("problemCode"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)
            ? code
            : null;
}
