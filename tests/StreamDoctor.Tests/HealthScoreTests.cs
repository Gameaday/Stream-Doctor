using StreamDoctor.Core.Diagnosis;
using StreamDoctor.Core.Model;

namespace StreamDoctor.Tests;

public sealed class HealthScoreTests
{
    [Fact]
    public void Compute_WithNoFindings_IsPerfect()
    {
        Assert.Equal(HealthScore.Maximum, HealthScore.Compute([]));
    }

    [Fact]
    public void Compute_AdvisoryCostsSixPointsAtFullConfidence()
    {
        Assert.Equal(94, HealthScore.Compute([Finding(Severity.Advisory, 1.0)]));
    }

    [Fact]
    public void Compute_ScalesThePenaltyWithConfidence()
    {
        // 45 * (0.4 + 0.6 * 0.5) = 31.5, rounded away from zero.
        Assert.Equal(69, HealthScore.Compute([Finding(Severity.Critical, 0.5)]));
    }

    [Fact]
    public void Compute_FloorsAtZero()
    {
        var findings = Enumerable.Range(0, 4).Select(_ => Finding(Severity.Critical, 1.0)).ToList();

        Assert.Equal(0, HealthScore.Compute(findings));
    }

    [Fact]
    public void Compute_IgnoresInfoFindings()
    {
        Assert.Equal(HealthScore.Maximum, HealthScore.Compute([Finding(Severity.Info, 1.0)]));
    }

    [Theory]
    [InlineData(100, HealthState.Green)]
    [InlineData(85, HealthState.Green)]
    [InlineData(84, HealthState.Yellow)]
    [InlineData(60, HealthState.Yellow)]
    [InlineData(59, HealthState.Red)]
    [InlineData(0, HealthState.Red)]
    public void StateFor_MapsThresholds(int score, HealthState expected)
    {
        Assert.Equal(expected, HealthScore.StateFor(score, anyModuleScanned: true));
    }

    [Fact]
    public void StateFor_WithNoModulesScanned_IsUnknown()
    {
        Assert.Equal(HealthState.Unknown, HealthScore.StateFor(100, anyModuleScanned: false));
    }

    [Fact]
    public void ModuleHealth_UnscannedModuleIsUnknownNotGreen()
    {
        var modules = ModuleHealthCalculator.Compute([ScanModule.UsbSentinel], []);

        var usb = Assert.Single(modules, module => module.Module == ScanModule.UsbSentinel);
        Assert.Equal(HealthState.Green, usb.State);
        Assert.True(usb.WasScanned);

        var audio = Assert.Single(modules, module => module.Module == ScanModule.AudioChain);
        Assert.Equal(HealthState.Unknown, audio.State);
        Assert.False(audio.WasScanned);
    }

    [Theory]
    [InlineData(Severity.Advisory, HealthState.Yellow)]
    [InlineData(Severity.Warning, HealthState.Yellow)]
    [InlineData(Severity.Critical, HealthState.Red)]
    [InlineData(Severity.Info, HealthState.Green)]
    public void ModuleHealth_UsesTheWorstFinding(Severity severity, HealthState expected)
    {
        var finding = Finding(severity, 1.0) with { Module = ScanModule.FramePacing };

        var modules = ModuleHealthCalculator.Compute([ScanModule.FramePacing], [finding]);
        var framePacing = Assert.Single(modules, module => module.Module == ScanModule.FramePacing);

        Assert.Equal(expected, framePacing.State);
        Assert.Equal(1, framePacing.FindingCount);
        Assert.Equal(severity, framePacing.HighestSeverity);
    }

    private static Finding Finding(Severity severity, double confidence) => new()
    {
        HypothesisId = "test.finding",
        Module = ScanModule.UsbSentinel,
        Title = "Test finding",
        Explanation = "Test explanation",
        Severity = severity,
        Confidence = confidence,
        Evidence = [],
    };
}
