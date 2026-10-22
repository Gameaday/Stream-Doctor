using StreamDoctor.Core.Model;

namespace StreamDoctor.Core.Obs;

public static partial class ObsLogParser
{
    private sealed partial class LogBuilder
    {
        private void OpenSession(ObsSessionKind kind, DateTimeOffset timestamp)
        {
            // A duplicate start marker means OBS restarted the output without logging
            // a stop; close the dangling session instead of dropping its stats.
            var open = _sessions.LastOrDefault(session => session.Kind == kind && session.EndedAt is null);
            if (open is not null)
            {
                open.EndedAt = timestamp;
                _warnings.Add($"Duplicate {kind} start; closed the previous session.");
            }

            _sessions.Add(new SessionBuilder(kind, timestamp));
            AddEvent(
                timestamp,
                kind == ObsSessionKind.Streaming ? ScanEventCodes.ObsStreamStarted : ScanEventCodes.ObsRecordingStarted,
                Severity.Info,
                kind == ObsSessionKind.Streaming ? "Stream started." : "Recording started.");
        }

        private void CloseSession(ObsSessionKind kind, DateTimeOffset timestamp)
        {
            var open = _sessions.LastOrDefault(session => session.Kind == kind && session.EndedAt is null);
            if (open is null)
            {
                _warnings.Add($"{kind} stop without a matching start.");
                return;
            }

            open.EndedAt = timestamp;
            AddEvent(
                timestamp,
                kind == ObsSessionKind.Streaming ? ScanEventCodes.ObsStreamStopped : ScanEventCodes.ObsRecordingStopped,
                Severity.Info,
                kind == ObsSessionKind.Streaming ? "Stream stopped." : "Recording stopped.");
        }

        private SessionBuilder? FindSession(string? output, ObsSessionKind? fallbackKind)
        {
            var kind = string.IsNullOrEmpty(output) ? fallbackKind : KindFor(output) ?? fallbackKind;

            if (kind is null)
            {
                return _sessions.LastOrDefault(static session => session.EndedAt is null) ?? _sessions.LastOrDefault();
            }

            return _sessions.LastOrDefault(session => session.Kind == kind && session.EndedAt is null)
                ?? _sessions.LastOrDefault(session => session.Kind == kind);
        }

        private bool HandleStatistics(DateTimeOffset timestamp, string message) =>
            HandleDroppedFrames(timestamp, message)
            || HandleLaggedFrames(timestamp, message)
            || HandleSkippedFrames(timestamp, message)
            || HandleTotalFrames(message);

        private bool HandleDroppedFrames(DateTimeOffset timestamp, string message)
        {
            var match = DroppedFramesRegex().Match(message);
            if (!match.Success)
            {
                return false;
            }

            var count = ParseInt(match, "count");
            var percent = ParseDouble(match, "percent");
            var session = FindSession(match.Groups["output"].Value, ObsSessionKind.Streaming);
            if (session is not null)
            {
                session.DroppedFrames = count ?? session.DroppedFrames;
                session.DroppedPercent = percent ?? session.DroppedPercent;
            }

            AddEvent(
                timestamp,
                ScanEventCodes.ObsFramesDropped,
                count is > 0 ? Severity.Warning : Severity.Info,
                $"OBS dropped {Format(count)} frames ({FormatPercent(percent)}): the network is the bottleneck.",
                P(ScanEventPayload.Count, Format(count) ?? "0"),
                P(ScanEventPayload.Percent, FormatPercent(percent) ?? "0"));
            return true;
        }

        private bool HandleLaggedFrames(DateTimeOffset timestamp, string message)
        {
            var match = LaggedFramesRegex().Match(message);
            if (!match.Success)
            {
                return false;
            }

            var count = ParseInt(match, "count");
            var percent = ParseDouble(match, "percent");
            var session = FindSession(match.Groups["output"].Value, null);
            if (session is not null)
            {
                session.LaggedFrames = count ?? session.LaggedFrames;
                session.LaggedPercent = percent ?? session.LaggedPercent;
            }

            AddEvent(
                timestamp,
                ScanEventCodes.ObsRenderLag,
                count is > 0 ? Severity.Warning : Severity.Info,
                $"OBS lagged {Format(count)} frames ({FormatPercent(percent)}): the GPU could not render in time.",
                P(ScanEventPayload.Count, Format(count) ?? "0"),
                P(ScanEventPayload.Percent, FormatPercent(percent) ?? "0"));
            return true;
        }

        private static ObsSessionKind? KindFor(string output)
        {
            if (output.Contains("stream", StringComparison.OrdinalIgnoreCase))
            {
                return ObsSessionKind.Streaming;
            }

            if (output.Contains("record", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("file", StringComparison.OrdinalIgnoreCase))
            {
                return ObsSessionKind.Recording;
            }

            return null;
        }
    }
}
