using System.Diagnostics;
using System.Security.Principal;

namespace StreamDoctor.Windows.Fixes;

public sealed partial class WindowsChangeApplier
{
    private const int ProcessTimeoutMilliseconds = 20000;

    private static string ReadActivePowerPlan()
    {
        var output = RunProcess("powercfg", "/getactivescheme");
        var start = output.IndexOf('{');
        var end = output.IndexOf('}');

        return start >= 0 && end > start
            ? output[(start + 1)..end]
            : throw new InvalidOperationException("powercfg did not report an active scheme.");
    }

    private static void SetActivePowerPlan(string scheme)
    {
        if (!Guid.TryParse(scheme.Trim('{', '}'), out var guid))
        {
            throw new ArgumentException($"'{scheme}' is not a power scheme GUID.", nameof(scheme));
        }

        RunProcess("powercfg", $"/setactive {guid:D}");
    }

    private static string RunProcess(string fileName, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException($"Could not start {fileName}.");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();

        if (!process.WaitForExit(ProcessTimeoutMilliseconds))
        {
            process.Kill();
            throw new TimeoutException($"{fileName} did not finish within {ProcessTimeoutMilliseconds} ms.");
        }

        return process.ExitCode == 0
            ? stdout
            : throw new InvalidOperationException($"{fileName} exited with {process.ExitCode}: {stderr.Trim()}");
    }
}

/// <summary>Checks whether the current process can perform machine-wide changes.</summary>
public static class WindowsElevation
{
    public static bool IsElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
