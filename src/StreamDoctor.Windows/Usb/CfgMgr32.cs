using System.Buffers.Binary;
using System.Text;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;

namespace StreamDoctor.Windows.Usb;

/// <summary>
/// Thin, documented wrapper over the CfgMgr32 entry points Stream Doctor uses.
/// The OSR USBView sample is the canonical reference for this walk.
/// </summary>
internal static class CfgMgr32
{
    // CM_DEVNODE_REGISTRY_PROPERTY values (cfgmgr32.h); stable ABI constants.
    private const uint DrpDeviceDesc = 0x01;
    private const uint DrpHardwareId = 0x02;
    private const uint DrpCompatibleIds = 0x03;
    private const uint DrpService = 0x04;
    private const uint DrpClass = 0x08;
    private const uint DrpDriver = 0x0A;
    private const uint DrpManufacturer = 0x0C;
    private const uint DrpFriendlyName = 0x0D;
    private const uint DrpLocationInformation = 0x0E;
    private const uint DrpAddress = 0x1D;

    private const uint DnHasProblem = 0x00000400;

    public static bool TryGetRoot(out uint devInst)
    {
        devInst = 0;
        return PInvoke.CM_Locate_DevNode(out devInst, default, CM_LOCATE_DEVNODE_FLAGS.CM_LOCATE_DEVNODE_NORMAL)
            == CONFIGRET.CR_SUCCESS;
    }

    public static string? GetDeviceInstanceId(uint devInst)
    {
        if (PInvoke.CM_Get_Device_ID_Size(out var length, devInst, 0) != CONFIGRET.CR_SUCCESS || length == 0)
        {
            return null;
        }

        var buffer = new char[length + 1];
        return PInvoke.CM_Get_Device_IDW(devInst, buffer, 0) == CONFIGRET.CR_SUCCESS
            ? new string(buffer, 0, (int)length)
            : null;
    }

    public static uint? GetChild(uint devInst) =>
        PInvoke.CM_Get_Child(out var child, devInst, 0) == CONFIGRET.CR_SUCCESS ? child : null;

    public static uint? GetSibling(uint devInst) =>
        PInvoke.CM_Get_Sibling(out var sibling, devInst, 0) == CONFIGRET.CR_SUCCESS ? sibling : null;

    public static uint? GetParent(uint devInst) =>
        PInvoke.CM_Get_Parent(out var parent, devInst, 0) == CONFIGRET.CR_SUCCESS ? parent : null;

    public static DeviceNode ReadNode(uint devInst)
    {
        var instanceId = GetDeviceInstanceId(devInst) ?? string.Empty;

        return new DeviceNode
        {
            InstanceId = instanceId,
            Description = ReadString(devInst, DrpDeviceDesc),
            FriendlyName = ReadString(devInst, DrpFriendlyName),
            ClassName = ReadString(devInst, DrpClass),
            Manufacturer = ReadString(devInst, DrpManufacturer),
            Service = ReadString(devInst, DrpService),
            DriverKey = ReadString(devInst, DrpDriver),
            LocationInfo = ReadString(devInst, DrpLocationInformation),
            PortNumber = ReadDword(devInst, DrpAddress),
            ProblemCode = ReadProblemCode(devInst),
            HardwareIds = ReadMultiString(devInst, DrpHardwareId),
            CompatibleIds = ReadMultiString(devInst, DrpCompatibleIds),
        };
    }

    private static int? ReadProblemCode(uint devInst)
    {
        if (PInvoke.CM_Get_DevNode_Status(out var status, out var problem, devInst, 0) != CONFIGRET.CR_SUCCESS)
        {
            return null;
        }

        return ((uint)status & DnHasProblem) != 0 ? unchecked((int)problem) : null;
    }

    private static string? ReadString(uint devInst, uint property)
    {
        var bytes = ReadProperty(devInst, property);
        if (bytes is null || bytes.Length < 2)
        {
            return null;
        }

        var text = Encoding.Unicode.GetString(bytes).TrimEnd('\0');
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static List<string> ReadMultiString(uint devInst, uint property)
    {
        var bytes = ReadProperty(devInst, property);
        if (bytes is null || bytes.Length < 2)
        {
            return [];
        }

        return Encoding.Unicode.GetString(bytes)
            .Split('\0', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static int? ReadDword(uint devInst, uint property)
    {
        var bytes = ReadProperty(devInst, property);
        if (bytes is null || bytes.Length < 4)
        {
            return null;
        }

        var value = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        return value == uint.MaxValue ? null : unchecked((int)value);
    }

    private static byte[]? ReadProperty(uint devInst, uint property)
    {
        var size = 512u;
        var buffer = new byte[size];

        var result = PInvoke.CM_Get_DevNode_Registry_Property(devInst, property, out _, buffer, ref size, 0);

        if (result == CONFIGRET.CR_BUFFER_SMALL)
        {
            buffer = new byte[size];
            result = PInvoke.CM_Get_DevNode_Registry_Property(devInst, property, out _, buffer, ref size, 0);
        }

        if (result != CONFIGRET.CR_SUCCESS || size == 0)
        {
            return null;
        }

        return buffer.AsSpan(0, (int)Math.Min(size, (uint)buffer.Length)).ToArray();
    }
}
