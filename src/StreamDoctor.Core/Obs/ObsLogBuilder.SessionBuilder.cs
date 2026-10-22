namespace StreamDoctor.Core.Obs;

public static partial class ObsLogParser
{
    private sealed partial class LogBuilder
    {
        /// <summary>Mutable session state; converted to an immutable <see cref="ObsSession"/> at the end.</summary>
        private sealed class SessionBuilder(ObsSessionKind kind, DateTimeOffset startedAt)
        {
            public ObsSessionKind Kind { get; } = kind;

            public DateTimeOffset StartedAt { get; } = startedAt;

            public DateTimeOffset? EndedAt { get; set; }

            public int? TotalFrames { get; set; }

            public int? AttemptedFrames { get; set; }

            public int? DroppedFrames { get; set; }

            public double? DroppedPercent { get; set; }

            public int? LaggedFrames { get; set; }

            public double? LaggedPercent { get; set; }

            public int? SkippedFrames { get; set; }

            public double? SkippedPercent { get; set; }

            public ObsSessionStats ToStats() => new()
            {
                TotalFrames = TotalFrames,
                AttemptedFrames = AttemptedFrames,
                DroppedFrames = DroppedFrames,
                DroppedPercent = DroppedPercent,
                LaggedFrames = LaggedFrames,
                LaggedPercent = LaggedPercent,
                SkippedFrames = SkippedFrames,
                SkippedPercent = SkippedPercent,
            };
        }
    }
}
