using StreamDoctor.Core.Model;

namespace StreamDoctor.Core.Obs;

public static partial class ObsLogParser
{
    private sealed partial class LogBuilder
    {
        private void HandleMessage(DateTimeOffset timestamp, string message)
        {
            if (PluginListHeaderRegex().IsMatch(message))
            {
                _inPluginList = true;
                return;
            }

            if (StreamingStartRegex().IsMatch(message))
            {
                OpenSession(ObsSessionKind.Streaming, timestamp);
                return;
            }

            if (StreamingStopRegex().IsMatch(message))
            {
                CloseSession(ObsSessionKind.Streaming, timestamp);
                return;
            }

            if (RecordingStartRegex().IsMatch(message))
            {
                OpenSession(ObsSessionKind.Recording, timestamp);
                return;
            }

            if (RecordingStopRegex().IsMatch(message))
            {
                CloseSession(ObsSessionKind.Recording, timestamp);
                return;
            }

            var scene = SceneRegex().Match(message);
            if (scene.Success)
            {
                AddEvent(
                    timestamp,
                    ScanEventCodes.ObsSceneSwitched,
                    Severity.Info,
                    $"Switched to scene '{scene.Groups["scene"].Value}'.",
                    P(ScanEventPayload.Scene, scene.Groups["scene"].Value));
                return;
            }

            if (HandleDeviceMessage(timestamp, message))
            {
                return;
            }

            var pluginFailure = PluginLoadFailedRegex().Match(message);
            if (pluginFailure.Success)
            {
                var plugin = pluginFailure.Groups["plugin"].Value;
                AddEvent(
                    timestamp,
                    ScanEventCodes.ObsPluginLoadFailed,
                    Severity.Warning,
                    $"OBS could not load the plugin '{plugin}'.",
                    P(ScanEventPayload.Plugin, plugin));
                return;
            }

            if (HandleStatistics(timestamp, message) || HandleConfiguration(timestamp, message))
            {
                return;
            }

            var target = StreamTargetRegex().Match(message);
            if (target.Success)
            {
                _streamTarget = new ObsStreamTarget(
                    target.Groups["scheme"].Value.ToLowerInvariant(),
                    target.Groups["host"].Value.ToLowerInvariant());

                AddEvent(
                    timestamp,
                    ScanEventCodes.ObsStreamTarget,
                    Severity.Info,
                    $"OBS connected to the ingest host {_streamTarget.Host}.",
                    P(ScanEventPayload.Host, _streamTarget.Host ?? string.Empty));
            }
        }

        private bool HandleDeviceMessage(DateTimeOffset timestamp, string message)
        {
            var withModule = SourceDisconnectedWithModuleRegex().Match(message);
            if (withModule.Success)
            {
                var device = withModule.Groups["device"].Value;
                AddEvent(
                    timestamp,
                    ScanEventCodes.ObsSourceDisconnected,
                    Severity.Warning,
                    $"OBS lost the device '{device}'.",
                    P(ScanEventPayload.Device, device),
                    P(ScanEventPayload.SourceName, withModule.Groups["source"].Value));
                return true;
            }

            var disconnected = SourceDisconnectedRegex().Match(message);
            if (disconnected.Success)
            {
                var device = disconnected.Groups["device"].Value;
                AddEvent(
                    timestamp,
                    ScanEventCodes.ObsSourceDisconnected,
                    Severity.Warning,
                    $"OBS lost the device '{device}'.",
                    P(ScanEventPayload.Device, device));
                return true;
            }

            var reconnected = SourceReconnectedRegex().Match(message);
            if (reconnected.Success)
            {
                var device = reconnected.Groups["device"].Value;
                AddEvent(
                    timestamp,
                    ScanEventCodes.ObsSourceReconnected,
                    Severity.Info,
                    $"OBS reconnected the device '{device}'.",
                    P(ScanEventPayload.Device, device));
                return true;
            }

            if (DeviceRemovedRegex().IsMatch(message))
            {
                AddEvent(
                    timestamp,
                    ScanEventCodes.ObsSourceDisconnected,
                    Severity.Warning,
                    "A capture device was removed while OBS was running.",
                    []);
                return true;
            }

            return false;
        }
    }
}
