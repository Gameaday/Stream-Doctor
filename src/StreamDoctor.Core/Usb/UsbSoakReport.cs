namespace StreamDoctor.Core.Usb;

/// <summary>One cycle of a USB soak test.</summary>
public sealed record UsbSoakIteration
{
    public required int Iteration { get; init; }

    public required bool ReEnumerated { get; init; }

    public TimeSpan ReEnumerationDuration { get; init; }

    public bool BandwidthMeasured { get; init; }

    public TimeSpan BandwidthDuration { get; init; }

    public string? Error { get; init; }
}

/// <summary>
/// Result of a soak test. The math lives in Core so the pass/fail policy is unit
/// tested; the Windows test bench only performs the device I/O.
/// </summary>
public sealed record UsbSoakReport
{
    public const double DefaultMinimumSuccessRate = 0.95;

    public required string DeviceInstanceId { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }

    public IReadOnlyList<UsbSoakIteration> Iterations { get; init; } = [];

    public double SuccessRate => Iterations.Count == 0
        ? 0
        : Iterations.Count(static iteration => iteration.ReEnumerated) / (double)Iterations.Count;

    public int SuccessfulIterations => Iterations.Count(static iteration => iteration.ReEnumerated);

    public TimeSpan TotalTime => TimeSpan.FromTicks(
        Iterations.Sum(static iteration => (iteration.ReEnumerationDuration + iteration.BandwidthDuration).Ticks));

    public TimeSpan AverageReEnumerationTime => Iterations.Count == 0
        ? TimeSpan.Zero
        : TimeSpan.FromTicks((long)Iterations.Average(static iteration => iteration.ReEnumerationDuration.Ticks));

    /// <summary>True when the observed success rate clears the threshold.</summary>
    public bool MeetsThreshold(double minimumSuccessRate = DefaultMinimumSuccessRate) =>
        Iterations.Count > 0 && SuccessRate >= minimumSuccessRate;

    public static UsbSoakReport Empty(string deviceInstanceId, DateTimeOffset completedAt) => new()
    {
        DeviceInstanceId = deviceInstanceId,
        CompletedAt = completedAt,
    };
}
