using StreamDoctor.Core.Diagnosis;
using StreamDoctor.Core.Model;

namespace StreamDoctor.Tests;

public sealed class SystemRuleTests
{
    [Fact]
    public void DiskSpaceRule_FlagsDrivesThatWillFillUp()
    {
        var report = TestEvents.Analyze(new DiagnosisContext
        {
            Events =
            [
                TestEvents.Disk("C:", freeGb: 500, totalGb: 2000),
                TestEvents.Disk("D:", freeGb: 5, totalGb: 2000),
                TestEvents.Disk("E:", freeGb: 25, totalGb: 200),
            ],
        });

        var findings = report.Findings.Where(finding => finding.HypothesisId == "disk.low-space").ToList();

        Assert.Equal(2, findings.Count);
        Assert.Contains(findings, finding => finding.Severity == Severity.Critical && finding.Data["target"] == "D:");
        Assert.Contains(findings, finding => finding.Severity == Severity.Warning && finding.Data["target"] == "E:");
        Assert.All(findings, finding => Assert.Equal(ScanModule.DiskRecording, finding.Module));
    }

    [Fact]
    public void DiskSpaceRule_WithOnlyHealthyDrives_IsSilent()
    {
        var report = TestEvents.Analyze(new DiagnosisContext
        {
            Events = [TestEvents.Disk("C:", freeGb: 900, totalGb: 2000)],
        });

        Assert.DoesNotContain(report.Findings, finding => finding.HypothesisId == "disk.low-space");
    }

    [Fact]
    public void WindowsInterferenceRule_OnlyCountsEventsInsideTheWindow()
    {
        var inside = TestEvents.WindowsEvent(TestData.At(21), ScanEventCodes.WindowsUpdateInstalled);
        var outside = TestEvents.WindowsEvent(TestData.At(10), ScanEventCodes.WindowsUpdateInstalled);

        var report = TestEvents.Analyze(new DiagnosisContext
        {
            Events = [inside, outside],
            WindowStart = TestData.At(20),
            WindowEnd = TestData.At(22),
        });

        var finding = Assert.Single(
            report.Findings,
            candidate => candidate.HypothesisId.StartsWith("windows.interference", StringComparison.Ordinal));

        Assert.Single(finding.Evidence);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Contains("1x", finding.Title, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowsInterferenceRule_EscalatesAPowerEventToCritical()
    {
        var report = TestEvents.Analyze(new DiagnosisContext
        {
            Events = [TestEvents.WindowsEvent(TestData.At(21), ScanEventCodes.SystemPowerEvent)],
            WindowStart = TestData.At(20),
            WindowEnd = TestData.At(22),
        });

        var finding = Assert.Single(
            report.Findings,
            candidate => candidate.HypothesisId.Contains(ScanEventCodes.SystemPowerEvent, StringComparison.Ordinal));

        Assert.Equal(Severity.Critical, finding.Severity);
        Assert.Equal(ScanModule.WindowsInterference, finding.Module);
    }

    [Fact]
    public void WindowsInterferenceRule_WithNoWindow_CountsEverythingInTheEvidenceSet()
    {
        var report = TestEvents.Analyze(new DiagnosisContext
        {
            Events = [TestEvents.WindowsEvent(TestData.At(10), ScanEventCodes.AppCrashed)],
        });

        Assert.Contains(report.Findings, candidate => candidate.HypothesisId.Contains(ScanEventCodes.AppCrashed, StringComparison.Ordinal));
    }
}
