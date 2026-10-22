using System.Collections.ObjectModel;
using System.Globalization;
using StreamDoctor.Core.Fixes;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Usb;

namespace StreamDoctor.Core.Diagnosis.Rules;

/// <summary>
/// Correlates OBS "device lost" events with USB resets and power settings.
/// This is the post-flight headline: "your mic dropped 14 times at 21:03".
/// </summary>
public sealed class UsbDropoutRule : IFindingRule
{
    private const double BaseWeight = 0.4;
    private const int ResetCorrelationSeconds = 120;
    private const int MaxEvidenceItems = 25;

    public string Id => "usb.dropout.correlated";

    public ScanModule OwnerModule => ScanModule.UsbSentinel;

    public IEnumerable<Finding> Evaluate(DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var disconnects = context.ByCategoryInWindow(ScanEventCodes.ObsSourceDisconnected);
        if (disconnects.Count == 0)
        {
            yield break;
        }

        var resets = context.ByCategoryInWindow(ScanEventCodes.UsbDeviceReset)
            .Concat(context.ByCategoryInWindow(ScanEventCodes.UsbControllerError))
            .OrderBy(static evidence => evidence.Timestamp)
            .ToList();

        foreach (var group in disconnects.GroupBy(static evidence => evidence.DeviceKey ?? "unknown device"))
        {
            var tally = new EvidenceTally();
            var occurrences = group.OrderBy(static evidence => evidence.Timestamp).ToList();

            foreach (var occurrence in occurrences.Take(MaxEvidenceItems))
            {
                tally.Support($"disconnect:{occurrence.Timestamp:O}:{occurrence.Sequence}", occurrence);
            }

            var lastDisconnect = occurrences[^1].Timestamp;
            var nearbyReset = resets.FirstOrDefault(reset =>
                Math.Abs((reset.Timestamp - lastDisconnect).TotalSeconds) <= ResetCorrelationSeconds);

            if (nearbyReset is not null)
            {
                tally.Support("usb-reset", nearbyReset);
            }

            var device = context.Topology?.FindDevice(group.Key);
            var powerCause = device is null ? null : FindPowerCause(context, device);

            if (device is not null)
            {
                tally.Support($"topology:{device.InstanceId}", context.EvidenceFor(device, lastDisconnect));
            }

            if (powerCause is not null)
            {
                tally.Support($"power:{powerCause.Value.Flag}", powerCause.Value.Evidence);
            }

            var count = occurrences.Count;
            var severity = count >= 5 ? Severity.Critical : count >= 2 ? Severity.Warning : Severity.Advisory;
            var playbook = powerCause?.PlaybookId;

            yield return new Finding
            {
                HypothesisId = Id,
                Module = ScanModule.UsbSentinel,
                Title = $"{group.Key} dropped out {count.ToString(CultureInfo.InvariantCulture)} time(s) during the stream",
                Explanation = BuildExplanation(group.Key, count, nearbyReset is not null, powerCause?.Reason),
                Severity = severity,
                Confidence = tally.Score(BaseWeight),
                Evidence = tally.Evidence,
                FixPlaybookId = playbook,
                RecommendedAction = playbook is null
                    ? "Try another cable first, then move the device to a rear port on a different controller."
                    : null,
                Data = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target"] = device?.InstanceId ?? group.Key,
                    [ScanEventPayload.Device] = group.Key,
                    [ScanEventPayload.Count] = count.ToString(CultureInfo.InvariantCulture),
                    ["nearbyReset"] = nearbyReset is not null ? "true" : "false",
                }),
            };
        }
    }

    private static (string Flag, string Reason, string PlaybookId, ScanEvent Evidence)? FindPowerCause(
        DiagnosisContext context,
        UsbDevice device)
    {
        var snapshot = context.PowerSnapshots.FirstOrDefault(candidate =>
            string.Equals(candidate.Device.InstanceId, device.InstanceId, StringComparison.OrdinalIgnoreCase));

        if (snapshot is null || snapshot.Settings.IsUnknown)
        {
            return null;
        }

        if (snapshot.Settings.AllowIdleIrpInD3 == true)
        {
            return ("AllowIdleIrpInD3", "it is allowed to power down while idle",
                PlaybookIds.UsbDisableIdleIrpD3, PowerEvidence(device, "AllowIdleIrpInD3", context));
        }

        if (snapshot.Settings.SelectiveSuspendEnabled == true)
        {
            return ("SelectiveSuspendEnabled", "Windows is allowed to suspend it",
                PlaybookIds.UsbDisableSelectiveSuspend, PowerEvidence(device, "SelectiveSuspendEnabled", context));
        }

        return null;
    }

    private static ScanEvent PowerEvidence(UsbDevice device, string flag, DiagnosisContext context) => new()
    {
        Timestamp = context.Events.Count == 0 ? context.WindowStart ?? DateTimeOffset.UtcNow : context.Events[0].Timestamp,
        Source = ScanSource.SystemInventory,
        Module = ScanModule.UsbSentinel,
        Category = ScanEventCodes.UsbPowerFlag,
        Severity = Severity.Warning,
        Message = $"{device.Name} has {flag} enabled.",
        Payload = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ScanEventPayload.Device] = device.Name,
            [ScanEventPayload.DeviceInstanceId] = device.InstanceId,
            ["flag"] = flag,
        }),
    };

    private static string BuildExplanation(string deviceName, int count, bool sawReset, string? powerReason)
    {
        var cause = sawReset
            ? "the USB controller reset right before the dropouts"
            : powerReason ?? "the device disappeared from the USB bus";

        return $"{deviceName} left the USB bus {count.ToString(CultureInfo.InvariantCulture)} time(s) while you were live, and {cause}. " +
               "That is the dropout chat sees as a frozen camera or a silent mic.";
    }
}
