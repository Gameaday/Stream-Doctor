using StreamDoctor.Core.Usb;

namespace StreamDoctor.Tests;

public sealed class UsbTopologyDiffTests
{
    [Theory]
    [InlineData("Intel(R) USB 3.20 eXtensible Host Controller - 1.20 (Microsoft)", UsbControllerKind.Usb3)]
    [InlineData("AMD USB 3.10 eXtensible Host Controller", UsbControllerKind.Usb3)]
    [InlineData("Standard Enhanced PCI to USB Host Controller", UsbControllerKind.Usb2)]
    [InlineData("Intel USB4 Host Controller", UsbControllerKind.Usb4)]
    [InlineData("Controleur USB", UsbControllerKind.Unknown)]
    public void ControllerKindDetector_InfersTheGeneration(string name, UsbControllerKind expected)
    {
        Assert.Equal(expected, UsbControllerKindDetector.FromName(name));
    }

    [Fact]
    public void BudgetUnits_GivesUsb3MoreHeadroomThanUsb2()
    {
        Assert.True(UsbControllerKind.Usb3.BudgetUnits() > UsbControllerKind.Usb2.BudgetUnits());
        Assert.True(UsbControllerKind.Usb4.BudgetUnits() > UsbControllerKind.Usb3.BudgetUnits());
    }

    [Fact]
    public void Compare_DetectsAppearedAndDisappearedDevices()
    {
        var mic = TestData.Device("Microphone", UsbDeviceClass.Microphone);
        var camera = TestData.Device("Webcam", UsbDeviceClass.Webcam);

        var before = TestData.Topology(TestData.Controller("USB 3.0 Controller", UsbControllerKind.Usb3, mic));
        var after = TestData.Topology(TestData.Controller("USB 3.0 Controller", UsbControllerKind.Usb3, camera));

        var changes = UsbTopologyDiff.Compare(before, after);

        Assert.Contains(changes, change => change.Kind == UsbTopologyChangeKind.Disappeared && change.Device.Name == "Microphone");
        Assert.Contains(changes, change => change.Kind == UsbTopologyChangeKind.Appeared && change.Device.Name == "Webcam");
    }

    [Fact]
    public void Compare_DetectsADeviceThatMovedControllers()
    {
        var mic = TestData.Device("Microphone", UsbDeviceClass.Microphone);
        var before = TestData.Topology(
            TestData.Controller("USB 3.0 Controller A", UsbControllerKind.Usb3, mic),
            TestData.Controller("USB 3.0 Controller B", UsbControllerKind.Usb3));

        var after = TestData.Topology(
            TestData.Controller("USB 3.0 Controller A", UsbControllerKind.Usb3),
            TestData.Controller("USB 3.0 Controller B", UsbControllerKind.Usb3, mic));

        var change = Assert.Single(
            UsbTopologyDiff.Compare(before, after),
            candidate => candidate.Kind == UsbTopologyChangeKind.MovedToAnotherController);

        Assert.Equal(TestData.ControllerId("USB 3.0 Controller A"), change.FromController);
        Assert.Equal(TestData.ControllerId("USB 3.0 Controller B"), change.ToController);
        Assert.Equal("Microphone", change.Device.Name);
    }

    [Fact]
    public void Compare_DetectsAPortChangeOnTheSameController()
    {
        var mic = TestData.Device("Microphone", UsbDeviceClass.Microphone, port: 1);
        var before = TestData.Topology(TestData.Controller("USB 3.0 Controller", UsbControllerKind.Usb3, mic));

        var moved = mic with { PortNumber = 4 };
        var after = TestData.Topology(TestData.Controller("USB 3.0 Controller", UsbControllerKind.Usb3, moved));

        var change = Assert.Single(UsbTopologyDiff.Compare(before, after));
        Assert.Equal(UsbTopologyChangeKind.MovedToAnotherPort, change.Kind);
        Assert.Equal(1, change.FromPort);
        Assert.Equal(4, change.ToPort);
    }

    [Fact]
    public void Compare_WithNoChanges_IsEmpty()
    {
        var topology = TestData.Topology(
            TestData.Controller("USB 3.0 Controller", UsbControllerKind.Usb3,
                TestData.Device("Microphone", UsbDeviceClass.Microphone)));

        Assert.Empty(UsbTopologyDiff.Compare(topology, topology));
    }
}
