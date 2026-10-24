using StreamDoctor.Core.Obs;
using StreamDoctor.Core.Model;

namespace StreamDoctor.Tests;

public sealed class ObsLogParserEdgeCaseTests
{
    [Fact]
    public void Parse_BadSession_CountsDeviceDropoutsScenesAndPluginFailures()
    {
        var document = TestData.ParseObs("bad-session.log");

        var dropouts = document.Events
            .Where(evidence => evidence.Category == ScanEventCodes.ObsSourceDisconnected)
            .ToList();

        Assert.Equal(3, dropouts.Count);
        Assert.All(dropouts, dropout =>
            Assert.Equal("Microphone (USB Audio Device)", dropout.GetPayload(ScanEventPayload.Device)));
        Assert.All(dropouts, dropout => Assert.Equal(Severity.Warning, dropout.Severity));

        Assert.Equal(3, document.Events.Count(evidence => evidence.Category == ScanEventCodes.ObsSceneSwitched));
        Assert.Single(document.Events, evidence => evidence.Category == ScanEventCodes.ObsSourceReconnected);

        var pluginFailure = Assert.Single(document.Events, evidence => evidence.Category == ScanEventCodes.ObsPluginLoadFailed);
        Assert.Equal("obs-vintage-filter.dll", pluginFailure.GetPayload(ScanEventPayload.Plugin));
        Assert.DoesNotContain("obs-vintage-filter.dll", document.LoadedPlugins);

        Assert.Equal(44100, document.AudioSampleRate);
    }

    [Fact]
    public void Parse_NeverCapturesTheStreamKey()
    {
        var document = TestData.ParseObs("bad-session.log");

        Assert.NotNull(document.StreamTarget);
        Assert.Equal("rtmp", document.StreamTarget.Scheme);
        Assert.Equal("live.twitch.tv", document.StreamTarget.Host);

        Assert.DoesNotContain(
            document.Events,
            evidence => evidence.Message.Contains("live_123_secret_key", StringComparison.Ordinal) ||
                        evidence.Payload.Values.Any(value => value.Contains("live_123_secret_key", StringComparison.Ordinal)));
    }

    [Fact]
    public void Parse_UnclosedSession_IsReportedAsIncomplete()
    {
        const string Log = """
            20:00:00.000: ==== Streaming Start ===============================================
            20:10:00.000: Switched to scene 'Gameplay'
            """;

        var document = ObsLogParser.Parse(Log, TestData.LogDate, TimeZoneInfo.Utc);

        var session = Assert.Single(document.Sessions);
        Assert.False(session.IsComplete);
        Assert.Null(session.EndedAt);
        Assert.True(session.Duration >= TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void Parse_StopWithoutStart_RecordsAParseWarningInsteadOfThrowing()
    {
        const string Log = "20:00:00.000: ==== Streaming Stop ==========================================";

        var document = ObsLogParser.Parse(Log, TestData.LogDate, TimeZoneInfo.Utc);

        Assert.Empty(document.Sessions);
        Assert.Single(document.ParseWarnings);
        Assert.Contains("without a matching start", document.ParseWarnings[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_EmptyLog_ReturnsEmptyDocument()
    {
        var document = ObsLogParser.Parse(string.Empty, TestData.LogDate, TimeZoneInfo.Utc);

        Assert.Empty(document.Sessions);
        Assert.Empty(document.Events);
        Assert.Null(document.ObsVersion);
        Assert.Null(document.LastStreamSession);
    }

    [Fact]
    public void Parse_UsesTheProvidedTimeZone()
    {
        const string Log = """
            20:00:00.000: ==== Streaming Start ===============================================
            20:05:00.000: ==== Streaming Stop ==========================================
            """;

        var timeZone = TimeZoneInfo.CreateCustomTimeZone("Test-2", TimeSpan.FromHours(2), "Test-2", "Test-2");
        var document = ObsLogParser.Parse(Log, TestData.LogDate, timeZone);

        var session = Assert.Single(document.Sessions);
        Assert.Equal(TimeSpan.FromHours(2), session.StartedAt.Offset);
        Assert.Equal(new DateTimeOffset(2026, 3, 15, 18, 0, 0, TimeSpan.Zero), session.StartedAt);
    }

    [Fact]
    public void Parse_ToleratesTrailingWhitespaceOnStatLines()
    {
        const string Log = """
            20:00:00.000: ==== Streaming Start ===============================================   
            20:01:00.000: Output 'simple_stream': Total frames output: 100   
            20:01:00.000: Output 'simple_stream': Number of dropped frames due to insufficient bandwidth: 5 (5.0%)   
            20:02:00.000: ==== Streaming Stop ==========================================
            """;

        var document = ObsLogParser.Parse(Log, TestData.LogDate, TimeZoneInfo.Utc);
        var session = Assert.Single(document.Sessions);

        Assert.True(session.IsComplete);
        Assert.Equal(100, session.Stats.TotalFrames);
        Assert.Equal(5, session.Stats.DroppedFrames);
        Assert.Equal(5.0, session.Stats.DroppedPercent);
    }

    [Fact]
    public void Parse_DuplicateStartMarker_ClosesThePreviousSession()
    {
        const string Log = """
            20:00:00.000: ==== Streaming Start ===============================================
            20:30:00.000: ==== Streaming Start ===============================================
            20:45:00.000: ==== Streaming Stop ==========================================
            """;

        var document = ObsLogParser.Parse(Log, TestData.LogDate, TimeZoneInfo.Utc);

        Assert.Equal(2, document.Sessions.Count);
        Assert.Equal(new DateTimeOffset(2026, 3, 15, 20, 30, 0, TimeSpan.Zero), document.Sessions[0].EndedAt);
        Assert.True(document.Sessions[1].IsComplete);
        Assert.Single(document.ParseWarnings);
    }
}
