using StreamDoctor.Core.Fixes;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Usb;

namespace StreamDoctor.Tests;

public sealed class UsbPowerAuditTests
{
    [Fact]
    public void Evaluate_FlagsSelectiveSuspendOnAStreamCriticalDevice()
    {
        var mic = TestData.Device("Microphone", UsbDeviceClass.Microphone);
        var topology = TestData.Topology(TestData.Controller("USB 3.0 Controller", UsbControllerKind.Usb3, mic));

        var risks = UsbPowerAudit.Evaluate(
            topology,
            [new UsbPowerSnapshot(mic, new UsbPowerSettings { SelectiveSuspendEnabled = true })]);

        var risk = Assert.Single(risks);
        Assert.Equal(UsbPowerAudit.SelectiveSuspendCode, risk.Code);
        Assert.Equal(Severity.Warning, risk.Severity);
        Assert.Equal(PlaybookIds.UsbDisableSelectiveSuspend, risk.FixPlaybookId);
    }

    [Fact]
    public void Evaluate_FlagsIdleIrpInD3OnIsochronousDevices()
    {
        var camera = TestData.Device("Webcam", UsbDeviceClass.Webcam);
        var topology = TestData.Topology(TestData.Controller("USB 3.0 Controller", UsbControllerKind.Usb3, camera));

        var risks = UsbPowerAudit.Evaluate(
            topology,
            [new UsbPowerSnapshot(camera, new UsbPowerSettings { AllowIdleIrpInD3 = true })]);

        var risk = Assert.Single(risks);
        Assert.Equal(UsbPowerAudit.IdleIrpCode, risk.Code);
        Assert.Equal(PlaybookIds.UsbDisableIdleIrpD3, risk.FixPlaybookId);
    }

    [Fact]
    public void Evaluate_FlagsHubPowerSavingAsAdvisory()
    {
        var hub = TestData.Device("USB Hub", UsbDeviceClass.Hub);
        var topology = TestData.Topology(TestData.Controller("USB 3.0 Controller", UsbControllerKind.Usb3, hub));

        var risks = UsbPowerAudit.Evaluate(
            topology,
            [new UsbPowerSnapshot(hub, new UsbPowerSettings { EnhancedPowerManagementEnabled = true })]);

        var risk = Assert.Single(risks);
        Assert.Equal(UsbPowerAudit.HubPowerSavingCode, risk.Code);
        Assert.Equal(Severity.Advisory, risk.Severity);
    }

    [Fact]
    public void Evaluate_FlagsABalancedPowerPlan()
    {
        var risks = UsbPowerAudit.Evaluate(
            UsbTopology.Empty(TestData.At(12)),
            [],
            new PowerPlanInfo("Balanced", "381b4222-f694-41f0-9685-ff5bb260df2e", IsHighPerformance: false));

        var risk = Assert.Single(risks);
        Assert.Equal(UsbPowerAudit.BalancedPlanCode, risk.Code);
        Assert.Null(risk.Device);
        Assert.Equal(PlaybookIds.SystemHighPerformancePowerPlan, risk.FixPlaybookId);
    }

    [Fact]
    public void Evaluate_HighPerformancePlanIsNotAProblem()
    {
        var risks = UsbPowerAudit.Evaluate(
            UsbTopology.Empty(TestData.At(12)),
            [],
            new PowerPlanInfo("High performance", "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", IsHighPerformance: true));

        Assert.Empty(risks);
    }

    [Fact]
    public void Evaluate_UnknownSettingsProduceNoNoise()
    {
        var mic = TestData.Device("Microphone", UsbDeviceClass.Microphone);
        var topology = TestData.Topology(TestData.Controller("USB 3.0 Controller", UsbControllerKind.Usb3, mic));

        var risks = UsbPowerAudit.Evaluate(topology, [new UsbPowerSnapshot(mic, new UsbPowerSettings())]);

        Assert.Empty(risks);
    }

    [Fact]
    public void Evaluate_IgnoresSettingsForDevicesThatAreNotInTheTopology()
    {
        var mic = TestData.Device("Microphone", UsbDeviceClass.Microphone);
        var camera = TestData.Device("Webcam", UsbDeviceClass.Webcam);
        var topology = TestData.Topology(TestData.Controller("USB 3.0 Controller", UsbControllerKind.Usb3, mic));

        var risks = UsbPowerAudit.Evaluate(
            topology,
            [new UsbPowerSnapshot(camera, new UsbPowerSettings { SelectiveSuspendEnabled = true })]);

        // A camera that is not enumerated right now is not a finding of this sweep.
        Assert.Empty(risks);
    }

    [Fact]
    public void Evaluate_OrdersCriticalAndWarningsBeforeAdvisories()
    {
        var mic = TestData.Device("Microphone", UsbDeviceClass.Microphone);
        var hub = TestData.Device("USB Hub", UsbDeviceClass.Hub);
        var topology = TestData.Topology(TestData.Controller("USB 3.0 Controller", UsbControllerKind.Usb3, mic, hub));

        var risks = UsbPowerAudit.Evaluate(
            topology,
            [
                new UsbPowerSnapshot(hub, new UsbPowerSettings { EnhancedPowerManagementEnabled = true }),
                new UsbPowerSnapshot(mic, new UsbPowerSettings { SelectiveSuspendEnabled = true }),
            ],
            new PowerPlanInfo("Balanced", "guid", IsHighPerformance: false));

        Assert.Equal(Severity.Warning, risks[0].Severity);
        Assert.Equal(Severity.Advisory, risks[^1].Severity);
        Assert.Equal(3, risks.Count);
    }
}
