using StreamDoctor.Core.Diagnosis;
using StreamDoctor.Core.Fixes;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Usb;

namespace StreamDoctor.Tests;

public sealed class UsbRuleTests
{
    [Fact]
    public void DropoutRule_CorrelatesObsDropoutsWithAResetAndAPowerFlag()
    {
        var mic = TestData.Device("Microphone (USB Audio Device)", UsbDeviceClass.Microphone);
        var topology = TestData.Topology(TestData.Controller("USB 3.0 Controller", UsbControllerKind.Usb3, mic));

        var events = Enumerable.Range(0, 3)
            .Select(index => TestEvents.Disconnect(TestData.At(20, 5 + index), "Microphone (USB Audio Device)"))
            .Append(TestEvents.UsbReset(TestData.At(20, 7)))
            .ToList();

        var report = TestEvents.Analyze(new DiagnosisContext
        {
            Events = events,
            Topology = topology,
            PowerSnapshots = [new UsbPowerSnapshot(mic, new UsbPowerSettings { SelectiveSuspendEnabled = true })],
            WindowStart = TestData.At(20),
            WindowEnd = TestData.At(22),
        });

        var dropout = Assert.Single(report.Findings, finding => finding.HypothesisId == "usb.dropout.correlated");
        Assert.Equal(Severity.Warning, dropout.Severity);
        Assert.Equal(1.0, dropout.Confidence, precision: 3);
        Assert.Equal(PlaybookIds.UsbDisableSelectiveSuspend, dropout.FixPlaybookId);
        Assert.Equal("3", dropout.Data[ScanEventPayload.Count]);
        Assert.Equal("true", dropout.Data["nearbyReset"]);
        Assert.True(dropout.Evidence.Count >= 4);
    }

    [Fact]
    public void DropoutRule_WithoutAResetStaysAtALowerConfidenceAndRecommendsManualChecks()
    {
        var events = new[] { TestEvents.Disconnect(TestData.At(20, 5), "Elgato Facecam") };

        var report = TestEvents.Analyze(new DiagnosisContext { Events = events });

        var dropout = Assert.Single(report.Findings, finding => finding.HypothesisId == "usb.dropout.correlated");
        Assert.Equal(Severity.Advisory, dropout.Severity);
        Assert.False(dropout.IsActionable);
        Assert.NotNull(dropout.RecommendedAction);
        Assert.True(dropout.Confidence < 0.8);
    }

    [Fact]
    public void TopologyChangeRule_ReportsADeviceThatMovedControllers()
    {
        var mic = TestData.Device("Microphone", UsbDeviceClass.Microphone);
        var before = TestData.Topology(TestData.Controller("USB 3.0 Controller A", UsbControllerKind.Usb3, mic));
        var after = TestData.Topology(TestData.Controller("USB 3.0 Controller B", UsbControllerKind.Usb3, mic));

        var report = TestEvents.Analyze(new DiagnosisContext
        {
            Events = [],
            Topology = after,
            PreviousTopology = before,
        });

        var change = Assert.Single(report.Findings, finding => finding.HypothesisId == "usb.topology.changed");
        Assert.Equal(Severity.Advisory, change.Severity);
        Assert.Contains("USB 3.0 Controller A", change.Explanation, StringComparison.Ordinal);
        Assert.Contains("USB 3.0 Controller B", change.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void BandwidthRule_FlagsAControllerAtRiskAndSuggestsHeadroom()
    {
        var busy = TestData.Controller(
            "Standard Enhanced PCI to USB Host Controller",
            UsbControllerKind.Usb2,
            TestData.Device("Webcam", UsbDeviceClass.Webcam),
            TestData.Device("Capture Card", UsbDeviceClass.CaptureCard));
        var idle = TestData.Controller("USB 3.10 eXtensible Host Controller", UsbControllerKind.Usb3);
        var topology = TestData.Topology(busy, idle);

        var report = TestEvents.Analyze(new DiagnosisContext
        {
            Events = UsbTopologyEvents.FromTopology(topology),
            Topology = topology,
        });

        var saturation = Assert.Single(report.Findings, finding => finding.HypothesisId == "usb.bandwidth.saturation");
        Assert.Equal(Severity.Critical, saturation.Severity);
        Assert.Contains("Webcam", saturation.Title, StringComparison.Ordinal);
        Assert.Contains(idle.Name, saturation.RecommendedAction!, StringComparison.Ordinal);
        Assert.NotEmpty(saturation.Evidence);
    }

    [Fact]
    public void BandwidthRule_StaysSilentForAHealthyUsb3Setup()
    {
        var topology = TestData.Topology(
            TestData.Controller(
                "USB 3.10 eXtensible Host Controller",
                UsbControllerKind.Usb3,
                TestData.Device("Microphone", UsbDeviceClass.Microphone)));

        var report = TestEvents.Analyze(new DiagnosisContext { Events = [], Topology = topology });

        Assert.DoesNotContain(report.Findings, finding => finding.HypothesisId == "usb.bandwidth.saturation");
    }

    [Fact]
    public void PowerRule_TurnsAuditRisksIntoActionableFindings()
    {
        var mic = TestData.Device("Microphone", UsbDeviceClass.Microphone);
        var topology = TestData.Topology(TestData.Controller("USB 3.0 Controller", UsbControllerKind.Usb3, mic));

        var report = TestEvents.Analyze(new DiagnosisContext
        {
            Events = [],
            Topology = topology,
            PowerSnapshots = [new UsbPowerSnapshot(mic, new UsbPowerSettings { AllowIdleIrpInD3 = true })],
        });

        var finding = Assert.Single(report.Findings, candidate => candidate.HypothesisId == UsbPowerAudit.IdleIrpCode);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Equal(PlaybookIds.UsbDisableIdleIrpD3, finding.FixPlaybookId);
        Assert.NotEmpty(finding.Evidence);
    }
}
