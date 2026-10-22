using StreamDoctor.Core.Obs;
using StreamDoctor.Core.Usb;

namespace StreamDoctor.Tests;

/// <summary>Shared fixture loading and domain builders for the test suite.</summary>
internal static class TestData
{
    public static readonly DateOnly LogDate = new(2026, 3, 15);

    public static string ReadObsLog(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Obs", fileName));

    public static ObsLogDocument ParseObs(
        string fileName,
        DateOnly? logDate = null,
        TimeZoneInfo? timeZone = null) =>
        ObsLogParser.Parse(
            ReadObsLog(fileName),
            logDate ?? LogDate,
            timeZone ?? TimeZoneInfo.Utc,
            fileName);

    public static UsbDevice Device(
        string name,
        UsbDeviceClass deviceClass,
        string? controller = null,
        string? instanceId = null,
        int? port = 1,
        string? hub = @"USB\ROOT_HUB30\4&1a2b3c&0&0")
    {
        return new UsbDevice
        {
            InstanceId = instanceId ?? $@"USB\VID_046D&PID_{StableHash(name) & 0xFFFF:X4}\5&{StableHash(name)}&0&{port ?? 1}",
            Name = name,
            Class = deviceClass,
            ControllerInstanceId = controller,
            HubInstanceId = hub,
            PortNumber = port,
        };
    }

    public static UsbController Controller(
        string name,
        UsbControllerKind kind,
        params UsbDevice[] devices)
    {
        var controllerId = ControllerId(name);

        return new()
        {
            InstanceId = controllerId,
            Name = name,
            Kind = kind,
            Devices = devices
                .Select(device => device with { ControllerInstanceId = controllerId })
                .ToList(),
        };
    }

    public static string ControllerId(string name) =>
        $@"PCI\VEN_8086&DEV_A36D\3&11583659&0&{StableHash(name):X4}";

    internal static int StableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in value)
            {
                hash = (hash * 31) + character;
            }

            return hash & 0x7FFFFFFF;
        }
    }

    public static UsbTopology Topology(params UsbController[] controllers) => new()
    {
        CapturedAt = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero),
        Controllers = controllers,
    };

    public static DateTimeOffset At(int hour, int minute = 0, int second = 0) =>
        new(2026, 3, 15, hour, minute, second, TimeSpan.Zero);
}
