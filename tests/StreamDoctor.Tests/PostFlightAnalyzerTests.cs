using StreamDoctor.Core.Diagnosis;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.PostFlight;
using StreamDoctor.Core.Usb;

namespace StreamDoctor.Tests;

public sealed class PostFlightAnalyzerTests
{
    [Fact]
    public void Analyze_ScopesFindingsToTheLastStreamSession()
    {
        var document = TestData.ParseObs("bad-session.log");
        var analyzer = new PostFlightAnalyzer(new DiagnosisEngine(DefaultRuleSet.Create()));

        var debrief = analyzer.Analyze(
            new DiagnosisContext
            {
                Events = document.Events,
                Obs = document,
                Topology = TestData.Topology(
                    TestData.Controller(
                        "USB 3.0 Controller",
                        UsbControllerKind.Usb3,
                        TestData.Device("Microphone (USB Audio Device)", UsbDeviceClass.Microphone))),
            },
            generatedAt: TestData.At(23));

        Assert.NotNull(debrief.Session);
        Assert.Equal(document.LastStreamSession!.StartedAt, debrief.Session.StartedAt);
        Assert.Contains(debrief.Findings, finding => finding.HypothesisId == "obs.frames.dropped");
        Assert.Contains(debrief.Findings, finding => finding.HypothesisId == "usb.dropout.correlated");
        Assert.Contains(debrief.Findings[0].Title, debrief.Headline, StringComparison.Ordinal);
        Assert.Contains("issue(s) found", debrief.Headline, StringComparison.Ordinal);
        Assert.NotEmpty(debrief.FollowUpChecks);
        Assert.All(debrief.Timeline, evidence => Assert.True(
            evidence.Timestamp >= debrief.Session.StartedAt &&
            evidence.Timestamp <= (debrief.Session.EndedAt ?? evidence.Timestamp)));
    }

    [Fact]
    public void Analyze_WithNoSession_SaysSoPlainly()
    {
        var debrief = new PostFlightAnalyzer(new DiagnosisEngine(DefaultRuleSet.Create()))
            .Analyze(new DiagnosisContext { Events = [] });

        Assert.Null(debrief.Session);
        Assert.Contains("No stream session", debrief.Headline, StringComparison.Ordinal);
        Assert.Empty(debrief.Findings);
        Assert.Empty(debrief.FollowUpChecks);
    }

    [Fact]
    public void Analyze_OnACleanSession_ReportsACleanHeadline()
    {
        var document = TestData.ParseObs("clean-session.log");
        var debrief = new PostFlightAnalyzer(new DiagnosisEngine(DefaultRuleSet.Create()))
            .Analyze(new DiagnosisContext { Events = document.Events, Obs = document }, generatedAt: TestData.At(23));

        Assert.Empty(debrief.Findings);
        Assert.Contains("Clean session", debrief.Headline, StringComparison.Ordinal);
        Assert.Empty(debrief.FollowUpChecks);
    }

    [Fact]
    public void Analyze_CapsFollowUpChecks()
    {
        var started = TestData.At(20);
        var findings = Enumerable.Range(0, 10)
            .Select(index => new Finding
            {
                HypothesisId = $"test.{index}",
                Module = ScanModule.UsbSentinel,
                Title = $"Issue {index}",
                Explanation = "Explanation",
                Severity = Severity.Warning,
                Confidence = 0.9,
                Evidence = [],
            })
            .ToList();

        var engine = new DiagnosisEngine([new StubRule(findings)]);
        var debrief = new PostFlightAnalyzer(engine).Analyze(
            new DiagnosisContext
            {
                Events = [],
                Obs = new Core.Obs.ObsLogDocument
                {
                    Sessions =
                    [
                        new Core.Obs.ObsSession
                        {
                            Kind = Core.Obs.ObsSessionKind.Streaming,
                            StartedAt = started,
                            EndedAt = started.AddHours(1),
                            Stats = new Core.Obs.ObsSessionStats { TotalFrames = 1000 },
                        },
                    ],
                },
            },
            generatedAt: TestData.At(23));

        Assert.Equal(6, debrief.FollowUpChecks.Count);
    }

    private sealed class StubRule : IFindingRule
    {
        private readonly IReadOnlyList<Finding> _findings;

        public StubRule(IReadOnlyList<Finding> findings) => _findings = findings;

        public string Id => "stub";

        public ScanModule OwnerModule => ScanModule.UsbSentinel;

        public IEnumerable<Finding> Evaluate(DiagnosisContext context) => _findings;
    }
}
