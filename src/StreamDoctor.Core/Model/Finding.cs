namespace StreamDoctor.Core.Model;

/// <summary>
/// A ranked root-cause hypothesis with the evidence that supports it and the
/// playbook that fixes it. Mirrors the design doc's finding model
/// <c>{ hypothesisId, confidence, evidence, fixPlaybookId }</c>.
/// </summary>
public sealed record Finding
{
    private readonly double _confidence;
    private readonly IReadOnlyList<ScanEvent> _evidence = [];

    /// <summary>Stable rule identifier, e.g. <c>usb.bandwidth.saturation</c>.</summary>
    public required string HypothesisId { get; init; }

    /// <summary>Module that owns this finding (drives the dashboard gauges).</summary>
    public required ScanModule Module { get; init; }

    /// <summary>Short, specific headline: "Your webcam shares a controller with your capture card".</summary>
    public required string Title { get; init; }

    /// <summary>
    /// Plain-language explanation in the voice of the design doc: what the user
    /// experienced and why, not raw Windows jargon.
    /// </summary>
    public required string Explanation { get; init; }

    public required Severity Severity { get; init; }

    /// <summary>Confidence in the hypothesis, always clamped to the range 0..1.</summary>
    public double Confidence
    {
        get => _confidence;
        init => _confidence = double.IsFinite(value) ? Math.Clamp(value, 0d, 1d) : 0d;
    }

    /// <summary>Evidence bundle backing the hypothesis, oldest first.</summary>
    public IReadOnlyList<ScanEvent> Evidence
    {
        get => _evidence;
        init => _evidence = value;
    }

    /// <summary>Playbook the Fix Wizard should offer, or null for diagnosis-only findings.</summary>
    public string? FixPlaybookId { get; init; }

    /// <summary>What to do when no automated playbook exists.</summary>
    public string? RecommendedAction { get; init; }

    /// <summary>Structured details used by the UI (controller name, scene, port...).</summary>
    public IReadOnlyDictionary<string, string> Data { get; init; } = ScanEvent.EmptyPayload;

    /// <summary>True when a fix can be offered for this finding.</summary>
    public bool IsActionable => !string.IsNullOrEmpty(FixPlaybookId);

    /// <summary>Timestamp of the earliest supporting evidence, if any.</summary>
    public DateTimeOffset? FirstSeenAt =>
        Evidence.Count == 0 ? null : Evidence.Min(static e => e.Timestamp);

    /// <summary>Timestamp of the latest supporting evidence, if any.</summary>
    public DateTimeOffset? LastSeenAt =>
        Evidence.Count == 0 ? null : Evidence.Max(static e => e.Timestamp);

    /// <summary>How many separate occurrences the evidence represents.</summary>
    public int Occurrences => Evidence.Count;
}
