using StreamDoctor.Core.Usb;
using StreamDoctor.Windows.Usb;

namespace StreamDoctor.StressTest;

public static partial class Program
{
    private static async Task<int> RunSoakTestAsync(StressConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.TargetDeviceInstanceId))
        {
            Console.Error.WriteLine("ERROR: --device is required for the soak test.");
            return 2;
        }

        Console.WriteLine($"[SOAK TEST] device: {config.TargetDeviceInstanceId}");
        Console.WriteLine($"{config.Iterations} cycle(s) of re-enumeration + bandwidth, delay {config.DelayBetweenIterations.TotalSeconds:0}s");
        Console.WriteLine();

        var result = await UsbTestBench.RunSoakTestAsync(
            config.TargetDeviceInstanceId,
            config.Iterations,
            config.DelayBetweenIterations,
            CancellationToken.None);

        Console.WriteLine();
        Console.WriteLine("Soak test result:");
        Console.WriteLine($"  device        : {result.DeviceInstanceId}");
        Console.WriteLine($"  iterations    : {result.Iterations.Count}");
        Console.WriteLine($"  success rate  : {result.SuccessRate:P1}");
        Console.WriteLine($"  total time    : {result.TotalTime}");

        foreach (var iteration in result.Iterations)
        {
            Console.WriteLine(
                $"    #{iteration.Iteration}: re-enumeration {(iteration.ReEnumerated ? "OK" : "FAILED")} " +
                $"({iteration.ReEnumerationDuration.TotalSeconds:0.0}s), bandwidth " +
                $"{(iteration.BandwidthMeasured ? "OK" : "n/a")} ({iteration.BandwidthDuration.TotalSeconds:0.0}s)");
        }

        WriteJsonOutput(new { Result = result, GeneratedAt = DateTimeOffset.Now }, config.OutputFile);
        return result.MeetsThreshold() ? 0 : 1;
    }

    private static async Task<int> RunFullStressAsync(StressConfig config)
    {
        Console.WriteLine("[FULL] pre-flight sweep followed by the USB suites");
        Console.WriteLine();

        var overall = true;

        if (await RunPreFlightStressAsync(config) != 0)
        {
            overall = false;
        }

        if (string.IsNullOrWhiteSpace(config.TargetDeviceInstanceId))
        {
            Console.WriteLine();
            Console.WriteLine("No --device supplied; skipping the USB test bench suites.");
        }
        else
        {
            if (await RunUsbReEnumerationStressAsync(config) != 0)
            {
                overall = false;
            }

            if (await RunUsbBandwidthStressAsync(config) != 0)
            {
                overall = false;
            }

            if (await RunSoakTestAsync(config) != 0)
            {
                overall = false;
            }
        }

        Console.WriteLine();
        Console.WriteLine("==============================================================");
        Console.WriteLine($"FULL STRESS TEST: {(overall ? "PASSED" : "FAILED")}");
        Console.WriteLine("==============================================================");
        return overall ? 0 : 1;
    }
}
