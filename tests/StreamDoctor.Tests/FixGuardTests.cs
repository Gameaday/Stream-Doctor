using StreamDoctor.Core.Fixes;
using StreamDoctor.Core.Usb;
using StreamDoctor.Fixes;

namespace StreamDoctor.Tests;

public sealed class FixGuardTests
{
    [Fact]
    public void Evaluate_RefusesToRunWhileLive()
    {
        var block = new FixGuard().Evaluate(SimplePlaybook(), Approved() with { IsLive = true });

        Assert.NotNull(block);
        Assert.Equal(FixGuard.CodeLive, block.Code);
    }

    [Fact]
    public void Evaluate_GuidedFixRequiresApproval()
    {
        var block = new FixGuard().Evaluate(SimplePlaybook(), new FixExecutionOptions());

        Assert.NotNull(block);
        Assert.Equal(FixGuard.CodeApproval, block.Code);
    }

    [Fact]
    public void Evaluate_ApprovedLowRiskFixIsAllowed()
    {
        Assert.Null(new FixGuard().Evaluate(SimplePlaybook(), Approved()));
    }

    [Fact]
    public void Evaluate_AutoFixWithoutApprovalIsAllowedOnceConfidenceClearsTheThreshold()
    {
        var options = new FixExecutionOptions
        {
            Mode = FixMode.Auto,
            Confidence = 0.9,
            AutoFixThreshold = 0.85,
            IsElevated = true,
        };

        Assert.Null(new FixGuard().Evaluate(SimplePlaybook(), options));
    }

    [Fact]
    public void Evaluate_AutoFixBelowTheThresholdIsBlocked()
    {
        var options = new FixExecutionOptions
        {
            Mode = FixMode.Auto,
            Confidence = 0.6,
            AutoFixThreshold = 0.85,
            IsElevated = true,
        };

        var block = new FixGuard().Evaluate(SimplePlaybook(), options);

        Assert.NotNull(block);
        Assert.Equal(FixGuard.CodeLowConfidence, block.Code);
    }

    [Fact]
    public void Evaluate_AutoFixForAPlaybookThatNeedsAHumanIsBlocked()
    {
        var playbook = SimplePlaybook() with { AutoFixSupported = false };
        var options = new FixExecutionOptions
        {
            Mode = FixMode.Auto,
            Confidence = 1,
            IsElevated = true,
        };

        var block = new FixGuard().Evaluate(playbook, options);

        Assert.NotNull(block);
        Assert.Equal(FixGuard.CodeAutoUnsupported, block.Code);
    }

    [Fact]
    public void Evaluate_HighRiskFixRequiresApprovalEvenInAutoMode()
    {
        var playbook = SimplePlaybook() with { Risk = FixRisk.High };
        var options = new FixExecutionOptions { Mode = FixMode.Auto, Confidence = 1, IsElevated = true };

        var block = new FixGuard().Evaluate(playbook, options);

        Assert.NotNull(block);
        Assert.Equal(FixGuard.CodeApproval, block.Code);
    }

    [Fact]
    public void Evaluate_ElevationIsRequiredWhenAnActionNeedsIt()
    {
        var playbook = SimplePlaybook() with
        {
            Actions = [Action("a", "0") with { RequiresElevation = true }],
        };

        var block = new FixGuard().Evaluate(playbook, Approved() with { IsElevated = false });

        Assert.NotNull(block);
        Assert.Equal(FixGuard.CodeElevation, block.Code);
    }

    [Fact]
    public void Evaluate_PlaybookWithoutActionsIsRefused()
    {
        var block = new FixGuard().Evaluate(SimplePlaybook() with { Actions = [] }, Approved());

        Assert.NotNull(block);
        Assert.Equal(FixGuard.CodeNoActions, block.Code);
    }

    [Fact]
    public void Evaluate_RefusesToReEnumerateAKeyboard()
    {
        var keyboard = TestData.Device("Corsair K70 Keyboard", UsbDeviceClass.Keyboard);
        var playbook = PlaybookCatalog.Resolve(PlaybookIds.UsbReEnumerateDevice, keyboard);

        var block = new FixGuard().Evaluate(
            playbook!,
            Approved(),
            new FixGuardOptions { Devices = [keyboard] });

        Assert.NotNull(block);
        Assert.Equal(DeviceReEnumerationGuard.CodeInputDevice, block.Code);
    }

    [Fact]
    public void Evaluate_RefusesToReEnumerateAUsbHub()
    {
        var hub = TestData.Device("USB Hub", UsbDeviceClass.Hub);
        var playbook = PlaybookCatalog.Resolve(PlaybookIds.UsbReEnumerateDevice, hub);

        var block = new FixGuard().Evaluate(playbook!, Approved(), new FixGuardOptions { Devices = [hub] });

        Assert.NotNull(block);
        Assert.Equal(DeviceReEnumerationGuard.CodeTopologyNode, block.Code);
    }

    [Fact]
    public void Evaluate_AllowsReEnumeratingACaptureCardWithApprovalAndElevation()
    {
        var capture = TestData.Device("HD60 X", UsbDeviceClass.CaptureCard);
        var playbook = PlaybookCatalog.Resolve(PlaybookIds.UsbReEnumerateDevice, capture);

        Assert.Null(new FixGuard().Evaluate(playbook!, Approved(), new FixGuardOptions { Devices = [capture] }));
    }

    [Fact]
    public void Evaluate_RefusesToReEnumerateAnUnknownDevice()
    {
        var unknown = TestData.Device("Composite Device", UsbDeviceClass.Unknown);

        var decision = DeviceReEnumerationGuard.Evaluate(unknown, [unknown]);

        Assert.False(decision.Allowed);
        Assert.Equal(DeviceReEnumerationGuard.CodeUnknown, decision.Code);
    }

    private static FixExecutionOptions Approved() => new()
    {
        UserApproved = true,
        IsElevated = true,
        Mode = FixMode.Guided,
    };

    private static FixPlaybook SimplePlaybook() => new()
    {
        Id = "test.playbook",
        Title = "Test playbook",
        Summary = "For tests",
        Risk = FixRisk.Low,
        Actions = [Action("test-a", "0")],
    };

    private static FixAction Action(string target, string desiredValue) => new()
    {
        Kind = FixActionKind.RegistryValue,
        Target = target,
        DesiredValue = desiredValue,
        Description = $"Set {target}",
    };
}
