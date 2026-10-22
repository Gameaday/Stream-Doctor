using StreamDoctor.Core.Diagnosis;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Obs;

namespace StreamDoctor.Tests;

public sealed class ObsRuleTests
{
    [Fact]
    public void FrameHealthRule_SeparatesDroppedLaggedAndSkippedOnABadSession()
    {
        var document = TestData.ParseObs("bad-session.log");
        var report = TestEvents.Analyze(new DiagnosisContext { Events = document.Events, Obs = document });

        var dropped = Assert.Single(report.Findings, finding => finding.HypothesisId == "obs.frames.dropped");
        Assert.Equal(Severity.Warning, dropped.Severity);
        Assert.Contains("4.2%", dropped.Title, StringComparison.Ordinal);
        Assert.Equal(ScanModule.FramePacing, dropped.Module);

        var lagged = Assert.Single(report.Findings, finding => finding.HypothesisId == "obs.frames.lagged");
        Assert.Equal(Severity.Warning, lagged.Severity);

        var skipped = Assert.Single(report.Findings, finding => finding.HypothesisId == "obs.frames.skipped");
        Assert.Equal(Severity.Advisory, skipped.Severity);
    }

    [Fact]
    public void FrameHealthRule_IsSilentOnACleanSession()
    {
        var document = TestData.ParseObs("clean-session.log");

        var report = TestEvents.Analyze(new DiagnosisContext { Events = document.Events, Obs = document });

        Assert.DoesNotContain(report.Findings, finding => finding.HypothesisId.StartsWith("obs.frames.", StringComparison.Ordinal));
    }

    [Fact]
    public void FrameHealthRule_EscalatesHeavyFrameLossToCritical()
    {
        var document = TestData.ParseObs("bad-session.log");
        var session = document.StreamingSessions[0] with
        {
            Stats = document.StreamingSessions[0].Stats with { DroppedFrames = 50000, DroppedPercent = 12.5 },
        };

        var report = TestEvents.Analyze(new DiagnosisContext
        {
            Events = document.Events,
            Obs = document with { Sessions = [session] },
        });

        var dropped = Assert.Single(report.Findings, finding => finding.HypothesisId == "obs.frames.dropped");
        Assert.Equal(Severity.Critical, dropped.Severity);
        Assert.True(dropped.Confidence >= 0.8);
    }

    [Fact]
    public void FrameHealthRule_UsesRecordingSessionsWhenThereIsNoStream()
    {
        var started = TestData.At(22);
        var recording = new ObsSession
        {
            Kind = ObsSessionKind.Recording,
            StartedAt = started,
            EndedAt = started.AddMinutes(45),
            Stats = new ObsSessionStats { TotalFrames = 15000, LaggedFrames = 12, LaggedPercent = 0.08 },
        };

        var document = new ObsLogDocument { Sessions = [recording], Events = [] };
        var report = TestEvents.Analyze(new DiagnosisContext { Events = [], Obs = document });

        var lagged = Assert.Single(report.Findings, finding => finding.HypothesisId == "obs.frames.lagged");
        Assert.Equal(Severity.Advisory, lagged.Severity);
    }

    [Fact]
    public void PluginRule_NamesThePluginThatFailedToLoad()
    {
        var document = TestData.ParseObs("bad-session.log");

        var report = TestEvents.Analyze(new DiagnosisContext { Events = document.Events, Obs = document });

        var finding = Assert.Single(report.Findings, candidate => candidate.HypothesisId == "obs.plugin.load-failed");
        Assert.Contains("obs-vintage-filter.dll", finding.Title, StringComparison.Ordinal);
        Assert.Equal(ScanModule.StreamingSoftware, finding.Module);
        Assert.Null(finding.FixPlaybookId);
        Assert.NotNull(finding.RecommendedAction);
    }

    [Fact]
    public void SampleRateRule_Flags44100AndAccepts48000()
    {
        var bad = TestData.ParseObs("bad-session.log");
        var clean = TestData.ParseObs("clean-session.log");

        var badReport = TestEvents.Analyze(new DiagnosisContext { Events = bad.Events, Obs = bad });
        var cleanReport = TestEvents.Analyze(new DiagnosisContext { Events = clean.Events, Obs = clean });

        var finding = Assert.Single(badReport.Findings, candidate => candidate.HypothesisId == "obs.audio.sample-rate");
        Assert.Equal(Severity.Advisory, finding.Severity);
        Assert.Equal(ScanModule.AudioChain, finding.Module);
        Assert.DoesNotContain(cleanReport.Findings, candidate => candidate.HypothesisId == "obs.audio.sample-rate");
    }
}
