using System.Diagnostics;
using System.Security.Principal;

namespace StreamDoctor.Windows.Fixes;

public sealed partial class WindowsChangeApplier
{
    private const int ProcessTimeoutMilliseconds = 20000;
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
