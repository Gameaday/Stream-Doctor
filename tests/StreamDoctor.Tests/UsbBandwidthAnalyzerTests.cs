using StreamDoctor.Core.Usb;

namespace StreamDoctor.Tests;

public sealed class UsbBandwidthAnalyzerTests
{
    [Fact]
    public void Analyze_FlagsTwoIsochronousDevicesOnAUsb2Controller()
    {
        var controller = TestData.Controller(
            "Standard Enhanced PCI to USB Host Controller",
            UsbControllerKind.Usb2,
            TestData.Device("Webcam", UsbDeviceClass.Webcam),
            TestData.Device("Capture Card", UsbDeviceClass.CaptureCard));

        var load = Assert.Single(UsbBandwidthAnalyzer.Analyze(TestData.Topology(controller)));

        Assert.True(load.IsAtRisk);
        Assert.True(load.IsSaturated);
        Assert.Equal(2, load.IsochronousDeviceCount);
        Assert.Equal(5.5, load.LoadUnits, precision: 3);
    }

    [Fact]
    public void Analyze_LeavesAHealthyUsb3ControllerAlone()
    {
        var controller = TestData.Controller(
            "USB 3.10 eXtensible Host Controller",
            UsbControllerKind.Usb3,
            TestData.Device("Webcam", UsbDeviceClass.Webcam),
            TestData.Device("Microphone", UsbDeviceClass.Microphone),
            TestData.Device("Mouse", UsbDeviceClass.Mouse));

        var load = Assert.Single(UsbBandwidthAnalyzer.Analyze(TestData.Topology(controller)));

        Assert.False(load.IsAtRisk);
        Assert.Equal(2, load.Devices.Count(device => device.Class.IsIsochronous()));
    }

    [Fact]
    public void Analyze_WarnsWhenHeadroomIsLowButNotExhausted()
    {
        var controller = TestData.Controller(
            "USB 3.10 eXtensible Host Controller",
            UsbControllerKind.Usb3,
            TestData.Device("Capture Card", UsbDeviceClass.CaptureCard),
            TestData.Device("Webcam", UsbDeviceClass.Webcam),
            TestData.Device("Webcam 2", UsbDeviceClass.Webcam),
            TestData.Device("Capture Card 2", UsbDeviceClass.CaptureCard));

        var load = Assert.Single(UsbBandwidthAnalyzer.Analyze(TestData.Topology(controller)));

        Assert.True(load.IsAtRisk);
        Assert.False(load.IsSaturated);
        Assert.Equal(0.917, load.Utilization, precision: 2);
    }

    [Fact]
    public void FindHeadroom_SuggestsAnIdleController()
    {
        var busy = TestData.Controller(
            "Standard Enhanced PCI to USB Host Controller",
            UsbControllerKind.Usb2,
            TestData.Device("Webcam", UsbDeviceClass.Webcam),
            TestData.Device("Capture Card", UsbDeviceClass.CaptureCard));
        var idle = TestData.Controller("USB 3.10 eXtensible Host Controller", UsbControllerKind.Usb3);

        var loads = UsbBandwidthAnalyzer.Analyze(TestData.Topology(busy, idle));
        var headroom = UsbBandwidthAnalyzer.FindHeadroom(loads, loads.Single(load => load.Controller.Name == busy.Name));

        var candidate = Assert.Single(headroom);
        Assert.Equal(idle.Name, candidate.Controller.Name);
    }

    [Fact]
    public void Analyze_ExcludesHubsFromTheLoad()
    {
        var controller = TestData.Controller(
            "USB 3.10 eXtensible Host Controller",
            UsbControllerKind.Usb3,
            TestData.Device("USB Hub", UsbDeviceClass.Hub),
            TestData.Device("Microphone", UsbDeviceClass.Microphone));

        var load = Assert.Single(UsbBandwidthAnalyzer.Analyze(TestData.Topology(controller)));

        Assert.Single(load.Devices);
        Assert.Equal(0.5, load.LoadUnits, precision: 3);
    }

    [Fact]
    public void Analyze_OrdersTheMostLoadedControllerFirst()
    {
        var loaded = TestData.Controller(
            "Standard Enhanced PCI to USB Host Controller",
            UsbControllerKind.Usb2,
            TestData.Device("Webcam", UsbDeviceClass.Webcam),
            TestData.Device("Capture Card", UsbDeviceClass.CaptureCard));
        var idle = TestData.Controller("USB 3.10 eXtensible Host Controller", UsbControllerKind.Usb3);

        var loads = UsbBandwidthAnalyzer.Analyze(TestData.Topology(idle, loaded));

        Assert.Equal(loaded.Name, loads[0].Controller.Name);
    }
}
