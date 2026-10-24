using StreamDoctor.Windows.Usb;

namespace StreamDoctor.Windows.Fixes;

public sealed partial class WindowsChangeApplier
{
    private static readonly TimeSpan ReEnumerationTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Power-cycles one device through the test bench. The guard has already refused
    /// input devices, hubs and controllers before this runs.
    /// </summary>
    private static string ReEnumerateDevice(string deviceInstanceId)
    {
        var completed = UsbTestBench
            .ReEnumerateDeviceAsync(deviceInstanceId)
            .Wait(ReEnumerationTimeout);

        if (!completed)
        {
            throw new TimeoutException($"Re-enumeration of '{deviceInstanceId}' did not finish in {ReEnumerationTimeout.TotalSeconds:0} s.");
        }

        return deviceInstanceId;
    }
}
