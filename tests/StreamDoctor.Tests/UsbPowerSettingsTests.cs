using StreamDoctor.Core.Usb;

namespace StreamDoctor.Tests;

public sealed class UsbPowerSettingsTests
{
    [Fact]
    public void Merge_TakesTheFirstNonNullValueForEachFlag()
    {
        var enumKey = new UsbPowerSettings
        {
            SelectiveSuspendEnabled = true,
            SourceKey = "enum",
        };

        var driverKey = new UsbPowerSettings
        {
            SelectiveSuspendEnabled = false,
            AllowIdleIrpInD3 = true,
            EnhancedPowerManagementEnabled = true,
            SourceKey = "driver",
        };

        var merged = UsbPowerSettings.Merge(enumKey, driverKey);

        Assert.True(merged.SelectiveSuspendEnabled);
        Assert.True(merged.AllowIdleIrpInD3);
        Assert.True(merged.EnhancedPowerManagementEnabled);
        Assert.Equal("enum", merged.SourceKey);
    }

    [Fact]
    public void Merge_FallsBackToTheDriverKeyWhenTheEnumKeyHasNothing()
    {
        var merged = UsbPowerSettings.Merge(
            new UsbPowerSettings { SourceKey = "enum" },
            new UsbPowerSettings { AllowIdleIrpInD3 = true, SourceKey = "driver" });

        Assert.Null(merged.SelectiveSuspendEnabled);
        Assert.True(merged.AllowIdleIrpInD3);
        Assert.Equal("enum", merged.SourceKey);
    }

    [Fact]
    public void Merge_WithNoSources_IsUnknown()
    {
        Assert.True(UsbPowerSettings.Merge().IsUnknown);
        Assert.True(UsbPowerSettings.Merge(null, null).IsUnknown);
    }

    [Fact]
    public void Merge_IgnoresNullEntries()
    {
        var merged = UsbPowerSettings.Merge(null, new UsbPowerSettings { SelectiveSuspendEnabled = false }, null);

        Assert.False(merged.SelectiveSuspendEnabled);
        Assert.True(merged.AllowIdleIrpInD3 is null);
    }
}
