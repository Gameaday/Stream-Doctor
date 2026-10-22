using StreamDoctor.Core.Fixes;

namespace StreamDoctor.Core.Updates;

/// <summary>
/// Decides what to do with each available update. This is the "guarantee" gate:
/// only OS-verified sources are eligible, known-bad targets are blocked, and
/// drivers, firmware and Windows patches always need a human click by default.
/// </summary>
public static partial class UpdatePlanner
{
    public static UpdatePlan Plan(IEnumerable<UpdateCandidate> candidates, UpdatePolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var effectivePolicy = policy ?? UpdatePolicy.Safe;
        var planned = new List<PlannedUpdate>();
        var autoApplied = 0;

        foreach (var candidate in Deduplicate(candidates))
        {
            var installedIssue = FindInstalledIssue(candidate, effectivePolicy);

            if (!candidate.IsGuaranteedSource)
            {
                planned.Add(new PlannedUpdate(
                    candidate,
                    UpdateDecision.NotifyOnly,
                    candidate.VendorToolName is null
                        ? "This update is not delivered by winget or Windows Update, so Stream Doctor cannot verify its signature."
                        : $"Open {candidate.VendorToolName} to check for this update; Stream Doctor cannot verify vendor firmware signatures.",
                    PlaybookId: null,
                    installedIssue));
                continue;
            }

            if (!candidate.HasUpdate)
            {
                planned.Add(new PlannedUpdate(
                    candidate,
                    UpdateDecision.NotifyOnly,
                    "No newer version is reported right now.",
                    PlaybookId: null,
                    installedIssue));
                continue;
            }

            var targetIssue = FindTargetIssue(candidate, effectivePolicy);
            if (targetIssue is not null)
            {
                planned.Add(new PlannedUpdate(
                    candidate,
                    UpdateDecision.Blocked,
                    $"The available version {candidate.AvailableVersion} is on the known-bad list: {targetIssue.Summary}" +
                    (targetIssue.SafeVersionHint is null ? string.Empty : $" Safe version: {targetIssue.SafeVersionHint}."),
                    PlaybookId: null,
                    installedIssue));
                continue;
            }

            var decision = Decide(candidate, effectivePolicy, autoApplied, out var reason);
            if (decision == UpdateDecision.AutoApply)
            {
                autoApplied++;
            }

            planned.Add(new PlannedUpdate(
                candidate,
                decision,
                reason,
                decision == UpdateDecision.NotifyOnly ? null : PlaybookIds.UpdatePlaybook(candidate.Id),
                installedIssue));
        }

        return new UpdatePlan
        {
            Updates = planned
                .OrderByDescending(static update => update.Candidate.Severity)
                .ThenBy(static update => update.Candidate.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
    }

    /// <summary>One row per package: prefer the OS-verified source and the newest target.</summary>
    private static IEnumerable<UpdateCandidate> Deduplicate(IEnumerable<UpdateCandidate> candidates) =>
        candidates
            .Where(static candidate => !string.IsNullOrWhiteSpace(candidate.Id))
            .GroupBy(static candidate => candidate.Id, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group
                .OrderByDescending(static candidate => candidate.IsGuaranteedSource)
                .ThenByDescending(static candidate => ParseOrZero(candidate.AvailableVersion))
                .First());

    private static SoftwareVersion ParseOrZero(string? version) =>
        SoftwareVersion.TryParse(version, out var parsed) ? parsed : default;
}
