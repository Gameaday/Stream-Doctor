using StreamDoctor.Core.Fixes;
using StreamDoctor.Core.Usb;
using StreamDoctor.Fixes;

namespace StreamDoctor.Tests;

public sealed class PlaybookCatalogTests
{
    [Fact]
    public void Templates_CoverEveryPlaybookTheRulesCanRecommend()
    {
        string[] expected =
        [
            PlaybookIds.UsbDisableSelectiveSuspend,
            PlaybookIds.UsbDisableIdleIrpD3,
            PlaybookIds.UsbDisableHubPowerSaving,
            PlaybookIds.SystemHighPerformancePowerPlan,
            PlaybookIds.UsbReEnumerateDevice,
        ];

        foreach (var id in expected)
        {
            Assert.NotNull(PlaybookCatalog.Find(id));
        }
    }

    [Fact]
    public void Resolve_ReplacesTheDevicePlaceholder()
    {
        var mic = TestData.Device("Microphone", UsbDeviceClass.Microphone);

        var playbook = PlaybookCatalog.Resolve(PlaybookIds.UsbDisableSelectiveSuspend, mic);

        Assert.NotNull(playbook);
        var action = Assert.Single(playbook.Actions);
        Assert.DoesNotContain(PlaybookCatalog.DevicePlaceholder, action.Target, StringComparison.Ordinal);
        Assert.Contains(mic.InstanceId, action.Target, StringComparison.Ordinal);
        Assert.Equal(mic.InstanceId, action.DeviceInstanceId);
        Assert.EndsWith("|SelectiveSuspendEnabled", action.Target, StringComparison.Ordinal);
        Assert.Equal("0", action.DesiredValue);
        Assert.True(action.RequiresElevation);
    }

    [Fact]
    public void Resolve_DeviceScopedPlaybookWithoutADevice_ReturnsNull()
    {
        Assert.Null(PlaybookCatalog.Resolve(PlaybookIds.UsbDisableSelectiveSuspend, null));
    }

    [Fact]
    public void Resolve_SystemPlaybookDoesNotNeedADevice()
    {
        var playbook = PlaybookCatalog.Resolve(PlaybookIds.SystemHighPerformancePowerPlan, null);

        Assert.NotNull(playbook);
        var action = Assert.Single(playbook.Actions);
        Assert.Equal(FixActionKind.PowerPlan, action.Kind);
        Assert.Equal(PlaybookCatalog.HighPerformanceSchemeGuid, action.Target);
    }

    [Fact]
    public void ReEnumerationPlaybook_IsHighRiskAndNeverAutoFixable()
    {
        var playbook = PlaybookCatalog.Find(PlaybookIds.UsbReEnumerateDevice);

        Assert.NotNull(playbook);
        Assert.Equal(FixRisk.High, playbook.Risk);
        Assert.False(playbook.AutoFixSupported);
        Assert.NotEmpty(playbook.Prerequisites);
    }

    [Fact]
    public void EveryTemplateDeclaresATitleSummaryAndVerificationHint()
    {
        Assert.All(PlaybookCatalog.Templates, playbook =>
        {
            Assert.False(string.IsNullOrWhiteSpace(playbook.Title));
            Assert.False(string.IsNullOrWhiteSpace(playbook.Summary));
            Assert.False(string.IsNullOrWhiteSpace(playbook.VerificationHint));
            Assert.NotEmpty(playbook.Actions);
        });
    }
}
