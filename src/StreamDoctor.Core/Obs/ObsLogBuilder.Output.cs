using System.Globalization;
using System.Text.RegularExpressions;
using StreamDoctor.Core.Model;

namespace StreamDoctor.Core.Obs;

public static partial class ObsLogParser
{
    private sealed partial class LogBuilder
    {
        private bool HandleSkippedFrames(DateTimeOffset timestamp, string message)
        {
            var match = SkippedFramesRegex().Match(message);
            if (!match.Success)
            {
                return false;
            }

            var count = ParseInt(match, "count");
            var total = ParseInt(match, "total");
            var percent = ParseDouble(match, "percent");
            var session = FindSession(match.Groups["output"].Value, null);
            if (session is not null)
            {
                session.SkippedFrames = count ?? session.SkippedFrames;
                session.SkippedPercent = percent ?? session.SkippedPercent;
                session.AttemptedFrames = total ?? session.AttemptedFrames;
            }

            AddEvent(
                timestamp,
                ScanEventCodes.ObsEncoderOverload,
                count is > 0 ? Severity.Warning : Severity.Info,
                $"OBS skipped {Format(count)} of {Format(total)} frames ({FormatPercent(percent)}): the encoder is overloaded.",
                P(ScanEventPayload.Count, Format(count) ?? "0"),
                P(ScanEventPayload.TotalFrames, Format(total) ?? "0"),
                P(ScanEventPayload.Percent, FormatPercent(percent) ?? "0"));
            return true;
        }

        private bool HandleTotalFrames(string message)
        {
            var match = TotalFramesRegex().Match(message);
            if (!match.Success)
            {
                return false;
            }

            var session = FindSession(match.Groups["output"].Value, null);
            if (session is not null)
            {
                session.TotalFrames = ParseInt(match, "count") ?? session.TotalFrames;
            }

            return true;
        }

        public ObsLogDocument Build()
        {
            var events = _events
                .OrderBy(static evidence => evidence.Timestamp)
                .ThenBy(static evidence => evidence.Sequence)
                .ToList();

            var sessions = _sessions
                .OrderBy(static session => session.StartedAt)
                .Select(session => new ObsSession
                {
                    Kind = session.Kind,
                    StartedAt = session.StartedAt,
                    EndedAt = session.EndedAt,
                    Stats = session.ToStats(),
                    Events = events
                        .Where(evidence =>
                            evidence.Timestamp >= session.StartedAt &&
                            (session.EndedAt is null || evidence.Timestamp <= session.EndedAt))
                        .ToList(),
                })
                .ToList();

            var video = _baseWidth is null && _outputWidth is null && _fps is null
                ? null
                : new ObsVideoSettings(_baseWidth, _baseHeight, _outputWidth, _outputHeight, _fps);

            return new ObsLogDocument
            {
                ObsVersion = _version,
                LogFileName = _fileName,
                Sessions = sessions,
                Events = events,
                LoadedPlugins = _plugins,
                Video = video,
                AudioSampleRate = _audioSampleRate,
                StreamTarget = _streamTarget,
                ParseWarnings = _warnings,
            };
        }

        private static int? ParseInt(Match match, string group) =>
            match.Groups[group].Success &&
            int.TryParse(match.Groups[group].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;

        private static double? ParseDouble(Match match, string group)
        {
            if (!match.Groups[group].Success)
            {
                return null;
            }

            var normalized = match.Groups[group].Value.Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
        }

        private static string? Format(int? value) => value?.ToString(CultureInfo.InvariantCulture);

        private static string? FormatPercent(double? value) =>
            value is null ? null : value.Value.ToString("0.0#", CultureInfo.InvariantCulture) + "%";
    }
}
