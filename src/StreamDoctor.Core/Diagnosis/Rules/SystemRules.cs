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

            foreach (var group in Group(category, events))
            {
                var tally = new EvidenceTally();
                foreach (var evidence in group.Events.Take(10))
                {
                    tally.Support($"{category}:{evidence.Timestamp:O}:{evidence.Sequence}", evidence);
                }

                var relevant = group.Label is null || IsStreamRelevant(group.Label);
                var severity = category == ScanEventCodes.SystemPowerEvent
                    ? Severity.Critical
                    : category == ScanEventCodes.AppCrashed && !relevant
                        ? Severity.Advisory
                        : Severity.Warning;

                yield return new Finding
                {
                    HypothesisId = group.Label is null
                        ? $"{Id}.{category}"
                        : $"{Id}.{category}.{Slug(group.Label)}",
                    Module = ScanModule.WindowsInterference,
                    Title = group.Label is null
                        ? $"{title} ({group.Events.Count.ToString(CultureInfo.InvariantCulture)}x)"
                        : $"{group.Label} crashed during your stream ({group.Events.Count.ToString(CultureInfo.InvariantCulture)}x)",
                    Explanation = group.Label is null
                        ? explanation
                        : relevant
                            ? $"{group.Label} crashed mid-stream. Apps that own a capture device or the encoder cause a visible freeze while Windows restarts them."
                            : $"{group.Label} crashed mid-stream, but it is not a capture or encoding component, so it is probably unrelated to the stream glitch.",
                    Severity = severity,
                    Confidence = tally.Score(BaseWeight),
                    Evidence = tally.Evidence,
                    RecommendedAction = relevant
                        ? "Check Reliability Monitor around these timestamps; update or remove the crashing app before the next stream."
                        : "No action needed for the stream itself, but the crash is worth reporting to the app vendor.",
                    Data = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["target"] = group.Label is null ? category : $"{category}:{group.Label}",
                        [ScanEventPayload.Count] = group.Events.Count.ToString(CultureInfo.InvariantCulture),
                        [ScanEventPayload.Device] = group.Label ?? string.Empty,
                    }),
                };
            }
        }
    }

    private static IEnumerable<(string? Label, List<ScanEvent> Events)> Group(
        string category,
        IReadOnlyList<ScanEvent> events)
    {
        if (category != ScanEventCodes.AppCrashed)
        {
            yield return (null, events.ToList());
            yield break;
        }

        foreach (var group in events.GroupBy(static evidence =>
                     evidence.GetPayload(ScanEventPayload.Device) ?? "an app"))
        {
            yield return (group.Key, group.ToList());
        }
    }

    /// <summary>
    /// Only crashes of stream-path software explain a frozen stream; a random
    /// background app crashing is noise and is reported as advisory instead.
    /// </summary>
    private static bool IsStreamRelevant(string application) => StreamSoftwareHints.Any(hint =>
        application.Contains(hint, StringComparison.OrdinalIgnoreCase));

    private static readonly string[] StreamSoftwareHints =
    [
        "obs", "streamlabs", "xsplit", "elgato", "capture", "camera", "webcam",
        "voicemeeter", "nvidia broadcast", "icue", "ghub", "synapse", "streamdeck",
    ];

    private static string Slug(string value) => new(
        value.ToLowerInvariant()
            .Select(static character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());

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
