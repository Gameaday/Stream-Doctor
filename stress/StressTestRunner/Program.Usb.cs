using System.Diagnostics;
using StreamDoctor.Windows.Usb;

namespace StreamDoctor.StressTest;

public static partial class Program
{
    private static async Task<int> RunUsbReEnumerationStressAsync(StressConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.TargetDeviceInstanceId))
        {
            Console.Error.WriteLine("ERROR: --device is required for USB re-enumeration.");
            return 2;
        }

        Console.WriteLine($"[USB RE-ENUMERATION] device: {config.TargetDeviceInstanceId}");
        Console.WriteLine($"{config.Iterations} cycle(s), delay {config.DelayBetweenIterations.TotalSeconds:0}s");
        Console.WriteLine("WARNING: the device will disappear for a few seconds each cycle.");
        Console.WriteLine();

        var results = new List<UsbReEnumerationResult>();
        var successes = 0;

        for (var i = 1; i <= config.Iterations; i++)
        {
            Console.WriteLine($"[{i}/{config.Iterations}] re-enumerating...");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var success = await UsbTestBench.ReEnumerateDeviceAsync(config.TargetDeviceInstanceId, CancellationToken.None);
                stopwatch.Stop();

                if (success)
                {
                    successes++;
                }

                results.Add(new UsbReEnumerationResult
                {
                    Iteration = i,
                    Success = success,
                    Duration = stopwatch.Elapsed,
                });

                Console.WriteLine($"  {(success ? "OK" : "FAILED")} in {stopwatch.Elapsed.TotalSeconds:0.0}s");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                results.Add(new UsbReEnumerationResult
                {
                    Iteration = i,
                    Success = false,
                    Duration = stopwatch.Elapsed,
                    Error = ex.Message,
                });
                Console.WriteLine($"  EXCEPTION: {ex.Message}");
            }

            await DelayBetween(config, i);
        }

        var summary = new UsbReEnumerationSummary
        {
            DeviceInstanceId = config.TargetDeviceInstanceId,
            GeneratedAt = DateTimeOffset.Now,
            TotalIterations = config.Iterations,
            Successful = successes,
            Failed = config.Iterations - successes,
            SuccessRate = config.Iterations == 0 ? 0 : (double)successes / config.Iterations,
            AverageDurationSeconds = results.Count == 0 ? 0 : results.Average(static result => result.Duration.TotalSeconds),
            Results = results,
        };

        WriteJsonOutput(summary, config.OutputFile);
        Console.WriteLine();
        Console.WriteLine($"Summary: {successes}/{config.Iterations} re-enumerated ({summary.SuccessRate:P1}), avg {summary.AverageDurationSeconds:0.0}s");
        return successes == config.Iterations ? 0 : 1;
    }

    private static async Task<int> RunUsbBandwidthStressAsync(StressConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.TargetDeviceInstanceId))
        {
            Console.Error.WriteLine("ERROR: --device is required for USB bandwidth measurement.");
            return 2;
        }

        Console.WriteLine($"[USB BANDWIDTH] device: {config.TargetDeviceInstanceId}");
        Console.WriteLine($"{config.Iterations} measurement(s) of {config.BandwidthDuration.TotalSeconds:0}s");
        Console.WriteLine();

        var results = new List<UsbBandwidthResult>();

        for (var i = 1; i <= config.Iterations; i++)
        {
            Console.WriteLine($"[{i}/{config.Iterations}] measuring...");
            var stopwatch = Stopwatch.StartNew();

            var throughput = await UsbTestBench.MeasureThroughputAsync(
                config.TargetDeviceInstanceId,
                config.BandwidthDuration,
                CancellationToken.None);

            stopwatch.Stop();
            results.Add(new UsbBandwidthResult
            {
                Iteration = i,
                Throughput = throughput,
                Duration = stopwatch.Elapsed,
            });

            if (throughput.Count == 0)
            {
                Console.WriteLine(
                    "  no measurable endpoints (device has no WinUSB/raw I/O interface, or the path is not implemented yet)");
            }

            foreach (var endpoint in throughput)
            {
                Console.WriteLine(
                    $"  {endpoint.Endpoint}: {endpoint.MeasuredMbps:0.00} MB/s" +
                    (endpoint.IsEstimated ? $" (estimated: {endpoint.Note})" : string.Empty));
            }

            await DelayBetween(config, i);
        }

        WriteSummary(results, config.OutputFile);
        return 0;
    }

    private static async Task DelayBetween(StressConfig config, int iteration)
    {
        if (config.DelayBetweenIterations > TimeSpan.Zero && iteration < config.Iterations)
        {
            await Task.Delay(config.DelayBetweenIterations);
        }
    }
}
