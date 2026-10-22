namespace StreamDoctor.Core.Model;

/// <summary>
/// The feature modules from the product design document (§2). Every event and
/// finding is tagged with exactly one module so the dashboard can render one
/// gauge per subsystem.
/// </summary>
public enum ScanModule
{
    UsbSentinel,
    FramePacing,
    StreamingSoftware,
    Peripherals,
    NetworkHealth,
    AudioChain,
    Thermals,
    CaptureCard,
    WindowsInterference,
    SecurityPrivacy,
    DiskRecording,
    Hotkeys,
    ObsConfig,
}

public static class ScanModuleExtensions
{
    /// <summary>Streamer-facing name used in the UI and in finding explanations.</summary>
    public static string DisplayName(this ScanModule module) => module switch
    {
        ScanModule.UsbSentinel => "USB Sentinel",
        ScanModule.FramePacing => "Frame Pacing",
        ScanModule.StreamingSoftware => "Streaming Software",
        ScanModule.Peripherals => "Peripherals",
        ScanModule.NetworkHealth => "Network Health",
        ScanModule.AudioChain => "Audio Chain",
        ScanModule.Thermals => "Thermals & Headroom",
        ScanModule.CaptureCard => "Capture Card",
        ScanModule.WindowsInterference => "Windows Interference",
        ScanModule.SecurityPrivacy => "Privacy & Security",
        ScanModule.DiskRecording => "Disk & Recording",
        ScanModule.Hotkeys => "Hotkeys",
        ScanModule.ObsConfig => "OBS Config",
        _ => module.ToString(),
    };
}
