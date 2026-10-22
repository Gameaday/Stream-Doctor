using System.Diagnostics;
using System.Globalization;

namespace StreamDoctor.Windows.Fixes;

public sealed partial class WindowsChangeApplier
{
    private static string? GetInstalledVersion(string packageId)
    {
        var output = RunProcess("winget", $"list --id {packageId} --accept-source-agreements")!;
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[1].Equals(packageId, StringComparison.OrdinalIgnoreCase))
            {
                return parts.Length > 2 ? parts[2] : null;
            }
        }
        return null;
    }

    private static string? BackupConfig(string sourcePath)
    {
        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException($"Config directory not found: {sourcePath}");
        }

        var backupDir = Path.Combine(
            Path.GetTempPath(),
            "StreamDoctorBackups",
            $"config-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Path.GetFileName(sourcePath)}");

        Directory.CreateDirectory(backupDir);
        CopyDirectory(sourcePath, backupDir);
        return backupDir;
    }

    private static string? RestoreConfig(string targetPath, string? backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath) || !Directory.Exists(backupPath))
        {
            return null;
        }

        if (Directory.Exists(targetPath))
        {
            Directory.Delete(targetPath, recursive: true);
        }

        CopyDirectory(backupPath, targetPath);
        return backupPath;
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
        }
        foreach (var dir in Directory.GetDirectories(source))
        {
            CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
        }
    }

    private static string? UpdateSoftware(string packageId, string desiredVersion)
    {
        var output = RunProcess("winget", $"upgrade --id {packageId} --version {desiredVersion} --accept-source-agreements --accept-package-agreements --silent")!;
        return desiredVersion;
    }

    private static string? InstallWindowsUpdate(string updateId, string desiredVersion)
    {
        var output = RunProcess("powershell", $"-Command \"Install-WindowsUpdate -KBArticleID {updateId} -AcceptAll -AutoReboot\"")!;
        return desiredVersion;
    }

    private static string? CreateRestorePoint(string description)
    {
        var sequence = RunProcess("powershell", $"-Command \"Checkpoint-Computer -Description '{description}' -RestorePointType Modify_Settings\"")!;
        return description;
    }
}