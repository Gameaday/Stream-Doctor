using StreamDoctor.Core.Diagnosis;
using StreamDoctor.Core.Diagnosis.Rules;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Obs;

namespace StreamDoctor.Core.PostFlight;

/// <summary>The post-stream debrief: what happened, what it cost, what to check next.</summary>
public sealed record PostFlightDebrief
{
    public required DiagnosisReport Diagnosis { get; init; }

    /// <summary>The session the debrief is about, or null when no session was found.</summary>
    public required ObsSession? Session { get; init; }

    /// <summary>Merged, window-scoped timeline for the evidence UI.</summary>
    public required IReadOnlyList<ScanEvent> Timeline { get; init; }

    /// <summary>One sentence the dashboard can show without any other context.</summary>
    public required string Headline { get; init; }

    /// <summary>Checks derived from this debrief, to run before the next stream.</summary>
    public required IReadOnlyList<string> FollowUpChecks { get; init; }

    public IReadOnlyList<Finding> Findings => Diagnosis.Findings;
}

/// <summary>
/// Post-flight analysis (design doc §10): scope every correlation to the OBS session
/// window, then run the same rules engine the pre-flight sweep uses.
/// </summary>
public sealed class PostFlightAnalyzer
{
    private const int MaxTimelineItems = 500;
    private const int MaxFollowUps = 6;

    private readonly DiagnosisEngine _engine;

    public PostFlightAnalyzer(DiagnosisEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    public PostFlightDebrief Analyze(
        DiagnosisContext context,
        ObsLogDocument? obs = null,
        ObsSession? session = null,
        DateTimeOffset? generatedAt = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var document = obs ?? context.Obs;
        var target = session ?? document?.LastStreamSession;

        var scoped = context with
        {
            Obs = document,
            WindowStart = target?.StartedAt ?? context.WindowStart,
            WindowEnd = target?.EndedAt ?? context.WindowEnd,
            ScannedModules = context.ScannedModules is { Count: > 0 } declared
                ? declared
                : BuildScannedModules(document),
        };

        var diagnosis = _engine.Analyze(scoped, generatedAt ?? scoped.WindowEnd ?? DateTimeOffset.UtcNow);

        var timeline = scoped.Events
            .Where(scoped.IsInWindow)
            .OrderBy(static evidence => evidence.Timestamp)
            .ThenBy(static evidence => evidence.Sequence)
            .ToList();

        if (timeline.Count > MaxTimelineItems)
        {
            timeline = timeline.TakeLast(MaxTimelineItems).ToList();
        }

        return new PostFlightDebrief
        {
            Diagnosis = diagnosis,
            Session = target,
            Timeline = timeline,
            Headline = BuildHeadline(target, diagnosis),
            FollowUpChecks = BuildFollowUps(diagnosis),
        };
    }

    private static string BuildHeadline(ObsSession? session, DiagnosisReport diagnosis)
    {
        if (session is null)
        {
            return "No stream session found in the OBS logs. Point Stream Doctor at your OBS log folder and try again.";
        }

        var minutes = session.Duration.TotalMinutes.ToString("0", System.Globalization.CultureInfo.InvariantCulture);

        if (diagnosis.Findings.Count == 0)
        {
            return $"Clean session: {minutes} minutes live with no problems found.";
        }

        var top = diagnosis.Findings[0];
        return $"{top.Title}. {diagnosis.Findings.Count} issue(s) found after {minutes} minutes live.";
    }

    private static List<string> BuildFollowUps(DiagnosisReport diagnosis) =>
        diagnosis.Findings
            .Take(MaxFollowUps)
            .Select(static finding => finding.IsActionable
                ? $"Before going live: apply '{finding.Title}' and confirm the fix."
                : $"Before going live: {finding.Title}.")
            .ToList();

    private static HashSet<ScanModule> BuildScannedModules(ObsLogDocument? document)
    {
        var scanned = new HashSet<ScanModule>();
        if (document is not null)
        {
            scanned.Add(ScanModule.FramePacing);
            scanned.Add(ScanModule.ObsConfig);
            if (document.Sessions.Count > 0)
            {
                scanned.Add(ScanModule.CaptureCard);
                scanned.Add(ScanModule.StreamingSoftware);
            }
        }

        return scanned;
    }
}

/// <summary>Convenience composition used by the app and by tests.</summary>
public static class DefaultRuleSet
{
    public static IReadOnlyList<IFindingRule> Create() =>
    [
        new UsbBandwidthRule(),
        new UsbPowerRule(),
        new UsbDropoutRule(),
        new UsbTopologyChangeRule(),
        new UsbDeviceProblemRule(),
        new ObsFrameHealthRule(),
        new ObsPluginFailureRule(),
        new ObsSampleRateRule(),
        new DiskSpaceRule(),
        new WindowsInterferenceRule(),
    ];
}
