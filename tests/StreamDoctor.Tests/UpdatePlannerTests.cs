using StreamDoctor.Core.Fixes;
using StreamDoctor.Core.Updates;

namespace StreamDoctor.Tests;

public sealed class UpdatePlannerTests
{
    [Fact]
    public void Plan_AutoAppliesAPatchLevelApplicationUpdate()
    {
        var plan = UpdatePlanner.Plan(
            [Candidate("OBSProject.OBSStudio", "31.0.1", "31.0.2")],
            UpdatePolicy.Safe);

        var update = Assert.Single(plan.Updates);
        Assert.Equal(UpdateDecision.AutoApply, update.Decision);
        Assert.Equal(PlaybookIds.UpdatePlaybook("OBSProject.OBSStudio"), update.PlaybookId);
        Assert.Contains("Patch-level", update.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_RequiresApprovalForAMajorUpgrade()
    {
        var plan = UpdatePlanner.Plan([Candidate("Some.App", "30.1.0", "31.0.0")]);

        var update = Assert.Single(plan.Updates);
        Assert.Equal(UpdateDecision.ApprovalRequired, update.Decision);
        Assert.Contains("major", update.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Plan_RequiresApprovalForWindowsPatches()
    {
        var driver = Candidate("driver-123", null, null) with
        {
            Kind = UpdateKind.Driver,
            Source = UpdateSource.WindowsUpdate,
            AvailableVersion = "10.0.1",
        };
        var firmware = Candidate("firmware-9", null, null) with
        {
            Kind = UpdateKind.Firmware,
            Source = UpdateSource.WindowsUpdate,
            AvailableVersion = "2.1",
        };
        var patch = Candidate("KB5000001", null, null) with
        {
            Kind = UpdateKind.WindowsPatch,
            Source = UpdateSource.WindowsUpdate,
            AvailableVersion = "1.0",
            Severity = UpdateSeverity.Security,
        };

        var plan = UpdatePlanner.Plan([driver, firmware, patch]);

        Assert.All(plan.Updates, update => Assert.Equal(UpdateDecision.ApprovalRequired, update.Decision));
        Assert.All(plan.Updates, update => Assert.NotNull(update.PlaybookId));
    }

    [Fact]
    public void Plan_BlocksAKnownBadTargetAndFlagsTheInstalledVersion()
    {
        var issue = new KnownIssue
        {
            Id = "obs-31.0.2",
            PackageId = "OBSProject.OBSStudio",
            AffectedVersions = VersionRange.Parse("31.0.2"),
            Summary = "Encoder hangs on scene switch.",
            SafeVersionHint = "31.0.3",
        };

        var blocked = UpdatePlanner.Plan(
            [Candidate("OBSProject.OBSStudio", "31.0.1", "31.0.2")],
            new UpdatePolicy { KnownIssues = [issue] });

        var blockedUpdate = Assert.Single(blocked.Updates);
        Assert.Equal(UpdateDecision.Blocked, blockedUpdate.Decision);
        Assert.Null(blockedUpdate.PlaybookId);
        Assert.Contains("known-bad", blockedUpdate.Reason, StringComparison.Ordinal);

        var installedBad = UpdatePlanner.Plan(
            [Candidate("OBSProject.OBSStudio", "31.0.2", "31.0.3")],
            new UpdatePolicy { KnownIssues = [issue] });

        var installedUpdate = Assert.Single(installedBad.Updates);
        Assert.NotNull(installedUpdate.InstalledIssue);
        Assert.Equal(UpdateDecision.AutoApply, installedUpdate.Decision);
    }

    [Fact]
    public void Plan_NotifiesOnlyForVendorTools()
    {
        var vendor = Candidate("elgato-control-center", null, null) with
        {
            Kind = UpdateKind.Firmware,
            Source = UpdateSource.VendorTool,
            VendorToolName = "Elgato Control Center",
            AvailableVersion = null,
        };

        var update = Assert.Single(UpdatePlanner.Plan([vendor]).Updates);

        Assert.Equal(UpdateDecision.NotifyOnly, update.Decision);
        Assert.Null(update.PlaybookId);
        Assert.Contains("Elgato Control Center", update.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_RespectsTheAutoUpdateLimit()
    {
        var plan = UpdatePlanner.Plan(
            [
                Candidate("a.app", "1.0.0", "1.0.1"),
                Candidate("b.app", "1.0.0", "1.0.1"),
                Candidate("c.app", "1.0.0", "1.0.1"),
            ],
            new UpdatePolicy { MaxAutoUpdatesPerSweep = 1 });

        Assert.Single(plan.AutoApplicable);
        Assert.Equal(2, plan.ApprovalRequired.Count());
    }

    [Fact]
    public void Plan_DeduplicatesPackagesPreferringTheVerifiedSource()
    {
        var winget = Candidate("obs.project", "31.0.1", "31.0.2");
        var manual = winget with { Source = UpdateSource.Manual };

        var update = Assert.Single(UpdatePlanner.Plan([manual, winget]).Updates);

        Assert.Equal(UpdateSource.Winget, update.Candidate.Source);
    }

    [Fact]
    public void Plan_WithNoCandidates_IsEmpty()
    {
        Assert.Empty(UpdatePlanner.Plan([]).Updates);
    }

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
