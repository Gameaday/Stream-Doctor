using StreamDoctor.Core.Fixes;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Updates;
using StreamDoctor.Core.Usb;
using StreamDoctor.Fixes;

namespace StreamDoctor.Tests;

public sealed class FixCoordinatorTests
{
    [Fact]
    public void Resolve_FindsADevicePlaybookByTargetInstanceId()
    {
        var mic = TestData.Device("Microphone", UsbDeviceClass.Microphone);
        var finding = Finding(PlaybookIds.UsbDisableSelectiveSuspend, mic.InstanceId, mic.Name);

        var playbook = FixCoordinator.Resolve(finding, [mic], []);

        Assert.NotNull(playbook);
        Assert.Equal(PlaybookIds.UsbDisableSelectiveSuspend, playbook.Id);
        Assert.Contains(mic.InstanceId, playbook.Actions[0].Target, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_FallbackToDeviceNameWhenTheInstanceIdIsMissing()
    {
        var mic = TestData.Device("Microphone (USB Audio Device)", UsbDeviceClass.Microphone);
        var finding = Finding(PlaybookIds.UsbDisableIdleIrpD3, target: "unknown", deviceName: "Microphone (USB Audio Device)");

        var playbook = FixCoordinator.Resolve(finding, [mic], []);

        Assert.NotNull(playbook);
        Assert.Contains(mic.InstanceId, playbook.Actions[0].Target, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_ReturnsNullForADeviceScopedPlaybookWithoutADevice()
    {
        var finding = Finding(PlaybookIds.UsbDisableSelectiveSuspend, "missing-device", deviceName: null);

        Assert.Null(FixCoordinator.Resolve(finding, [], []));
    }

    [Fact]
    public void Resolve_BuildsAnUpdatePlaybookFromTheCandidateList()
    {
        var candidate = new UpdateCandidate
        {
            Id = "OBSProject.OBSStudio",
            Name = "OBS Studio",
            Kind = UpdateKind.Application,
            Source = UpdateSource.Winget,
            InstalledVersion = "31.0.1",
            AvailableVersion = "31.0.2",
        };

        var finding = Finding(PlaybookIds.UpdatePlaybook(candidate.Id), candidate.Id, deviceName: null);
        var playbook = FixCoordinator.Resolve(finding, [], [candidate], configBackupSource: "/config/obs");

        Assert.NotNull(playbook);
        Assert.Equal(PlaybookIds.UpdatePlaybook(candidate.Id), playbook.Id);
        Assert.Equal(2, playbook.Actions.Count);
        Assert.Equal(FixActionKind.ConfigBackup, playbook.Actions[0].Kind);
    }

    [Fact]
    public void Resolve_ReturnsNullWhenTheUpdateIsNoLongerInTheSweep()
    {
        var finding = Finding(PlaybookIds.UpdatePlaybook("gone.package"), "gone.package", deviceName: null);

        Assert.Null(FixCoordinator.Resolve(finding, [], []));
    }

    [Fact]
    public void Resolve_ReturnsNullForADiagnosisOnlyFinding()
    {
        var finding = new Finding
        {
            HypothesisId = "obs.frames.dropped",
            Module = ScanModule.FramePacing,
            Title = "Dropped frames",
            Explanation = "Network",
            Severity = Severity.Warning,
            Confidence = 0.9,
            Evidence = [],
        };

        Assert.Null(FixCoordinator.Resolve(finding, [], []));
    }

    private static Finding Finding(string playbookId, string target, string? deviceName) => new()
    {
        HypothesisId = "test.finding",
        Module = ScanModule.UsbSentinel,
        Title = "Test finding",
        Explanation = "Test explanation",
        Severity = Severity.Warning,
        Confidence = 0.9,
        Evidence = [],
        FixPlaybookId = playbookId,
        Data = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["target"] = target,
            [ScanEventPayload.Device] = deviceName ?? string.Empty,
        },
    };
}
