using System.Collections.ObjectModel;
using System.Globalization;
using StreamDoctor.Core.Model;

namespace StreamDoctor.Core.Diagnosis.Rules;

/// <summary>Flags OBS plugins that failed to load, with the update path as the fix.</summary>
public sealed class ObsPluginFailureRule : IFindingRule
{
    private const double BaseWeight = 0.5;

    public string Id => "obs.plugin.load-failed";

    public ScanModule OwnerModule => ScanModule.StreamingSoftware;

    public IEnumerable<Finding> Evaluate(DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var failures = context.ByCategoryInWindow(ScanEventCodes.ObsPluginLoadFailed)
            .GroupBy(static evidence => evidence.GetPayload(ScanEventPayload.Plugin) ?? "unknown plugin");

        foreach (var failure in failures)
        {
            var tally = new EvidenceTally();
            foreach (var evidence in failure)
            {
                tally.Support($"failure:{evidence.Timestamp:O}:{evidence.Sequence}", evidence);
            }

            yield return new Finding
            {
                HypothesisId = Id,
                Module = ScanModule.StreamingSoftware,
                Title = $"OBS could not load the plugin '{failure.Key}'",
                Explanation = $"A plugin that OBS expects is missing or incompatible ({failure.Key}). Sources that depend on it will be silently missing from your scenes.",
                Severity = Severity.Warning,
                Confidence = tally.Score(BaseWeight),
                Evidence = tally.Evidence,
                RecommendedAction = $"Update or remove '{failure.Key}', then restart OBS and check the log again.",
                Data = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target"] = failure.Key,
                    [ScanEventPayload.Plugin] = failure.Key,
                }),
            };
        }
    }
}

/// <summary>
/// Flags a 44.1 kHz OBS audio pipeline. Most capture devices run at 48 kHz, and a
/// mismatch is the classic source of crackle (design doc §2.5, Audio Chain Doctor).
/// </summary>
public sealed class ObsSampleRateRule : IFindingRule
{
    private const double BaseWeight = 0.45;

    public string Id => "obs.audio.sample-rate";

    public ScanModule OwnerModule => ScanModule.AudioChain;

    public IEnumerable<Finding> Evaluate(DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Obs?.AudioSampleRate != 44100)
        {
            yield break;
        }

        var tally = new EvidenceTally();
        var settings = context.ByCategoryInWindow(ScanEventCodes.ObsAudioSettings);
        if (settings.Count > 0)
        {
            tally.Support("obs-audio-setting", settings[^1]);
        }

        tally.Support("obs-document", new ScanEvent
        {
            Timestamp = context.Obs.FirstTimestamp ?? DateTimeOffset.UtcNow,
            Source = ScanSource.ObsLog,
            Module = ScanModule.ObsConfig,
            Category = ScanEventCodes.ObsAudioSettings,
            Severity = Severity.Advisory,
            Message = "OBS audio is configured at 44.1 kHz.",
            Payload = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ScanEventPayload.Rate] = "44100",
            }),
        });

        yield return new Finding
        {
            HypothesisId = Id,
            Module = ScanModule.AudioChain,
            Title = "OBS is running audio at 44.1 kHz",
            Explanation = "Most USB interfaces and capture cards run at 48 kHz. When OBS resamples 44.1 kHz audio from a 48 kHz device you get crackle, pitch drift, or a delay that grows over the stream.",
            Severity = Severity.Advisory,
            Confidence = tally.Score(BaseWeight),
            Evidence = tally.Evidence,
            RecommendedAction = "Set OBS Settings → Audio → Sample Rate to 48 kHz, then restart OBS.",
            Data = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target"] = "obs-audio",
                [ScanEventPayload.Rate] = "44100",
            }),
        };
    }
}
