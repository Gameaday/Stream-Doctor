using StreamDoctor.Core.Fixes;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Updates;

namespace StreamDoctor.Core.Diagnosis.Rules;

/// <summary>
/// Turns the update plan into findings: patch-level app updates are safe to offer,
/// drivers/firmware/patches always need a click, vendor firmware is guidance only,
/// and known-bad targets are blocked with an explanation.
/// </summary>
public sealed partial class UpdateAvailableRule : IFindingRule
{
    private const double GuaranteedBaseWeight = 1.0;
    private const double GuidedBaseWeight = 0.3;

    public string Id => "update.available";

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
            var candidate = planned.Candidate;
            var tally = BuildTally(context, candidate, planned);

            yield return new Finding
            {
                HypothesisId = $"{Id}.{candidate.Kind.ToString().ToLowerInvariant()}",
                Module = ModuleFor(candidate.Kind),
                Title = BuildTitle(candidate, planned),
                Explanation = BuildExplanation(candidate, planned),
                Severity = MapSeverity(candidate, planned),
                Confidence = tally.Score(candidate.IsGuaranteedSource ? GuaranteedBaseWeight : GuidedBaseWeight),
                Evidence = tally.Evidence,
                FixPlaybookId = planned.PlaybookId,
                RecommendedAction = planned.Decision == UpdateDecision.NotifyOnly
                    ? candidate.VendorToolName is null
                        ? "Download the update from the vendor and verify it yourself; Stream Doctor cannot vouch for it."
                        : $"Open {candidate.VendorToolName} and check for updates there."
                    : null,
                Data = BuildData(candidate, planned),
            };
        }
    }

    private static double GuidesWeight(UpdateCandidate candidate) =>
        candidate.IsGuaranteedSource ? GuaranteedBaseWeight : GuidedBaseWeight;

    private static EvidenceTally BuildTally(DiagnosisContext context, UpdateCandidate candidate, PlannedUpdate planned)
    {
        var tally = new EvidenceTally();

        var observed = context.ByCategoryInWindow(ScanEventCodes.UpdateAvailable)
            .FirstOrDefault(evidence => string.Equals(
                evidence.GetPayload(ScanEventPayload.UpdateId), candidate.Id, StringComparison.OrdinalIgnoreCase));

        if (observed is not null)
        {
            tally.Support($"observed:{candidate.Id}", observed);
        }

        var timestamp = context.WindowEnd
            ?? (context.Events.Count > 0 ? context.Events[^1].Timestamp : DateTimeOffset.UtcNow);

        var planEvent = new ScanEvent
        {
            Timestamp = timestamp,
            Source = ScanSource.App,
            Module = ModuleFor(candidate.Kind),
            Category = planned.Decision == UpdateDecision.Blocked
                ? ScanEventCodes.UpdateBlocked
                : ScanEventCodes.UpdateAvailable,
            Severity = Severity.Info,
            Message = planned.Reason,
            Payload = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ScanEventPayload.UpdateId] = candidate.Id,
                [ScanEventPayload.UpdateDecision] = planned.Decision.ToString(),
            },
        };

        tally.Support($"decision:{candidate.Id}:{planned.Decision}", planEvent);
        return tally;
    }
}
