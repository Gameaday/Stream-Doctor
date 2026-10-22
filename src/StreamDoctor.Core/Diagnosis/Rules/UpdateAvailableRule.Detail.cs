using StreamDoctor.Core.Model;
using StreamDoctor.Core.Updates;

namespace StreamDoctor.Core.Diagnosis.Rules;

public sealed partial class UpdateAvailableRule
{
    private static string BuildTitle(UpdateCandidate candidate, PlannedUpdate planned) => planned.Decision switch
    {
        UpdateDecision.Blocked => $"Hold off: {candidate.Name} {candidate.AvailableVersion} is known-bad",
        UpdateDecision.NotifyOnly => $"{candidate.Name}: check {candidate.VendorToolName ?? "the vendor"} for updates",
        _ => candidate.Kind switch
        {
            UpdateKind.Driver => $"Driver update available: {candidate.Name}",
            UpdateKind.Firmware => $"Firmware update available: {candidate.Name}",
            UpdateKind.WindowsPatch => $"Windows update available: {candidate.Name}",
            _ => $"{candidate.Name} {candidate.AvailableVersion} is available" +
                 (candidate.InstalledVersion is null ? string.Empty : $" (you have {candidate.InstalledVersion})"),
        },
    };

    private static string BuildExplanation(UpdateCandidate candidate, PlannedUpdate planned)
    {
        var source = candidate.Source switch
        {
            UpdateSource.Winget => "winget, which verifies installer signatures for Windows",
            UpdateSource.WindowsUpdate => "Windows Update, which only ships WHQL-signed packages",
            UpdateSource.VendorTool => candidate.VendorToolName ?? "a vendor tool",
            _ => "the vendor",
        };

        var why = planned.Decision switch
        {
            UpdateDecision.AutoApply => "Stream Doctor can apply it safely: it is an OS-verified patch-level update and the change is journaled so it can be undone.",
            UpdateDecision.ApprovalRequired => "Stream Doctor applies it only after you approve, and takes a restore point or config backup first.",
            UpdateDecision.NotifyOnly => "Stream Doctor will not change this automatically because it cannot verify the package.",
            UpdateDecision.Blocked => "Stream Doctor refuses to install this version.",
            _ => string.Empty,
        };

        return $"Available through {source}. {why} {planned.Reason}";
    }

    private static Severity MapSeverity(UpdateCandidate candidate, PlannedUpdate planned)
    {
        if (planned.Decision == UpdateDecision.Blocked || planned.InstalledIssue is not null)
        {
            return Severity.Warning;
        }

        return candidate.Severity switch
        {
            UpdateSeverity.Critical or UpdateSeverity.Security => Severity.Warning,
            UpdateSeverity.Recommended => Severity.Advisory,
            _ => Severity.Info,
        };
    }

    private static ScanModule ModuleFor(UpdateKind kind) => kind switch
    {
        UpdateKind.Driver or UpdateKind.Firmware => ScanModule.Peripherals,
        UpdateKind.WindowsPatch => ScanModule.WindowsInterference,
        _ => ScanModule.StreamingSoftware,
    };

    private static Dictionary<string, string> BuildData(UpdateCandidate candidate, PlannedUpdate planned) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["target"] = candidate.Id,
            [ScanEventPayload.UpdateId] = candidate.Id,
            [ScanEventPayload.UpdateKind] = candidate.Kind.ToString(),
            [ScanEventPayload.UpdateSource] = candidate.Source.ToString(),
            [ScanEventPayload.UpdateDecision] = planned.Decision.ToString(),
            [ScanEventPayload.InstalledVersion] = candidate.InstalledVersion ?? string.Empty,
            [ScanEventPayload.AvailableVersion] = candidate.AvailableVersion ?? string.Empty,
            [ScanEventPayload.VendorTool] = candidate.VendorToolName ?? string.Empty,
        };
}
