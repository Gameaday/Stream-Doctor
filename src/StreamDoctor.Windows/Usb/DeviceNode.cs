namespace StreamDoctor.Windows.Usb;

/// <summary>Raw device node data read through CfgMgr32.</summary>
internal sealed record DeviceNode
{
    public required string InstanceId { get; init; }

    public string? Description { get; init; }

    public string? FriendlyName { get; init; }

    public string? ClassName { get; init; }

    public string? Manufacturer { get; init; }

    public string? Service { get; init; }

    public string? DriverKey { get; init; }

    public string? LocationInfo { get; init; }

    public int? PortNumber { get; init; }

    public int? ProblemCode { get; init; }

    public IReadOnlyList<string> HardwareIds { get; init; } = [];

    public IReadOnlyList<string> CompatibleIds { get; init; } = [];

    public string DisplayName => FriendlyName ?? Description ?? InstanceId;
}
