namespace StreamDoctor.Core.Updates;

public static partial class UpdatePlanner
{
    private static UpdateDecision Decide(
        UpdateCandidate candidate,
        UpdatePolicy policy,
        int autoApplied,
        out string reason)
    {
        if (!CanFixAutomatically(candidate.Kind))
        {
            reason = "Stream Doctor has no verified update path for this kind of update.";
            return UpdateDecision.NotifyOnly;
        }

        if (autoApplied >= Math.Max(policy.MaxAutoUpdatesPerSweep, 0))
        {
            reason = $"Auto-update limit of {policy.MaxAutoUpdatesPerSweep} reached for this sweep; approve it manually.";
            return UpdateDecision.ApprovalRequired;
        }

        return candidate.Kind switch
        {
            UpdateKind.Application => DecideApplication(candidate, policy, out reason),
            UpdateKind.Driver => Approval("Driver updates always need your approval so a bad driver never lands unattended.", out reason),
            UpdateKind.Firmware => Approval("Firmware updates always need your approval; a failed flash can brick the device.", out reason),
            UpdateKind.WindowsPatch => Approval("Windows patches are installed with a restore point, but only after you approve.", out reason),
            _ => Approval("Stream Doctor does not install this update automatically.", out reason),
        };
    }

    private static UpdateDecision Approval(string why, out string reason)
    {
        reason = why;
        return UpdateDecision.ApprovalRequired;
    }

    private static UpdateDecision DecideApplication(UpdateCandidate candidate, UpdatePolicy policy, out string reason)
    {
        if (!policy.AutoApplyApplications)
        {
            reason = "Automatic application updates are disabled in your update policy.";
            return UpdateDecision.ApprovalRequired;
        }

        if (!SoftwareVersion.TryParse(candidate.InstalledVersion, out var installed) ||
            !SoftwareVersion.TryParse(candidate.AvailableVersion, out var available))
        {
            reason = "The installed or available version could not be compared, so this one needs a manual decision.";
            return UpdateDecision.ApprovalRequired;
        }

        if (policy.AutoApplyPatchLevelOnly && !available.IsPatchLevelUpgradeFrom(installed))
        {
            reason = $"This is a major upgrade ({installed} to {available}), not a patch. Approve it manually.";
            return UpdateDecision.ApprovalRequired;
        }

        reason = $"Patch-level update from {installed} to {available} via winget (signature-verified by Windows).";
        return UpdateDecision.AutoApply;
    }

    private static bool CanFixAutomatically(UpdateKind kind) => kind is
        UpdateKind.Application or UpdateKind.Driver or UpdateKind.Firmware or UpdateKind.WindowsPatch;

    private static KnownIssue? FindTargetIssue(UpdateCandidate candidate, UpdatePolicy policy) =>
        policy.KnownIssues.FirstOrDefault(issue =>
            string.Equals(issue.PackageId, candidate.Id, StringComparison.OrdinalIgnoreCase) &&
            issue.Matches(candidate.AvailableVersion));

    private static KnownIssue? FindInstalledIssue(UpdateCandidate candidate, UpdatePolicy policy) =>
        policy.KnownIssues.FirstOrDefault(issue =>
            string.Equals(issue.PackageId, candidate.Id, StringComparison.OrdinalIgnoreCase) &&
            issue.Matches(candidate.InstalledVersion));
}
