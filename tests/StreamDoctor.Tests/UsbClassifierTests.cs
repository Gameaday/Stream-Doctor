using StreamDoctor.Core.Usb;

namespace StreamDoctor.Tests;

public sealed class UsbClassifierTests
{
    [Theory]
    [InlineData("HD60 X", "0e", UsbDeviceClass.CaptureCard)]
    [InlineData("Logitech Brio 4K", "0e", UsbDeviceClass.Webcam)]
    [InlineData("Scarlett Solo", "01", UsbDeviceClass.AudioInterface)]
    [InlineData("SM7B USB", "01", UsbDeviceClass.Microphone)]
    [InlineData("Corsair K70", "03", UsbDeviceClass.Keyboard)]
    [InlineData("Logitech G502", "03", UsbDeviceClass.Mouse)]
    [InlineData("Xbox Wireless Controller", "03", UsbDeviceClass.GameController)]
    [InlineData("USB Flash Drive", "08", UsbDeviceClass.Storage)]
    [InlineData("USB Ethernet Adapter", "02", UsbDeviceClass.Network)]
    public void Classify_UsesTheUsbInterfaceClassCode(string name, string classCode, UsbDeviceClass expected)
    {
        var classified = UsbDeviceClassifier.Classify(new UsbClassificationInput
        {
            Name = name,
            CompatibleIds = [$"USB\\Class_{classCode}&SubClass_00&Prot_00"],
        });

        Assert.Equal(expected, classified);
    }

    [Fact]
    public void Classify_RecognizesAUsbHubFromItsClassCode()
    {
        var classified = UsbDeviceClassifier.Classify(new UsbClassificationInput
        {
            Name = "Generic USB Hub",
            CompatibleIds = ["USB\\Class_09&SubClass_00&Prot_00"],
        });

        Assert.Equal(UsbDeviceClass.Hub, classified);
    }

    [Fact]
    public void Classify_RecognizesStreamDeckByNameWithoutAClassCode()
    {
        var classified = UsbDeviceClassifier.Classify(new UsbClassificationInput
        {
            Name = "Elgato Stream Deck MK.2",
        });

        Assert.Equal(UsbDeviceClass.StreamController, classified);
    }

    [Fact]
    public void Classify_UnknownDeviceStaysUnknown()
    {
        var classified = UsbDeviceClassifier.Classify(new UsbClassificationInput
        {
            Name = "Composite Device",
        });

        Assert.Equal(UsbDeviceClass.Unknown, classified);
    }

    [Fact]
    public void Classify_VendorSpecificDeviceIsOtherNotUnknown()
    {
        var classified = UsbDeviceClassifier.Classify(new UsbClassificationInput
        {
            Name = "Studio Light",
            CompatibleIds = ["USB\\Class_ff&SubClass_00&Prot_00"],
        });

        Assert.Equal(UsbDeviceClass.Other, classified);
    }

    [Fact]
    public void BandwidthWeight_RanksCaptureCardsAboveHid()
    {
        Assert.True(UsbDeviceClass.CaptureCard.BandwidthWeight() > UsbDeviceClass.Webcam.BandwidthWeight());
        Assert.True(UsbDeviceClass.Webcam.BandwidthWeight() > UsbDeviceClass.AudioInterface.BandwidthWeight());
        Assert.True(UsbDeviceClass.AudioInterface.BandwidthWeight() > UsbDeviceClass.Mouse.BandwidthWeight());
        Assert.Equal(0, UsbDeviceClass.Hub.BandwidthWeight());
    }

    [Theory]
    [InlineData(UsbDeviceClass.Webcam, true)]
    [InlineData(UsbDeviceClass.CaptureCard, true)]
    [InlineData(UsbDeviceClass.Microphone, true)]
    [InlineData(UsbDeviceClass.Storage, false)]
    [InlineData(UsbDeviceClass.Keyboard, false)]
    public void IsIsochronous_MatchesTheDevicesThatDropOutFirst(UsbDeviceClass deviceClass, bool expected)
    {
        Assert.Equal(expected, deviceClass.IsIsochronous());
    }
}
