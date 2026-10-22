using System.Globalization;
using StreamDoctor.Core.Model;

namespace StreamDoctor.Core.Obs;

public static partial class ObsLogParser
{
    private sealed partial class LogBuilder
    {
        private bool HandleConfiguration(DateTimeOffset timestamp, string message)
        {
            var rate = AudioRateRegex().Match(message);
            if (rate.Success && ParseInt(rate, "rate") is { } sampleRate)
            {
                _audioSampleRate = sampleRate;
                AddEvent(
                    timestamp,
                    ScanEventCodes.ObsAudioSettings,
                    Severity.Info,
                    $"OBS audio is running at {sampleRate} Hz.",
                    P(ScanEventPayload.Rate, sampleRate.ToString(CultureInfo.InvariantCulture)));
                return true;
            }

            var baseResolution = BaseResolutionRegex().Match(message);
            if (baseResolution.Success &&
                ParseInt(baseResolution, "w") is { } baseWidth &&
                ParseInt(baseResolution, "h") is { } baseHeight)
            {
                _baseWidth = baseWidth;
                _baseHeight = baseHeight;
                return true;
            }

            var outputResolution = OutputResolutionRegex().Match(message);
            if (outputResolution.Success &&
                ParseInt(outputResolution, "w") is { } outputWidth &&
                ParseInt(outputResolution, "h") is { } outputHeight)
            {
                _outputWidth = outputWidth;
                _outputHeight = outputHeight;
                return true;
            }

            var fps = FpsRegex().Match(message);
            if (fps.Success && ParseInt(fps, "fps") is { } framesPerSecond)
            {
                _fps = framesPerSecond;
                return true;
            }

            return false;
        }

        private void AddEvent(
            DateTimeOffset timestamp,
            string category,
            Severity severity,
            string message,
            params KeyValuePair<string, string>[] payload)
        {
            _events.Add(ScanEvent.Create(
                timestamp,
                ScanSource.ObsLog,
                ModuleFor(category),
                category,
                severity,
                message,
                payload,
                ++_sequence));
        }

        private static ScanModule ModuleFor(string category) => category switch
        {
            ScanEventCodes.ObsPluginLoadFailed or ScanEventCodes.ObsPluginLoaded => ScanModule.StreamingSoftware,
            ScanEventCodes.ObsVideoSettings or ScanEventCodes.ObsAudioSettings => ScanModule.ObsConfig,
            ScanEventCodes.ObsStreamTarget => ScanModule.NetworkHealth,
            ScanEventCodes.ObsSourceDisconnected or ScanEventCodes.ObsSourceReconnected => ScanModule.CaptureCard,
            _ => ScanModule.FramePacing,
        };
    }
}
