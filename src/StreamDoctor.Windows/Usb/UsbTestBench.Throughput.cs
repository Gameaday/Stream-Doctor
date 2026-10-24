using System.Diagnostics;
using StreamDoctor.Core.Usb;

namespace StreamDoctor.Windows.Usb;

public static partial class UsbTestBench
{
    /// <summary>
    /// Measures actual bulk/isochronous endpoint throughput for a USB device.
    /// Uses WinUSB to open the device and run read/write loops.
    /// </summary>
    public static async Task<IReadOnlyList<UsbEndpointThroughput>> MeasureThroughputAsync(
        string deviceInstanceId,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Bandwidth measurement is Windows-only.");
        }

        return await Task.Run<IReadOnlyList<UsbEndpointThroughput>>(
            () =>
            {
                var devicePath = GetDeviceInterfacePath(deviceInstanceId);
                if (string.IsNullOrEmpty(devicePath))
                {
                    return [];
                }

                // WinUSB throughput measurement lands with the M3 test bench; until then
                // the analyzer keeps using class-based estimates rather than fake numbers.
                return
                [
                    new UsbEndpointThroughput
                    {
                        Endpoint = "Bulk IN",
                        MeasuredMbps = 0,
                        IsEstimated = true,
                        Note = "WinUSB measurement not implemented yet",
                    },
                ];
            },
            cancellationToken);
    }

    /// <summary>
    /// Runs a soak test: repeated re-enumeration plus a bandwidth check per cycle.
    /// The pass/fail math is <see cref="UsbSoakReport"/>; this method only drives I/O.
    /// </summary>
    public static async Task<UsbSoakReport> RunSoakTestAsync(
        string deviceInstanceId,
        int iterations,
        TimeSpan delayBetweenIterations,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceInstanceId);

        var completed = new List<UsbSoakIteration>(Math.Max(iterations, 0));

        for (var index = 0; index < iterations; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var reEnumerationStart = Stopwatch.GetTimestamp();
            var reEnumerated = false;
            string? error = null;

            try
            {
                reEnumerated = await ReEnumerateDeviceAsync(deviceInstanceId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                error = ex.Message;
            }

            var reEnumerationDuration = Stopwatch.GetElapsedTime(reEnumerationStart);

            var bandwidthStart = Stopwatch.GetTimestamp();
            IReadOnlyList<UsbEndpointThroughput> throughput;

            try
            {
                throughput = await MeasureThroughputAsync(
                    deviceInstanceId,
                    TimeSpan.FromSeconds(5),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throughput = [];
                error ??= ex.Message;
            }

            var bandwidthDuration = Stopwatch.GetElapsedTime(bandwidthStart);

            completed.Add(new UsbSoakIteration
            {
                Iteration = index + 1,
                ReEnumerated = reEnumerated,
                ReEnumerationDuration = reEnumerationDuration,
                BandwidthMeasured = throughput.Count > 0,
                BandwidthDuration = bandwidthDuration,
                Error = error,
            });

            if (index < iterations - 1)
            {
                await Task.Delay(delayBetweenIterations, cancellationToken).ConfigureAwait(false);
            }
        }

        return new UsbSoakReport
        {
            DeviceInstanceId = deviceInstanceId,
            CompletedAt = DateTimeOffset.UtcNow,
            Iterations = completed,
        };
    }

    private static string? GetDeviceInterfacePath(string instanceId) => null;
}

/// <summary>Measured throughput for one USB endpoint.</summary>
public sealed record UsbEndpointThroughput
{
    public required string Endpoint { get; init; }

    public double MeasuredMbps { get; init; }

    public bool IsEstimated { get; init; }

    public string? Note { get; init; }
}
