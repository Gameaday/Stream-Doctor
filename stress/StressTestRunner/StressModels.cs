using System.Diagnostics;

using StreamDoctor.Windows.Usb;

namespace StreamDoctor.StressTest;

public enum StressMode
{
    PreFlight,
    PostFlight,
    UsbReEnumeration,
    UsbBandwidth,
    Soak,
    Full,
}

public sealed class StressConfig
{
    public StressMode Mode { get; set; } = StressMode.PreFlight;
    public string? TargetDeviceInstanceId { get; set; }
    public int Iterations { get; set; } = 10;
    public TimeSpan DelayBetweenIterations { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan BandwidthDuration { get; set; } = TimeSpan.FromSeconds(10);
    public string? OutputFile { get; set; }
    public string? ObsLogDirectory { get; set; }
}

public sealed class PreFlightStressResult
{
    public int Iteration { get; set; }
    public int HealthScore { get; set; }
    public string HealthState { get; set; } = "";
    public int FindingCount { get; set; }
    public int CriticalFindings { get; set; }
    public int WarningFindings { get; set; }
    public int EvidenceCount { get; set; }
    public int UpdateCount { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public List<string> Failures { get; set; } = [];

    public string StateLabel => HealthState switch
    {
        "Green" => "GREEN",
        "Yellow" => "YELLOW",
        "Red" => "RED",
        _ => "UNKNOWN",
    };
}

public sealed class PostFlightStressResult
{
    public int Iteration { get; set; }
    public int HealthScore { get; set; }
    public string HealthState { get; set; } = "";
    public int FindingCount { get; set; }
    public bool HasSession { get; set; }
    public TimeSpan SessionDuration { get; set; }
    public int FollowUpChecks { get; set; }
    public string Headline { get; set; } = "";
    public TimeSpan Duration { get; set; }

    public string StateLabel => HealthState switch
    {
        "Green" => "GREEN",
        "Yellow" => "YELLOW",
        "Red" => "RED",
        _ => "UNKNOWN",
    };
}

public sealed class UsbReEnumerationResult
{
    public int Iteration { get; set; }
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
    public string? Error { get; set; }
}

public sealed class UsbReEnumerationSummary
{
    public required string DeviceInstanceId { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
    public int TotalIterations { get; init; }
    public int Successful { get; init; }
    public int Failed { get; init; }
    public double SuccessRate { get; init; }
    public double AverageDurationSeconds { get; init; }
    public List<UsbReEnumerationResult> Results { get; init; } = [];
}

public sealed class UsbBandwidthResult
{
    public int Iteration { get; set; }
    public IReadOnlyList<UsbEndpointThroughput> Throughput { get; set; } = [];
    public TimeSpan Duration { get; set; }
}