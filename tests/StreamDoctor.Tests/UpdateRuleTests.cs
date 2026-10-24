using StreamDoctor.Core.Diagnosis;
using StreamDoctor.Core.Fixes;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Updates;

namespace StreamDoctor.Tests;

public sealed class UpdateRuleTests
{
    [Fact]
    public void UpdateRule_OffersAPatchLevelApplicationUpdateWithAPlaybook()
    {
        var candidate = Candidate("OBSProject.OBSStudio", "31.0.1", "31.0.2");

        var report = TestEvents.Analyze(Context(candidate));

        var finding = Assert.Single(report.Findings, item => item.HypothesisId == "update.available.application");
        Assert.Equal(ScanModule.StreamingSoftware, finding.Module);
        Assert.Equal(Severity.Advisory, finding.Severity);
        Assert.Equal(PlaybookIds.UpdatePlaybook(candidate.Id), finding.FixPlaybookId);
        Assert.Equal(1.0, finding.Confidence, precision: 3);
    }

    [Fact]
    public void UpdateRule_KeepsDriversInThePeripheralsModuleWithApproval()
    {
        var driver = Candidate("driver-abc", "10.0.0", "10.0.1") with
        {
            Kind = UpdateKind.Driver,
            Source = UpdateSource.WindowsUpdate,
        };

        var report = TestEvents.Analyze(Context(driver));

        var finding = Assert.Single(report.Findings, item => item.HypothesisId == "update.available.driver");
        Assert.Equal(ScanModule.Peripherals, finding.Module);
        Assert.Equal(UpdateDecision.ApprovalRequired.ToString(), finding.Data[ScanEventPayload.UpdateDecision]);
        Assert.NotNull(finding.FixPlaybookId);
    }

    [Fact]
    public void UpdateRule_MentionsARestartWhenThePackageNeedsOne()
    {
        var candidate = Candidate("driver-x", "10.0.0", "10.0.1") with
        {
            Kind = UpdateKind.Driver,
            Source = UpdateSource.WindowsUpdate,
            RequiresRestart = true,
        };

        var report = TestEvents.Analyze(Context(candidate));

        var finding = Assert.Single(report.Findings, item => item.HypothesisId == "update.available.driver");
        Assert.Contains("restart", finding.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("after the stream", finding.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdateRule_LeavesVendorFirmwareAsGuidanceOnly()
    {
        var vendor = Candidate("elgato", null, null) with
        {
            Kind = UpdateKind.Firmware,
            Source = UpdateSource.VendorTool,
            VendorToolName = "Elgato Control Center",
            AvailableVersion = null,
        };

        var report = TestEvents.Analyze(Context(vendor));

        var finding = Assert.Single(report.Findings, item => item.HypothesisId == "update.available.firmware");
        Assert.Null(finding.FixPlaybookId);
        Assert.Contains("Elgato Control Center", finding.RecommendedAction!, StringComparison.Ordinal);
        Assert.True(finding.Confidence < 0.8);
    }

    [Fact]
    public void UpdateRule_ExplainsABlockedKnownBadTarget()
    {
        var issue = new KnownIssue
        {
            Id = "obs-31.0.2",
            PackageId = "OBSProject.OBSStudio",
            AffectedVersions = VersionRange.Parse("31.0.2"),
            Summary = "Encoder hangs on scene switch.",
            SafeVersionHint = "31.0.3",
        };

        var report = TestEvents.Analyze(
            Context(Candidate("OBSProject.OBSStudio", "31.0.1", "31.0.2"), new UpdatePolicy { KnownIssues = [issue] }));

        var finding = Assert.Single(report.Findings, item => item.HypothesisId == "update.available.application");
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Null(finding.FixPlaybookId);
        Assert.Contains("known-bad", finding.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Encoder hangs", finding.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void InstalledKnownIssueRule_FlagsTheVersionTheUserRunsToday()
    {
        var issue = new KnownIssue
        {
            Id = "obs-31.0.1",
            PackageId = "OBSProject.OBSStudio",
            AffectedVersions = VersionRange.Parse("31.0.1"),
            Summary = "Random crashes on startup.",
            Severity = UpdateSeverity.Security,
            SafeVersionHint = "31.0.2",
        };

        var report = TestEvents.Analyze(
            Context(Candidate("OBSProject.OBSStudio", "31.0.1", "31.0.2"), new UpdatePolicy { KnownIssues = [issue] }));

        var finding = Assert.Single(report.Findings, item => item.HypothesisId == "update.installed-known-issue");
        Assert.Equal(Severity.Critical, finding.Severity);
        Assert.Contains("31.0.2", finding.RecommendedAction!, StringComparison.Ordinal);
        Assert.NotNull(finding.FixPlaybookId);
    }

    [Fact]
    public void UpdateRules_AreSilentWithoutAPlan()
    {
        var report = TestEvents.Analyze(new DiagnosisContext { Events = [] });

        Assert.DoesNotContain(report.Findings, item => item.HypothesisId.StartsWith("update.", StringComparison.Ordinal));
    }

    private static DiagnosisContext Context(UpdateCandidate candidate) => new()
    {
        Events = [],
        Updates = [candidate],
        UpdatePlan = UpdatePlanner.Plan([candidate]),
    };

    private static DiagnosisContext Context(UpdateCandidate candidate, UpdatePolicy policy) => new()
    {
        Events = [],
        Updates = [candidate],
        UpdatePlan = UpdatePlanner.Plan([candidate], policy),
    };

    private static UpdateCandidate Candidate(string id, string? installed, string? available) => new()
    {
        Id = id,
        Name = id,
        Kind = UpdateKind.Application,
        Source = UpdateSource.Winget,
        InstalledVersion = installed,
        AvailableVersion = available,
    };
}
