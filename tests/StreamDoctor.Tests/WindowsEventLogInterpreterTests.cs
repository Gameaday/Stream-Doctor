using StreamDoctor.Core.Model;
using StreamDoctor.Core.Windows;

namespace StreamDoctor.Tests;

public sealed class WindowsEventLogInterpreterTests
{
    [Theory]
    [InlineData("USBXHCI", 400, ScanEventCodes.UsbControllerError)]
    [InlineData("Microsoft-Windows-USB-USBHUB3", 411, ScanEventCodes.UsbDeviceReset)]
    public void Interpret_MapsKnownUsbProviderEvents(string provider, int id, string expectedCategory)
    {
        var evidence = WindowsEventLogInterpreter.Interpret(Record(provider, id));

        Assert.NotNull(evidence);
        Assert.Equal(expectedCategory, evidence.Category);
        Assert.Equal(ScanModule.UsbSentinel, evidence.Module);
        Assert.Equal(Severity.Warning, evidence.Severity);
        Assert.Equal(id.ToString(System.Globalization.CultureInfo.InvariantCulture), evidence.GetPayload(ScanEventPayload.EventId));
    }

    [Fact]
    public void Interpret_IgnoresUnknownUsbEventIds()
    {
        Assert.Null(WindowsEventLogInterpreter.Interpret(Record("USBXHCI", 999)));
    }

    [Fact]
    public void Interpret_RecognizesSurpriseRemovalFromKernelPnp()
    {
        var evidence = WindowsEventLogInterpreter.Interpret(
            Record("Microsoft-Windows-Kernel-PnP", 424, "The device USB\\VID_1234 was surprise removal."));

        Assert.NotNull(evidence);
        Assert.Equal(ScanEventCodes.UsbDeviceRemoved, evidence.Category);
    }

    [Fact]
    public void Interpret_MapsAGpuDriverReset()
    {
        var evidence = WindowsEventLogInterpreter.Interpret(
            Record("Display", 4101, "Display driver nvlddmkm stopped responding and has successfully recovered."));

        Assert.NotNull(evidence);
        Assert.Equal(ScanEventCodes.GpuDriverReset, evidence.Category);
        Assert.Equal(ScanModule.FramePacing, evidence.Module);
    }

    [Fact]
    public void Interpret_ExtractsTheCrashedApplicationName()
    {
        var evidence = WindowsEventLogInterpreter.Interpret(
            Record("Application Error", 1000, "Faulting application name: obs64.exe, version: 31.0.1.0"));

        Assert.NotNull(evidence);
        Assert.Equal(ScanEventCodes.AppCrashed, evidence.Category);
        Assert.Equal("obs64.exe", evidence.GetPayload(ScanEventPayload.Device));
        Assert.Contains("obs64.exe", evidence.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(7031)]
    [InlineData(7034)]
    [InlineData(7000)]
    public void Interpret_MapsServiceFailures(int id)
    {
        var evidence = WindowsEventLogInterpreter.Interpret(Record("Service Control Manager", id));

        Assert.NotNull(evidence);
        Assert.Equal(ScanEventCodes.ServiceFailed, evidence.Category);
        Assert.Equal(ScanModule.WindowsInterference, evidence.Module);
    }

    [Fact]
    public void Interpret_MapsAnUnexpectedPowerLossToCritical()
    {
        var evidence = WindowsEventLogInterpreter.Interpret(
            Record("Microsoft-Windows-Kernel-Power", 41));

        Assert.NotNull(evidence);
        Assert.Equal(ScanEventCodes.SystemPowerEvent, evidence.Category);
        Assert.Equal(Severity.Critical, evidence.Severity);
    }

    [Fact]
    public void Interpret_MapsWindowsUpdateInstallation()
    {
        var evidence = WindowsEventLogInterpreter.Interpret(
            Record("Microsoft-Windows-WindowsUpdateClient", 19));

        Assert.NotNull(evidence);
        Assert.Equal(ScanEventCodes.WindowsUpdateInstalled, evidence.Category);
        Assert.Equal(Severity.Advisory, evidence.Severity);
    }

    [Fact]
    public void Interpret_IgnoresUnrelatedEvents()
    {
        Assert.Null(WindowsEventLogInterpreter.Interpret(Record("Microsoft-Windows-DistributedCOM", 10016)));
    }

    [Fact]
    public void Interpret_TruncatesLongMessages()
    {
        var message = new string('x', 2000);

        var evidence = WindowsEventLogInterpreter.Interpret(Record("Display", 4101, message));

        Assert.NotNull(evidence);
        Assert.Equal(400, evidence.GetPayload("windows.message")!.Length);
    }

    [Fact]
    public void InterpretMany_DropsUnmappedEntriesAndSortsByTime()
    {
        var records = new[]
        {
            Record("Display", 4101, timestamp: TestData.At(21)),
            Record("Microsoft-Windows-DistributedCOM", 10016, timestamp: TestData.At(20)),
            Record("USBXHCI", 411, timestamp: TestData.At(20, 30)),
        };

        var evidence = WindowsEventLogInterpreter.InterpretMany(records);

        Assert.Equal(2, evidence.Count);
        Assert.True(evidence[0].Timestamp < evidence[1].Timestamp);
    }

    private static WindowsEventRecord Record(
        string provider,
        int id,
        string? message = null,
        DateTimeOffset? timestamp = null) => new()
    {
        Timestamp = timestamp ?? TestData.At(20),
        LogName = "System",
        Provider = provider,
        Id = id,
        Level = 3,
        Message = message,
        RecordId = 1,
    };
}
