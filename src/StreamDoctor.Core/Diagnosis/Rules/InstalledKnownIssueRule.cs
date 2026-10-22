using System.Collections.ObjectModel;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Updates;

namespace StreamDoctor.Core.Diagnosis.Rules;

/// <summary>
/// Flags an installed version that is on the known-bad list. Knowing a version is
/// bad is only useful if the app says so before the next stream, not after a crash.
/// </summary>
public sealed class InstalledKnownIssueRule : IFindingRule
{
    private const double BaseWeight = 0.6;

    public string Id => "update.installed-known-issue";

    public ScanModule OwnerModule => ScanModule.StreamingSoftware;

    public IEnumerable<Finding> Evaluate(DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.UpdatePlan is null)
        {
            yield break;
        }

        foreach (var planned in context.UpdatePlan.Updates)
        {
            var issue = planned.InstalledIssue;
            if (issue is null)
            {
                continue;
            }

            var candidate = planned.Candidate;
            var tally = new EvidenceTally();
            var evidence = new ScanEvent
            {
                Timestamp = context.WindowEnd ?? (context.Events.Count > 0 ? context.Events[^1].Timestamp : DateTimeOffset.UtcNow),
                Source = ScanSource.App,
                Module = ScanModule.StreamingSoftware,
                Category = ScanEventCodes.UpdateInstalledKnownBad,
                Severity = Severity.Warning,
                Message = $"{candidate.Name} {candidate.InstalledVersion} matches known issue {issue.Id}: {issue.Summary}",
                Payload = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ScanEventPayload.UpdateId] = candidate.Id,
                    [ScanEventPayload.InstalledVersion] = candidate.InstalledVersion ?? string.Empty,
                    ["knownIssue"] = issue.Id,
                }),
            };

            tally.Support($"known-bad:{candidate.Id}:{issue.Id}", evidence);

            yield return new Finding
            {
                HypothesisId = Id,
                Module = ScanModule.StreamingSoftware,
                Title = $"{candidate.Name} {candidate.InstalledVersion} is a known-bad version",
                Explanation = $"The version you have installed matches a known issue: {issue.Summary}",
                Severity = issue.Severity is UpdateSeverity.Critical or UpdateSeverity.Security
                    ? Severity.Critical
                    : Severity.Warning,
                Confidence = tally.Score(BaseWeight),
                Evidence = tally.Evidence,
                FixPlaybookId = planned.PlaybookId,
                RecommendedAction = issue.SafeVersionHint is null
                    ? "Update to a version the knowledge base marks as safe before you go live."
                    : $"Update to {issue.SafeVersionHint} (marked safe by the knowledge base) before you go live.",
                Data = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target"] = candidate.Id,
                    [ScanEventPayload.UpdateId] = candidate.Id,
                    [ScanEventPayload.InstalledVersion] = candidate.InstalledVersion ?? string.Empty,
                }),
            };
        }
    }
}
