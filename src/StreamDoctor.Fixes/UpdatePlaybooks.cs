using StreamDoctor.Core.Fixes;
using StreamDoctor.Core.Updates;

namespace StreamDoctor.Fixes;

/// <summary>
/// Builds concrete playbooks for update candidates. Only OS-verified sources get a
/// playbook; vendor firmware stays guidance-only.
/// </summary>
public static class UpdatePlaybooks
{
    /// <summary>Description prefix used for the restore point a driver update creates.</summary>
    public const string RestorePointPrefix = "Stream Doctor before ";

    public static FixPlaybook? Create(UpdateCandidate candidate, string? configBackupSource = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!candidate.IsGuaranteedSource || !candidate.HasUpdate)
        {
            return null;
        }

        return candidate.Kind switch
        {
            UpdateKind.Application => Application(candidate, configBackupSource),
            UpdateKind.Driver => WindowsPackage(candidate, "driver"),
            UpdateKind.Firmware => WindowsPackage(candidate, "firmware"),
            UpdateKind.WindowsPatch => WindowsPackage(candidate, "Windows patch"),
            _ => null,
        };
    }

    /// <summary>True when the target is a patch-level bump over the installed version.</summary>
    public static bool IsPatchLevel(UpdateCandidate candidate) =>
        SoftwareVersion.TryParse(candidate.InstalledVersion, out var installed) &&
        SoftwareVersion.TryParse(candidate.AvailableVersion, out var available) &&
        available.IsPatchLevelUpgradeFrom(installed);

    private static FixPlaybook Application(UpdateCandidate candidate, string? configBackupSource)
    {
        var patchLevel = IsPatchLevel(candidate);
        var actions = new List<FixAction>();

        if (!string.IsNullOrWhiteSpace(configBackupSource))
        {
            actions.Add(new FixAction
            {
                Kind = FixActionKind.ConfigBackup,
                Target = configBackupSource,
                Description = $"Back up {configBackupSource} before updating {candidate.Name}",
                RequiresElevation = false,
            });
        }

        actions.Add(new FixAction
        {
            Kind = FixActionKind.SoftwareUpdate,
            Target = candidate.Id,
            DesiredValue = candidate.AvailableVersion,
            RollbackValue = candidate.InstalledVersion,
            Description = $"Upgrade {candidate.Name} to {candidate.AvailableVersion} through winget",
            RequiresElevation = true,
        });

        return new FixPlaybook
        {
            Id = PlaybookIds.UpdatePlaybook(candidate.Id),
            Title = $"Update {candidate.Name} to {candidate.AvailableVersion}",
            Summary = $"Installs {candidate.Name} {candidate.AvailableVersion} with winget (installer signatures are verified by Windows)." +
                      (configBackupSource is null ? string.Empty : " Your configuration is copied to a timestamped backup first."),
            Risk = patchLevel ? FixRisk.Low : FixRisk.Medium,
            AutoFixSupported = patchLevel,
            Actions = actions,
            Prerequisites =
            [
                $"Close {candidate.Name} before updating",
                "You are not live",
            ],
            VerificationHint = "Stream Doctor reads the installed version back after the upgrade; a mismatch fails the fix and restores the backup.",
        };
    }

    private static FixPlaybook WindowsPackage(UpdateCandidate candidate, string label)
    {
        var restorePoint = RestorePointPrefix + candidate.Name;

        return new FixPlaybook
        {
            Id = PlaybookIds.UpdatePlaybook(candidate.Id),
            Title = $"Install {label}: {candidate.Name}",
            Summary = $"Installs '{candidate.Name}' through Windows Update. Windows only ships WHQL-signed packages here, so the binary is verified by the OS.",
            Risk = FixRisk.High,
            AutoFixSupported = false,
            Actions =
            [
                new FixAction
                {
                    Kind = FixActionKind.RestorePoint,
                    Target = restorePoint,
                    Description = "Create a System Restore point",
                    RequiresElevation = true,
                },
                new FixAction
                {
                    Kind = FixActionKind.WindowsUpdate,
                    Target = candidate.Id,
                    DesiredValue = candidate.AvailableVersion,
                    RollbackValue = candidate.InstalledVersion,
                    Description = $"Install '{candidate.Name}' from Windows Update",
                    RequiresElevation = true,
                },
            ],
            Prerequisites =
            [
                "You are not live",
                "OBS and games are closed",
                "System Protection is enabled so the restore point succeeds",
            ],
            VerificationHint = "Windows Update reports success; if the device misbehaves, roll back with the restore point Stream Doctor created.",
        };
    }
}
