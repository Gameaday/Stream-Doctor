using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace StreamDoctor.Windows.Usb;

/// <summary>
/// Active USB test bench: re-enumeration, bandwidth measurement, soak testing.
/// All methods are Windows-only and require elevation.
/// </summary>
[SupportedOSPlatform("windows10.0.17763.0")]
public static partial class UsbTestBench
{
    private const uint DnHasProblem = 0x00000400;

    /// <summary>
    /// Soft-disables and re-enables a USB device (equivalent to unplug/replug).
    /// A cycle only counts as successful when the device actually left the bus and
    /// came back; the previous eject-only implementation could report success
    /// without changing anything.
    /// </summary>
    public static async Task<bool> ReEnumerateDeviceAsync(
        string deviceInstanceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceInstanceId);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Re-enumeration is Windows-only.");
        }

        if (!await IsElevatedAsync())
        {
            throw new UnauthorizedAccessException("Re-enumeration requires administrator privileges.");
        }

        return await Task.Run(() => Cycle(deviceInstanceId), cancellationToken).ConfigureAwait(false);
    }

    private static bool Cycle(string deviceInstanceId)
    {
        if (CfgMgr32.CM_Locate_DevNodeW(out var devInst, deviceInstanceId, 0) != CfgMgr32.CR_SUCCESS)
        {
            return false;
        }

        if (!IsReady(devInst))
        {
            // A device that is already broken cannot show a clean transition.
            return false;
        }

        var disabled = CfgMgr32.CM_Disable_DevNode(devInst, 0) == CfgMgr32.CR_SUCCESS;
        if (!disabled)
        {
            // Fallback for stacks that reject a disable but support eject; the
            // resulting transition is still verified below.
            _ = CfgMgr32.CM_Request_Device_EjectW(devInst, out _, null, 0, 0);
        }

        if (!WaitUntil(() => !IsReady(devInst), TimeSpan.FromSeconds(8)))
        {
            return false;
        }

        if (CfgMgr32.CM_Enable_DevNode(devInst, 0) != CfgMgr32.CR_SUCCESS)
        {
            if (CfgMgr32.CM_Get_ParentW(out var parent, devInst, 0) == CfgMgr32.CR_SUCCESS)
            {
                _ = CfgMgr32.CM_Setup_DevNodeW(parent, 0);
            }
        }

        return WaitUntil(() => IsReady(devInst), TimeSpan.FromSeconds(15));
    }

    private static bool IsReady(uint devInst) =>
        CfgMgr32.CM_Get_DevNode_StatusW(out var status, out _, devInst, 0) == CfgMgr32.CR_SUCCESS &&
        (status & DnHasProblem) == 0;

    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var start = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(start) < timeout)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(100);
        }

        return condition();
    }

    private static async Task<bool> IsElevatedAsync()
    {
        return await Task.Run(() =>
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }).ConfigureAwait(false);
    }

    /// <summary>P/Invoke for the CFGMGR32 entry points CsWin32 does not generate.</summary>
    private static class CfgMgr32
    {
        private const string DllName = "cfgmgr32.dll";

        internal const int CR_SUCCESS = 0x00000000;

        [DllImport(DllName, CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int CM_Locate_DevNodeW(out uint pdnDevInst, string pDeviceID, uint ulFlags);

        [DllImport(DllName, CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int CM_Disable_DevNode(uint dnDevInst, uint ulFlags);

        [DllImport(DllName, CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int CM_Enable_DevNode(uint dnDevInst, uint ulFlags);

        [DllImport(DllName, CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int CM_Request_Device_EjectW(uint dnDevInst, out uint pVetoType, string? pszVetoName, int ulVetoNameLength, uint ulFlags);

        [DllImport(DllName, CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int CM_Get_DevNode_StatusW(out uint pulStatus, out uint pulProblemNumber, uint dnDevInst, uint ulFlags);

        [DllImport(DllName, CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int CM_Get_ParentW(out uint pdnDevInst, uint dnDevInst, uint ulFlags);

        [DllImport(DllName, CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int CM_Setup_DevNodeW(uint dnDevInst, uint ulFlags);
    }
}
