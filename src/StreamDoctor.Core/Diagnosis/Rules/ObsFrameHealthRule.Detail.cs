using System.Collections.ObjectModel;
using System.Globalization;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Obs;

namespace StreamDoctor.Core.Diagnosis.Rules;

public sealed partial class ObsFrameHealthRule
{
    private static Finding Create(DiagnosisContext context, ObsSession session, FrameFailure failure)
    {
        var tally = new EvidenceTally();
        tally.Support($"session:{session.StartedAt:O}", SessionEvidence(session, failure.HypothesisId));

        var matching = context.Events
            .Where(evidence => string.Equals(evidence.Category, failure.Category, StringComparison.Ordinal))
            .Where(evidence => evidence.Timestamp >= session.StartedAt)
            .Where(evidence => session.EndedAt is null || evidence.Timestamp <= session.EndedAt)
            .OrderBy(evidence => evidence.Timestamp)
            .ToList();

        if (matching.Count > 0)
        {
            tally.Support($"log:{failure.Category}", matching[^1]);
        }

        var percentText = failure.Percent.ToString("0.0#", CultureInfo.InvariantCulture) + "%";

        return new Finding
        {
            HypothesisId = failure.HypothesisId,
            Module = ScanModule.FramePacing,
            Title = $"Your stream {failure.Mode} {percentText} of its frames last session",
            Explanation = failure.Explanation,
            Severity = failure.Severity,
            Confidence = tally.Score(BaseWeight),
            Evidence = tally.Evidence,
            RecommendedAction = failure.Action,
            Data = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target"] = $"stream:{session.StartedAt:O}",
                [ScanEventPayload.Percent] = percentText,
                [ScanEventPayload.TotalFrames] = (session.Stats.TotalFrames ?? 0).ToString(CultureInfo.InvariantCulture),
            }),
        };
    }

    private static ScanEvent SessionEvidence(ObsSession session, string hypothesisId) => new()
    {
        Timestamp = session.EndedAt ?? session.StartedAt,
        Source = ScanSource.ObsLog,
        Module = ScanModule.FramePacing,
        Category = hypothesisId,
        Severity = Severity.Info,
        Message = $"Session lasted {session.Duration.TotalMinutes.ToString("0", CultureInfo.InvariantCulture)} minutes and handled {session.Stats.TotalFrames ?? 0} frames.",
        Payload = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ScanEventPayload.Count] = (session.Stats.TotalFrames ?? 0).ToString(CultureInfo.InvariantCulture),
            ["session.start"] = session.StartedAt.ToString("O", CultureInfo.InvariantCulture),
        }),
    };

    private static int EffectiveTotal(ObsSessionStats stats) => stats.TotalFrames is > 0
        ? stats.TotalFrames.Value
        : stats.AttemptedFrames is > 0
            ? stats.AttemptedFrames.Value
            : Math.Max((stats.DroppedFrames ?? 0) + (stats.LaggedFrames ?? 0) + (stats.SkippedFrames ?? 0) + 1, 1);

    private static double PercentOf(double? reported, int count, int total) =>
        reported ?? (count * 100d / Math.Max(total, 1));

    private static Severity SeverityFor(double percent, double warning, double critical) =>
        percent >= critical ? Severity.Critical : percent >= warning ? Severity.Warning : Severity.Advisory;

    private sealed record FrameFailure(
        string Category,
        string HypothesisId,
        string Mode,
        double Percent,
        Severity Severity,
        string Explanation,
        string Action);
}
