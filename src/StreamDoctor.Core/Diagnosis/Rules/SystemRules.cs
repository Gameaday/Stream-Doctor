using System.Collections.ObjectModel;
using System.Globalization;
using StreamDoctor.Core.Model;

namespace StreamDoctor.Core.Diagnosis.Rules;

/// <summary>Flags drives that will fill up during a long stream (design doc §2.5).</summary>
public sealed class DiskSpaceRule : IFindingRule
{
    private const double BaseWeight = 0.5;
    private const long Gigabyte = 1024L * 1024 * 1024;

    public string Id => "disk.low-space";

    public ScanModule OwnerModule => ScanModule.DiskRecording;

    public IEnumerable<Finding> Evaluate(DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var observations = context.ByCategory(ScanEventCodes.DiskSpaceObserved)
            .Where(evidence => ParseLong(evidence, ScanEventPayload.FreeBytes) is not null &&
                               ParseLong(evidence, ScanEventPayload.TotalBytes) is > 0);

        foreach (var group in observations.GroupBy(static evidence => evidence.GetPayload(ScanEventPayload.Drive) ?? "?"))
        {
            var latest = group.OrderBy(static evidence => evidence.Timestamp).Last();
            var free = ParseLong(latest, ScanEventPayload.FreeBytes)!.Value;
            var total = ParseLong(latest, ScanEventPayload.TotalBytes)!.Value;
            var ratio = (double)free / total;

            var severity = ratio < 0.05 || free < 10 * Gigabyte
                ? Severity.Critical
                : ratio < 0.15 || free < 30 * Gigabyte
                    ? Severity.Warning
                    : Severity.Info;

            if (severity == Severity.Info)
            {
                continue;
            }

            var tally = new EvidenceTally();
            tally.Support($"drive:{group.Key}", latest);

            yield return new Finding
            {
                HypothesisId = Id,
                Module = ScanModule.DiskRecording,
                Title = $"{group.Key} has only {Gigabytes(free)} free",
                Explanation = $"OBS records locally as well as streaming. With {Gigabytes(free)} left on {group.Key}, a two-hour recording can fill the drive and stop the stream.",
                Severity = severity,
                Confidence = tally.Score(BaseWeight),
                Evidence = tally.Evidence,
                RecommendedAction = "Delete old recordings or point OBS at a bigger drive before you go live.",
                Data = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target"] = group.Key,
                    [ScanEventPayload.Drive] = group.Key,
                    [ScanEventPayload.FreeBytes] = free.ToString(CultureInfo.InvariantCulture),
                }),
            };
        }
    }

    private static long? ParseLong(ScanEvent evt, string key) =>
        long.TryParse(evt.GetPayload(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static string Gigabytes(long bytes) =>
        (bytes / (double)Gigabyte).ToString("0.#", CultureInfo.InvariantCulture) + " GB";
}

/// <summary>
/// Finds Windows interference that lines up with the stream window: mid-stream
/// updates, service crashes, app crashes and unexpected power events.
/// </summary>
public sealed class WindowsInterferenceRule : IFindingRule
{
    private const double BaseWeight = 0.45;

    public string Id => "windows.interference";

    public ScanModule OwnerModule => ScanModule.WindowsInterference;

    public IEnumerable<Finding> Evaluate(DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var (category, title, explanation) in Checks)
        {
            var events = context.ByCategoryInWindow(category);
            if (events.Count == 0)
            {
                continue;
            }

            var tally = new EvidenceTally();
            foreach (var evidence in events.Take(10))
            {
                tally.Support($"{category}:{evidence.Timestamp:O}:{evidence.Sequence}", evidence);
            }

            yield return new Finding
            {
                HypothesisId = $"{Id}.{category}",
                Module = ScanModule.WindowsInterference,
                Title = $"{title} ({events.Count.ToString(CultureInfo.InvariantCulture)}x)",
                Explanation = explanation,
                Severity = category == ScanEventCodes.SystemPowerEvent ? Severity.Critical : Severity.Warning,
                Confidence = tally.Score(BaseWeight),
                Evidence = tally.Evidence,
                RecommendedAction = "Set quiet hours for Windows Update and driver updates, and check Reliability Monitor for the same timestamps.",
                Data = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target"] = category,
                    [ScanEventPayload.Count] = events.Count.ToString(CultureInfo.InvariantCulture),
                }),
            };
        }
    }

    private static readonly (string Category, string Title, string Explanation)[] Checks =
    [
        (ScanEventCodes.WindowsUpdateInstalled, "Windows installed an update during your stream",
            "An update landing mid-stream can restart services, reset audio, or stall the encoder."),
        (ScanEventCodes.ServiceFailed, "A Windows service failed during your stream",
            "Service crashes mid-stream usually show up as a device or audio glitch a few seconds later."),
        (ScanEventCodes.AppCrashed, "An app crashed during your stream",
            "Crashes logged in the stream window explain short freezes, especially when the app owns a capture device."),
        (ScanEventCodes.SystemPowerEvent, "Windows logged an unexpected power event",
            "A power event means the machine lost power, slept, or was forcibly shut down mid-stream."),
    ];
}
