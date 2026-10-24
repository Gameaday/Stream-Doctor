using System.Diagnostics;
using System.Runtime.InteropServices;
using StreamDoctor.Core.Model;

namespace StreamDoctor.StressTest;

/// <summary>
/// Stress test runner for Stream Doctor. Must run on a Windows client PC with
/// administrator rights; every mode is read-only except the USB test bench modes.
/// </summary>
public static partial class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("==============================================================");
        Console.WriteLine("        Stream Doctor Stress Test Runner v1.0");
        Console.WriteLine("==============================================================");
        Console.WriteLine();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.Error.WriteLine("ERROR: This stress test must run on Windows.");
            return 1;
        }

        if (!IsElevated())
        {
            Console.Error.WriteLine("ERROR: Stress tests require administrator privileges.");
            Console.Error.WriteLine("       Start an elevated PowerShell/cmd and run it again.");
            return 1;
        }

        var config = ParseArgs(args);

        try
        {
            return config.Mode switch
            {
                StressMode.PreFlight => await RunPreFlightStressAsync(config),
                StressMode.PostFlight => await RunPostFlightStressAsync(config),
                StressMode.UsbReEnumeration => await RunUsbReEnumerationStressAsync(config),
                StressMode.UsbBandwidth => await RunUsbBandwidthStressAsync(config),
                StressMode.Soak => await RunSoakTestAsync(config),
                StressMode.Full => await RunFullStressAsync(config),
                _ => 1,
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FATAL: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static StressConfig ParseArgs(string[] args)
    {
        var config = new StressConfig();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--mode":
                    config.Mode = Enum.Parse<StressMode>(args[++i], ignoreCase: true);
                    break;
                case "--device":
                    config.TargetDeviceInstanceId = args[++i];
                    break;
                case "--iterations":
                    config.Iterations = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--delay":
                    config.DelayBetweenIterations = TimeSpan.FromSeconds(int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case "--duration":
                    config.BandwidthDuration = TimeSpan.FromSeconds(int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case "--output":
                    config.OutputFile = args[++i];
                    break;
                case "--obs-logs":
                    config.ObsLogDirectory = args[++i];
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument '{args[i]}'. Use --help.");
                    Environment.Exit(2);
                    break;
            }
        }

        return config;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Usage: StreamDoctor.StressTest.exe --mode <mode> [options]

            Modes:
              preflight        Read-only pre-flight sweep, repeated N times
              postflight       Post-flight debrief against recent OBS logs
              usbreenum        Re-enumerate one USB device N times (DESTRUCTIVE for that device)
              usbbandwidth     Measure throughput for one USB device N times
              soak             Combined re-enumeration + bandwidth cycles
              full             preflight, then the three USB suites (needs --device)

            Options:
              --device <id>        Device instance ID, e.g. USB\VID_046D&PID_0825\6&12345678&0&2
              --iterations <n>     Iterations (default 10)
              --delay <seconds>    Delay between iterations (default 5)
              --duration <seconds> Bandwidth measurement window (default 10)
              --output <file>      Write JSON results to this path
              --obs-logs <dir>     Override the OBS log directory
              --help               Show this help

            Safety:
              * Never target a keyboard, mouse, gamepad, hub or host controller.
              * Close OBS and games; the device will disappear for a few seconds.
              * The runner refuses to start unless elevated.

            Examples:
              StreamDoctor.StressTest.exe --mode preflight --iterations 20 --output preflight.json
              StreamDoctor.StressTest.exe --mode usbreenum --device "USB\VID_0FD9&PID_0066\..." --iterations 20
              StreamDoctor.StressTest.exe --mode soak --device "USB\VID_0FD9&PID_0066\..." --iterations 50 --delay 10
            """);

        Environment.Exit(0);
    }

    private static bool IsElevated()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
}
