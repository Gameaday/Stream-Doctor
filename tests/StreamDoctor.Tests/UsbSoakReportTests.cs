using StreamDoctor.Core.Usb;

namespace StreamDoctor.Tests;

public sealed class UsbSoakReportTests
{
    [Fact]
    public void EmptyReport_HasNoSuccessAndFailsTheThreshold()
    {
        var report = UsbSoakReport.Empty(@"USB\VID_0000&PID_0001", TestData.At(12));

        Assert.Equal(0, report.SuccessRate);
        Assert.Equal(0, report.SuccessfulIterations);
        Assert.Equal(TimeSpan.Zero, report.TotalTime);
        Assert.False(report.MeetsThreshold());
    }

    [Fact]
    public void SuccessRate_IsTheFractionOfReEnumeratedCycles()
    {
        var report = Report(iterations: 10, successes: 9);

        Assert.Equal(9, report.SuccessfulIterations);
        Assert.Equal(0.9, report.SuccessRate, precision: 3);
        Assert.False(report.MeetsThreshold());
        Assert.True(report.MeetsThreshold(0.9));
    }

    [Fact]
    public void FullySuccessfulSoak_PassesTheDefaultThreshold()
    {
        var report = Report(iterations: 20, successes: 20);

        Assert.Equal(1.0, report.SuccessRate, precision: 3);
        Assert.True(report.MeetsThreshold());
    }

    [Fact]
    public void TotalTime_SumsEveryDuration()
    {
        var report = new UsbSoakReport
        {
            DeviceInstanceId = "dev",
            CompletedAt = TestData.At(12),
            Iterations =
            [
                new UsbSoakIteration
                {
                    Iteration = 1,
                    ReEnumerated = true,
                    ReEnumerationDuration = TimeSpan.FromSeconds(2),
                    BandwidthMeasured = true,
                    BandwidthDuration = TimeSpan.FromSeconds(5),
                },
                new UsbSoakIteration
                {
                    Iteration = 2,
                    ReEnumerated = true,
                    ReEnumerationDuration = TimeSpan.FromSeconds(3),
                    BandwidthMeasured = false,
                    BandwidthDuration = TimeSpan.FromSeconds(5),
                },
            ],
        };

        Assert.Equal(TimeSpan.FromSeconds(15), report.TotalTime);
        Assert.Equal(TimeSpan.FromSeconds(2.5), report.AverageReEnumerationTime);
    }

    [Fact]
    public void IterationFailuresAreCarriedForTheReport()
    {
        var report = new UsbSoakReport
        {
            DeviceInstanceId = "dev",
            CompletedAt = TestData.At(12),
            Iterations =
            [
                new UsbSoakIteration
                {
                    Iteration = 1,
                    ReEnumerated = false,
                    ReEnumerationDuration = TimeSpan.FromSeconds(1),
                    Error = "access denied",
                },
            ],
        };

        Assert.Equal("access denied", report.Iterations[0].Error);
        Assert.False(report.MeetsThreshold());
    }

    private static UsbSoakReport Report(int iterations, int successes) => new()
    {
        DeviceInstanceId = @"USB\VID_0000&PID_0001",
        CompletedAt = TestData.At(12),
        Iterations = Enumerable.Range(1, iterations)
            .Select(index => new UsbSoakIteration
            {
                Iteration = index,
                ReEnumerated = index <= successes,
                ReEnumerationDuration = TimeSpan.FromSeconds(2),
                BandwidthMeasured = index <= successes,
                BandwidthDuration = TimeSpan.FromSeconds(1),
            })
            .ToList(),
    };
}
