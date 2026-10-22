namespace StreamDoctor.Core.Model;

/// <summary>Where a piece of evidence came from. Mirrors the design doc's data model.</summary>
public enum ScanSource
{
    /// <summary>Parsed from an OBS Studio log file.</summary>
    ObsLog,

    /// <summary>Read from the Windows System/Application event logs.</summary>
    WindowsEventLog,

    /// <summary>Enumerated device topology (controller to hub to port).</summary>
    UsbTopology,

    /// <summary>Produced by an active test-bench measurement.</summary>
    UsbTestBench,

    /// <summary>OS, driver, disk or power-plan inventory.</summary>
    SystemInventory,

    /// <summary>Local configuration files (OBS JSON, app settings).</summary>
    Config,

    /// <summary>Provided by the user, or assembled by the app itself.</summary>
    App,
}
