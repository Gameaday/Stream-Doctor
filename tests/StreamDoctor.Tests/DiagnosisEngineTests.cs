using StreamDoctor.Core.Diagnosis;
using StreamDoctor.Core.Model;

namespace StreamDoctor.Tests;

public sealed class DiagnosisEngineTests
{
    [Fact]
    public void Analyze_RanksFindingsByConfidenceThenSeverity()
    {
        var engine = new DiagnosisEngine(
            [
                new StubRule("low", Finding("low", 0.2, Severity.Critical)),
                new StubRule("high", Finding("high", 0.9, Severity.Advisory)),
            ]);

        var report = engine.Analyze(TestContext(), TestData.At(12));

        Assert.Equal("high", report.Findings[0].HypothesisId);
        Assert.Equal("low", report.Findings[1].HypothesisId);
    }

    [Fact]
    public void Analyze_MergesDuplicateHypothesesForTheSameTarget()
    {
        var first = Finding("usb.power.selective-suspend", 0.4, Severity.Warning, "device-a", TestData.At(10));
        var second = Finding("usb.power.selective-suspend", 0.8, Severity.Warning, "device-a", TestData.At(11));

        var report = new DiagnosisEngine([new StubRule("a", first), new StubRule("b", second)])
            .Analyze(TestContext(), TestData.At(12));

        var merged = Assert.Single(report.Findings);
        Assert.Equal(0.8, merged.Confidence, precision: 3);
        Assert.Equal(2, merged.Evidence.Count);
    }

    [Fact]
    public void Analyze_KeepsFindingsForDifferentTargetsSeparate()
    {
        var report = new DiagnosisEngine(
            [
                new StubRule(
                    "a",
                    Finding("usb.power.selective-suspend", 0.4, Severity.Warning, "device-a"),
                    Finding("usb.power.selective-suspend", 0.4, Severity.Warning, "device-b")),
            ]).Analyze(TestContext(), TestData.At(12));

        Assert.Equal(2, report.Findings.Count);
    }

    [Fact]
    public void Analyze_IsolatesAFaultyRuleInsteadOfFailingTheSweep()
    {
        var engine = new DiagnosisEngine([new ThrowingRule(), new StubRule("good", Finding("good", 0.5, Severity.Advisory))]);

        var report = engine.Analyze(TestContext(), TestData.At(12));

        Assert.Single(report.Findings);
        var failure = Assert.Single(report.RuleFailures);
        Assert.Equal("throwing", failure.RuleId);
        Assert.Contains("boom", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_TrimsEvidenceToTheConfiguredCapKeepingTheNewest()
    {
        var evidence = Enumerable.Range(0, 40)
            .Select(index => new ScanEvent
            {
                Timestamp = TestData.At(10).AddMinutes(index),
                Source = ScanSource.ObsLog,
                Module = ScanModule.FramePacing,
                Category = ScanEventCodes.ObsSourceDisconnected,
                Severity = Severity.Warning,
                Sequence = index,
            })
            .ToList();

        var finding = Finding("cap", 0.5, Severity.Warning) with { Evidence = evidence };
        var engine = new DiagnosisEngine([new StubRule("cap", finding)], new DiagnosisOptions { MaxEvidencePerFinding = 5 });

        var result = Assert.Single(engine.Analyze(TestContext(), TestData.At(12)).Findings);

        Assert.Equal(5, result.Evidence.Count);
        Assert.Equal(evidence[^1].Timestamp, result.Evidence[^1].Timestamp);
    }

    [Fact]
    public void Analyze_WithNothingScanned_ReportsUnknownInsteadOfPerfectHealth()
    {
        var report = new DiagnosisEngine([]).Analyze(new DiagnosisContext { Events = [] }, TestData.At(12));

        Assert.Equal(HealthState.Unknown, report.State);
        Assert.All(report.Modules, module => Assert.False(module.WasScanned));
    }

    [Fact]
    public void Analyze_ClampsFindingConfidenceIntoRange()
    {
        var finding = Finding("clamp", 5.0, Severity.Warning);

        Assert.Equal(1.0, finding.Confidence);
        Assert.Equal(0, (finding with { Confidence = -3 }).Confidence);
        Assert.Equal(0, (finding with { Confidence = double.NaN }).Confidence);
    }

    [Fact]
    public void Analyze_RespectsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var engine = new DiagnosisEngine([new StubRule("a", Finding("a", 0.5, Severity.Advisory))]);

        Assert.Throws<OperationCanceledException>(() => engine.Analyze(TestContext(), TestData.At(12), cancellation.Token));
    }

    private static DiagnosisContext TestContext() => new()
    {
        Events = [],
        ScannedModules = [ScanModule.FramePacing],
    };

    private static Finding Finding(
        string hypothesisId,
        double confidence,
        Severity severity,
        string target = "x",
        DateTimeOffset? timestamp = null) => new()
    {
        HypothesisId = hypothesisId,
        Module = ScanModule.UsbSentinel,
        Title = "Title",
        Explanation = "Explanation",
        Severity = severity,
        Confidence = confidence,
        Evidence =
        [
            new ScanEvent
            {
                Timestamp = timestamp ?? TestData.At(10),
                Source = ScanSource.App,
                Module = ScanModule.UsbSentinel,
                Category = "test.evidence",
                Severity = severity,
            },
        ],
        Data = new Dictionary<string, string>(StringComparer.Ordinal) { ["target"] = target },
    };

    private sealed class StubRule : IFindingRule
    {
        private readonly Finding[] _findings;

        public StubRule(string id, params Finding[] findings)
        {
            Id = id;
            _findings = findings;
        }

        public string Id { get; }

        public ScanModule OwnerModule => ScanModule.UsbSentinel;

        public IEnumerable<Finding> Evaluate(DiagnosisContext context) => _findings;
    }

    private sealed class ThrowingRule : IFindingRule
    {
        public string Id => "throwing";

        public ScanModule OwnerModule => ScanModule.UsbSentinel;

        public IEnumerable<Finding> Evaluate(DiagnosisContext context) =>
            throw new InvalidOperationException("boom");
    }
}
