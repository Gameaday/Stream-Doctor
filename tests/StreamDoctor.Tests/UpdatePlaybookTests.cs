using StreamDoctor.Core.Fixes;
using StreamDoctor.Core.Updates;
using StreamDoctor.Fixes;

namespace StreamDoctor.Tests;

public sealed class UpdatePlaybookTests
{
    [Fact]
    public void Create_ForAPatchLevelApp_BacksUpThenUpgrades()
    {
        var candidate = Candidate("OBSProject.OBSStudio", "31.0.1", "31.0.2");

        var playbook = UpdatePlaybooks.Create(candidate, @"C:\Users\streamer\AppData\Roaming\obs-studio");

        Assert.NotNull(playbook);
        Assert.Equal(PlaybookIds.UpdatePlaybook(candidate.Id), playbook.Id);
        Assert.Equal(2, playbook.Actions.Count);
        Assert.Equal(FixActionKind.ConfigBackup, playbook.Actions[0].Kind);
        Assert.Equal(@"C:\Users\streamer\AppData\Roaming\obs-studio", playbook.Actions[0].Target);
        Assert.False(playbook.Actions[0].RequiresElevation);

        Assert.Equal(FixActionKind.SoftwareUpdate, playbook.Actions[1].Kind);
        Assert.Equal(candidate.Id, playbook.Actions[1].Target);
        Assert.Equal("31.0.2", playbook.Actions[1].DesiredValue);
        Assert.Equal("31.0.1", playbook.Actions[1].RollbackValue);
        Assert.True(playbook.Actions[1].RequiresElevation);

        Assert.Equal(FixRisk.Low, playbook.Risk);
        Assert.True(playbook.AutoFixSupported);
    }

    [Fact]
    public void Create_ForAMajorUpgrade_IsNeverAutoFixable()
    {
        var playbook = UpdatePlaybooks.Create(Candidate("Some.App", "30.1.0", "31.0.0"), null);

        Assert.NotNull(playbook);
        Assert.Single(playbook.Actions);
        Assert.Equal(FixRisk.Medium, playbook.Risk);
        Assert.False(playbook.AutoFixSupported);
    }

    [Fact]
    public void Create_ForADriver_CreatesARestorePointThenInstalls()
    {
        var candidate = Candidate("driver-1", "10.0.0", "10.0.1") with
        {
            Kind = UpdateKind.Driver,
            Source = UpdateSource.WindowsUpdate,
        };

        var playbook = UpdatePlaybooks.Create(candidate);

        Assert.NotNull(playbook);
        Assert.Equal(2, playbook.Actions.Count);
        Assert.Equal(FixActionKind.RestorePoint, playbook.Actions[0].Kind);
        Assert.StartsWith(UpdatePlaybooks.RestorePointPrefix, playbook.Actions[0].Target, StringComparison.Ordinal);
        Assert.Equal(FixActionKind.WindowsUpdate, playbook.Actions[1].Kind);
        Assert.Equal(FixRisk.High, playbook.Risk);
        Assert.False(playbook.AutoFixSupported);
    }

    [Fact]
    public void Create_ForAVendorTool_ReturnsNullBecauseThereIsNoVerifiedPath()
    {
        var vendor = Candidate("elgato", null, null) with
        {
            Kind = UpdateKind.Firmware,
            Source = UpdateSource.VendorTool,
            AvailableVersion = null,
        };

        Assert.Null(UpdatePlaybooks.Create(vendor));
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
