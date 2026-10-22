using StreamDoctor.Core.Obs;
using StreamDoctor.Core.Model;

namespace StreamDoctor.Tests;

public sealed class ObsLogParserTests
{
    [Fact]
    public void Parse_CleanSession_ExtractsHeaderSessionAndStats()
    {
        var document = TestData.ParseObs("clean-session.log");

        Assert.Equal("31.0.1", document.ObsVersion);
        Assert.Equal(48000, document.AudioSampleRate);
        Assert.Equal(new ObsVideoSettings(1920, 1080, 1920, 1080, 60), document.Video);
        Assert.Contains("obs-websocket.dll", document.LoadedPlugins);
        Assert.Contains("win-capture.dll", document.LoadedPlugins);

        var session = Assert.Single(document.Sessions);
        Assert.Equal(ObsSessionKind.Streaming, session.Kind);
        Assert.True(session.IsComplete);
        Assert.Equal(new DateTimeOffset(2026, 3, 15, 20, 0, 0, TimeSpan.Zero), session.StartedAt);
        Assert.Equal(new DateTimeOffset(2026, 3, 15, 22, 0, 1, TimeSpan.Zero), session.EndedAt);
        Assert.Equal(213000, session.Stats.TotalFrames);
        Assert.Equal(214000, session.Stats.AttemptedFrames);
        Assert.Equal(0, session.Stats.DroppedFrames);
        Assert.Equal(0, session.Stats.LaggedFrames);
        Assert.Empty(document.ParseWarnings);
    }

    [Fact]
    public void Parse_CleanSession_ScopesEventsToTheSession()
    {
        var document = TestData.ParseObs("clean-session.log");
        var session = Assert.Single(document.Sessions);

        Assert.All(session.Events, evidence => Assert.True(evidence.Timestamp >= session.StartedAt));
        Assert.DoesNotContain(session.Events, evidence => evidence.Category == ScanEventCodes.ObsPluginLoaded);
        Assert.Contains(document.Events, evidence => evidence.Category == ScanEventCodes.ObsPluginLoaded);
    }

    [Fact]
    public void Parse_BadSession_SeparatesDroppedLaggedAndSkippedFrames()
    {
        var document = TestData.ParseObs("bad-session.log");
        var streaming = Assert.Single(document.StreamingSessions);

        Assert.Equal(9000, streaming.Stats.DroppedFrames);
        Assert.Equal(4.2, streaming.Stats.DroppedPercent);
        Assert.Equal(2000, streaming.Stats.LaggedFrames);
        Assert.Equal(0.9, streaming.Stats.LaggedPercent);
        Assert.Equal(400, streaming.Stats.SkippedFrames);
        Assert.Equal(0.2, streaming.Stats.SkippedPercent);
        Assert.Equal(216000, streaming.Stats.TotalFrames);
        Assert.Equal(226000, streaming.Stats.AttemptedFrames);
        Assert.Equal(TimeSpan.FromHours(2) + TimeSpan.FromSeconds(1), streaming.Duration);
    }

    [Fact]
    public void Parse_BadSession_TracksRecordingAlongsideStreaming()
    {
        var document = TestData.ParseObs("bad-session.log");

        Assert.Equal(2, document.Sessions.Count);

        var recording = Assert.Single(document.RecordingSessions);
        Assert.Equal(new DateTimeOffset(2026, 3, 15, 22, 0, 5, TimeSpan.Zero), recording.StartedAt);
        Assert.Equal(12, recording.Stats.LaggedFrames);

        // The 0.9% lag belongs to the stream, the 0.08% lag to the recording.
        Assert.Equal(0.9, document.StreamingSessions[0].Stats.LaggedPercent);
        Assert.Equal(0.08, recording.Stats.LaggedPercent);
    }

    [Fact]
    public void Parse_SessionCrossingMidnight_RollsTheDateForward()
    {
        var document = TestData.ParseObs("midnight-session.log");
        var session = Assert.Single(document.Sessions);

        Assert.Equal(new DateTimeOffset(2026, 3, 15, 23, 50, 0, TimeSpan.Zero), session.StartedAt);
        Assert.Equal(new DateTimeOffset(2026, 3, 16, 0, 20, 1, TimeSpan.Zero), session.EndedAt);
        Assert.Equal(TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(1), session.Duration);
        Assert.Equal(5, session.Stats.DroppedFrames);
    }
}
