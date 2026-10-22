namespace StreamDoctor.Core.Model;

/// <summary>
/// Canonical evidence categories. Rules and tests use these constants instead of
/// string literals so a renamed category fails the build rather than silently
/// disabling a diagnosis.
/// </summary>
public static class ScanEventCodes
{
    // USB / device evidence
    public const string UsbControllerObserved = "usb.controller.observed";
    public const string UsbDeviceObserved = "usb.device.observed";
    public const string UsbDeviceRemoved = "usb.device.removed";
    public const string UsbDeviceArrived = "usb.device.arrived";
    public const string UsbDeviceReset = "usb.device-reset";
    public const string UsbControllerError = "usb.controller-error";
    public const string UsbPowerFlag = "usb.power.flag";
    public const string UsbTopologyChanged = "usb.topology.changed";
    public const string UsbDeviceProblem = "usb.device.problem";

    // OBS evidence
    public const string ObsStreamStarted = "obs.stream.started";
    public const string ObsStreamStopped = "obs.stream.stopped";
    public const string ObsRecordingStarted = "obs.recording.started";
    public const string ObsRecordingStopped = "obs.recording.stopped";
    public const string ObsSceneSwitched = "obs.scene.switched";
    public const string ObsSourceDisconnected = "obs.source.disconnected";
    public const string ObsSourceReconnected = "obs.source.reconnected";
    public const string ObsPluginLoadFailed = "obs.plugin.load-failed";
    public const string ObsPluginLoaded = "obs.plugin.loaded";
    public const string ObsEncoderOverload = "obs.encoder.overload";
    public const string ObsRenderLag = "obs.render.lag";
    public const string ObsFramesDropped = "obs.frames.dropped";
    public const string ObsStreamTarget = "obs.stream.target";
    public const string ObsVideoSettings = "obs.video.settings";
    public const string ObsAudioSettings = "obs.audio.settings";
    public const string ObsParseWarning = "obs.log.parse-warning";

    // System evidence
    public const string AppCrashed = "app.crashed";
    public const string ServiceFailed = "service.failed";
    public const string GpuDriverReset = "gpu.driver-reset";
    public const string PowerPlanObserved = "system.power-plan.observed";
    public const string SystemPowerEvent = "system.power.event";
    public const string DiskSpaceObserved = "disk.space.observed";
    public const string OperatingSystemObserved = "system.os.observed";
    public const string HardwareObserved = "system.hardware.observed";
    public const string DriverObserved = "system.driver.observed";
    public const string WindowsUpdateInstalled = "windows.update.installed";
}

/// <summary>Well-known payload keys, shared by collectors, rules and the UI.</summary>
public static class ScanEventPayload
{
    public const string Device = "device";
    public const string DeviceInstanceId = "device.instanceId";
    public const string DeviceClass = "device.class";
    public const string Controller = "controller";
    public const string ControllerKind = "controller.kind";
    public const string Port = "port";
    public const string Hub = "hub";
    public const string Speed = "speed";
    public const string Scene = "scene";
    public const string SourceName = "source";
    public const string Plugin = "plugin";
    public const string Host = "host";
    public const string Count = "count";
    public const string Percent = "percent";
    public const string Rate = "rate";
    public const string Utilization = "utilization";
    public const string Devices = "devices";
    public const string TotalFrames = "totalFrames";
    public const string Plan = "plan";
    public const string Drive = "drive";
    public const string FreeBytes = "freeBytes";
    public const string TotalBytes = "totalBytes";
    public const string Version = "version";
    public const string Provider = "provider";
    public const string LogName = "windows.log";
    public const string EventId = "eventId";

    public const string FixPlaybook = "fix.playbook";
}
