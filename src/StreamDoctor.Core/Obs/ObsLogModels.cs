using StreamDoctor.Core.Model;

namespace StreamDoctor.Core.Obs;

/// <summary>Which output a session belongs to. OBS can stream and record at once.</summary>
public enum ObsSessionKind
{
    Streaming,
    Recording,
}

/// <summary>
/// Session statistics as OBS reports them at the end of an output. Percentages are
/// the ones OBS itself printed; counts are authoritative.
/// </summary>
public sealed record ObsSessionStats
{
    /// <summary>Frames OBS actually output ("Total frames output").</summary>
    public int? TotalFrames { get; init; }

    /// <summary>Frames OBS attempted to render, from the skipped-frames line.</summary>
    public int? AttemptedFrames { get; init; }

    /// <summary>Dropped frames: the network could not keep up (design doc §2.2).</summary>
    public int? DroppedFrames { get; init; }

    public double? DroppedPercent { get; init; }

    /// <summary>Lagged frames: the GPU could not render in time.</summary>
    public int? LaggedFrames { get; init; }

    public double? LaggedPercent { get; init; }

    /// <summary>Skipped frames: the encoder could not keep up.</summary>
    public int? SkippedFrames { get; init; }

    public double? SkippedPercent { get; init; }

    public bool HasAny =>
        TotalFrames is not null ||
        AttemptedFrames is not null ||
        DroppedFrames is not null ||
        LaggedFrames is not null ||
        SkippedFrames is not null;
}

/// <summary>One start → stop window from an OBS log.</summary>
public sealed record ObsSession
{
    public required ObsSessionKind Kind { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? EndedAt { get; init; }

    public ObsSessionStats Stats { get; init; } = new();

    /// <summary>Events that happened inside this session window.</summary>
    public IReadOnlyList<ScanEvent> Events { get; init; } = [];

    /// <summary>
    /// Known duration of the session. For a session that never logged a stop marker
    /// (OBS crashed), this is the time of the last log line, i.e. a lower bound.
    /// </summary>
    public TimeSpan Duration
    {
        get
        {
            var end = EndedAt ?? (Events.Count > 0 ? Events[^1].Timestamp : StartedAt);
            var duration = end - StartedAt;
            return duration > TimeSpan.Zero ? duration : TimeSpan.Zero;
        }
    }

    /// <summary>False when the log ends mid-stream (OBS crashed or was killed).</summary>
    public bool IsComplete => EndedAt is not null;
}

/// <summary>Video settings OBS logged at startup, used by config lint (M2).</summary>
public sealed record ObsVideoSettings(
    int? BaseWidth,
    int? BaseHeight,
    int? OutputWidth,
    int? OutputHeight,
    int? Fps);

/// <summary>Where the stream was sent. The stream key is never captured.</summary>
public sealed record ObsStreamTarget(string? Scheme, string? Host);

/// <summary>Everything one OBS log file told us.</summary>
public sealed record ObsLogDocument
{
    public string? ObsVersion { get; init; }

    public string? LogFileName { get; init; }

    /// <summary>Streaming and recording sessions, ordered by start time.</summary>
    public IReadOnlyList<ObsSession> Sessions { get; init; } = [];

    /// <summary>All normalized events from the log, oldest first.</summary>
    public IReadOnlyList<ScanEvent> Events { get; init; } = [];

    public IReadOnlyList<string> LoadedPlugins { get; init; } = [];

    public ObsVideoSettings? Video { get; init; }

    public int? AudioSampleRate { get; init; }

    public ObsStreamTarget? StreamTarget { get; init; }

    /// <summary>Lines that looked important but could not be parsed.</summary>
    public IReadOnlyList<string> ParseWarnings { get; init; } = [];

    public IReadOnlyList<ObsSession> StreamingSessions =>
        Sessions.Where(static session => session.Kind == ObsSessionKind.Streaming).ToList();

    public IReadOnlyList<ObsSession> RecordingSessions =>
        Sessions.Where(static session => session.Kind == ObsSessionKind.Recording).ToList();

    /// <summary>The most recent complete streaming session, or the last one seen.</summary>
    public ObsSession? LastStreamSession
    {
        get
        {
            var streaming = StreamingSessions;
            for (var index = streaming.Count - 1; index >= 0; index--)
            {
                if (streaming[index].IsComplete)
                {
                    return streaming[index];
                }
            }

            return streaming.Count == 0 ? null : streaming[streaming.Count - 1];
        }
    }

    public DateTimeOffset? FirstTimestamp => Events.Count == 0 ? null : Events[0].Timestamp;

    public DateTimeOffset? LastTimestamp => Events.Count == 0 ? null : Events[^1].Timestamp;

    public static ObsLogDocument Empty { get; } = new();
}
