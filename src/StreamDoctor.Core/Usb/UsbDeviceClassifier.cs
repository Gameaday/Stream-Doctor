using System.Globalization;

namespace StreamDoctor.Core.Usb;

/// <summary>How a USB device participates in the capture pipeline.</summary>
public enum UsbDeviceClass
{
    Unknown = 0,
    HostController,
    Hub,
    Webcam,
    CaptureCard,
    AudioInterface,
    Microphone,
    Keyboard,
    Mouse,
    GameController,
    StreamController,
    Storage,
    Network,
    Printer,
    Other,
}

/// <summary>Inputs used to classify a device without any Windows dependency.</summary>
public sealed record UsbClassificationInput
{
    public required string Name { get; init; }
    public string? DeviceClass { get; init; }
    public string? Service { get; init; }
    public IReadOnlyList<string> CompatibleIds { get; init; } = [];
    public IReadOnlyList<string> HardwareIds { get; init; } = [];
}

/// <summary>
/// Maps Windows device metadata onto <see cref="UsbDeviceClass"/>.
/// Pure and deterministic so it can be unit-tested against real device inventories.
/// </summary>
public static partial class UsbDeviceClassifier
{
    private static readonly string[] MicrophoneHints =
        ["microphone", "micro mic", "sm7", "sm58", "yeti", "quadcast", "snowball", "mv7", "podmic", "usb mic"];

    private static readonly string[] AudioInterfaceHints =
        ["audio interface", "goxlr", "scarlett", "audient", "focusrite", "behringer", "umc", "mixer", "dac", "audio"];

    private static readonly string[] CaptureCardHints =
        ["capture", "cam link", "camlink", "hd60", "4k60", "avermedia", "magewell", "elgato hd", "live gamer"];

    private static readonly string[] WebcamHints =
        ["webcam", "camera", "cam", "brio", "c920", "c922", "c930", "facecam", "insta360", "obsbot"];

    private static readonly string[] KeyboardHints = ["keyboard", "keypad", "k70", "g915", "huntsman"];
    private static readonly string[] MouseHints = ["mouse", "g502", "g pro", "viper", "deathadder", "model o"];
    private static readonly string[] GamepadHints = ["gamepad", "controller", "xbox", "dualsense", "dualshock", "joy-con", "8bitdo"];
    private static readonly string[] StreamControllerHints = ["stream deck", "streamdeck", "loupedeck", "macro pad"];
    private static readonly string[] StorageHints = ["flash", "ssd", "hdd", "storage", "card reader", "usb drive"];
    private static readonly string[] HubServices = ["usbhub3", "usbhub"];

    /// <summary>Classifies a device from its descriptive name, class and USB interface IDs.</summary>
    public static UsbDeviceClass Classify(UsbClassificationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var name = input.Name;
        var usbClassCode = FindUsbClassCode(input);

        // Interface class codes are authoritative; names only disambiguate within a class.
        switch (usbClassCode)
        {
            case 0x09:
                return UsbDeviceClass.Hub;
            case 0x01:
                return ContainsAny(name, MicrophoneHints) ? UsbDeviceClass.Microphone : UsbDeviceClass.AudioInterface;
            case 0x0e:
                return ContainsAny(name, CaptureCardHints) ? UsbDeviceClass.CaptureCard : UsbDeviceClass.Webcam;
            case 0x03:
                return ClassifyHid(name);
            case 0x08:
                return UsbDeviceClass.Storage;
            case 0x02 or 0x0a or 0xe0:
                return UsbDeviceClass.Network;
            case 0x07:
                return UsbDeviceClass.Printer;
            case 0xff:
                return ClassifyByName(name, vendorSpecific: true);
            case null:
                break;
            default:
                return UsbDeviceClass.Other;
        }

        var byName = ClassifyByName(name, vendorSpecific: false);
        if (byName != UsbDeviceClass.Unknown)
        {
            return byName;
        }

        var service = input.Service ?? string.Empty;
        if (HubServices.Any(hub => service.Contains(hub, StringComparison.OrdinalIgnoreCase)))
        {
            return UsbDeviceClass.Hub;
        }

        return UsbDeviceClass.Unknown;
    }

    private static UsbDeviceClass ClassifyByName(string name, bool vendorSpecific)
    {
        if (ContainsAny(name, StreamControllerHints))
        {
            return UsbDeviceClass.StreamController;
        }

        if (ContainsAny(name, CaptureCardHints))
        {
            return UsbDeviceClass.CaptureCard;
        }

        if (ContainsAny(name, WebcamHints))
        {
            return UsbDeviceClass.Webcam;
        }

        if (ContainsAny(name, MicrophoneHints))
        {
            return UsbDeviceClass.Microphone;
        }

        if (ContainsAny(name, AudioInterfaceHints))
        {
            return UsbDeviceClass.AudioInterface;
        }

        if (ContainsAny(name, KeyboardHints))
        {
            return UsbDeviceClass.Keyboard;
        }

        if (ContainsAny(name, MouseHints))
        {
            return UsbDeviceClass.Mouse;
        }

        if (ContainsAny(name, GamepadHints))
        {
            return UsbDeviceClass.GameController;
        }

        if (ContainsAny(name, StorageHints))
        {
            return UsbDeviceClass.Storage;
        }

        return vendorSpecific ? UsbDeviceClass.Other : UsbDeviceClass.Unknown;
    }

    private static UsbDeviceClass ClassifyHid(string name)
    {
        var classified = ClassifyByName(name, vendorSpecific: false);
        return classified == UsbDeviceClass.Unknown ? UsbDeviceClass.Other : classified;
    }

    private static int? FindUsbClassCode(UsbClassificationInput input)
    {
        foreach (var id in input.HardwareIds.Concat(input.CompatibleIds))
        {
            // e.g. USB\Class_0e&SubClass_01&Prot_00
            var marker = id.IndexOf("Class_", StringComparison.OrdinalIgnoreCase);
            if (marker < 0 || id.Length < marker + 8)
            {
                continue;
            }

            var digits = id.AsSpan(marker + 6, 2);
            if (int.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
            {
                return code;
            }
        }

        if (input.DeviceClass?.Equals("USB", StringComparison.OrdinalIgnoreCase) == true &&
            input.Service?.Contains("hub", StringComparison.OrdinalIgnoreCase) == true)
        {
            return 0x09;
        }

        return null;
    }

    private static bool ContainsAny(string value, IEnumerable<string> hints) =>
        hints.Any(hint => value.Contains(hint, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Relative bandwidth weight of a device class, in arbitrary "units".
    /// One USB 2.0 controller has roughly 4 units, a USB 3.x controller 12 units.
    /// These weights are conservative on purpose and are replaced by measured
    /// throughput once the USB test bench lands (design doc §9, Test 1).
    /// </summary>
    public static double BandwidthWeight(this UsbDeviceClass deviceClass) => deviceClass switch
    {
        UsbDeviceClass.CaptureCard => 3.0,
        UsbDeviceClass.Webcam => 2.5,
        UsbDeviceClass.Storage => 1.5,
        UsbDeviceClass.AudioInterface => 1.0,
        UsbDeviceClass.Network => 1.0,
        UsbDeviceClass.Microphone => 0.5,
        UsbDeviceClass.StreamController => 0.2,
        UsbDeviceClass.Printer => 0.2,
        UsbDeviceClass.Keyboard or UsbDeviceClass.Mouse or UsbDeviceClass.GameController => 0.1,
        UsbDeviceClass.Hub or UsbDeviceClass.HostController => 0.0,
        _ => 0.5,
    };

    /// <summary>Isochronous classes drop out first when a controller saturates.</summary>
    public static bool IsIsochronous(this UsbDeviceClass deviceClass) => deviceClass
        is UsbDeviceClass.Webcam
        or UsbDeviceClass.CaptureCard
        or UsbDeviceClass.AudioInterface
        or UsbDeviceClass.Microphone;

    /// <summary>True for classes whose dropouts the user notices immediately.</summary>
    public static bool IsStreamCritical(this UsbDeviceClass deviceClass) => deviceClass
        is UsbDeviceClass.Webcam
        or UsbDeviceClass.CaptureCard
        or UsbDeviceClass.AudioInterface
        or UsbDeviceClass.Microphone
        or UsbDeviceClass.StreamController;
}
