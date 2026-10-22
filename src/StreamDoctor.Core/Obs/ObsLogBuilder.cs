using System.Globalization;
using StreamDoctor.Core.Model;

namespace StreamDoctor.Core.Obs;

public static partial class ObsLogParser
{
    /// <summary>Single-pass mutable state for one log parse.</summary>
    private sealed partial class LogBuilder
    {
        private readonly string? _fileName;
        private readonly List<ScanEvent> _events = [];
        private readonly List<SessionBuilder> _sessions = [];
        private readonly List<string> _plugins = [];
        private readonly List<string> _warnings = [];
        private readonly HashSet<string> _pluginSet = new(StringComparer.OrdinalIgnoreCase);
        private readonly TimeZoneInfo _timeZone;

        private DateOnly _currentDate;
        private TimeSpan _lastTime;
        private long _sequence;
        private bool _inPluginList;
        private string? _version;
        private ObsStreamTarget? _streamTarget;
        private int? _baseWidth;
        private int? _baseHeight;
        private int? _outputWidth;
        private int? _outputHeight;
        private int? _fps;
        private int? _audioSampleRate;

        public LogBuilder(DateOnly logDate, TimeZoneInfo timeZone, string? fileName)
        {
            _currentDate = logDate;
            _timeZone = timeZone;
            _fileName = fileName;
        }

        public void Consume(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                _inPluginList = false;
                return;
            }

            var match = LineRegex().Match(line);
            if (!match.Success)
            {
                // Un-timestamped header lines still carry the OBS version.
                if (_version is null)
                {
                    var headerVersion = ObsVersionRegex().Match(line);
                    if (headerVersion.Success)
                    {
                        _version = headerVersion.Groups["version"].Value;
                    }
                }

                _inPluginList = false;
                return;
            }

            if (!TimeSpan.TryParseExact(
                    match.Groups["time"].Value,
                    @"hh\:mm\:ss\.fff",
                    CultureInfo.InvariantCulture,
                    out var time))
            {
                _warnings.Add($"Unreadable timestamp '{match.Groups["time"].Value}'.");
                return;
            }

            var timestamp = ResolveTimestamp(time);
            var message = match.Groups["message"].Value;

            if (_version is null)
            {
                var versionMatch = ObsVersionRegex().Match(message);
                if (versionMatch.Success)
                {
                    _version = versionMatch.Groups["version"].Value;
                }
            }

            if (_inPluginList)
            {
                var plugin = PluginListEntryRegex().Match(message);
                if (plugin.Success)
                {
                    var name = plugin.Groups["plugin"].Value;
                    if (_pluginSet.Add(name))
                    {
                        _plugins.Add(name);
                    }

                    AddEvent(
                        timestamp,
                        ScanEventCodes.ObsPluginLoaded,
                        Severity.Info,
                        $"OBS loaded the plugin '{name}'.",
                        P(ScanEventPayload.Plugin, name));
                    return;
                }

                _inPluginList = false;
            }

            HandleMessage(timestamp, message);
        }

        private DateTimeOffset ResolveTimestamp(TimeSpan time)
        {
            // OBS logs cross midnight without changing the date; a large backwards
            // jump means the log rolled over to the next day.
            if (time < _lastTime - TimeSpan.FromHours(12))
            {
                _currentDate = _currentDate.AddDays(1);
            }

            _lastTime = time;

            var local = _currentDate.ToDateTime(TimeOnly.MinValue).Add(time);
            return new DateTimeOffset(local, _timeZone.GetUtcOffset(local));
        }

        private void AddPlugin(string plugin)
        {
            if (_pluginSet.Add(plugin))
            {
                _plugins.Add(plugin);
            }
        }

        private static KeyValuePair<string, string> P(string key, string value) => new(key, value);
    }
}
