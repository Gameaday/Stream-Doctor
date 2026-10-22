using StreamDoctor.Core.Model;

namespace StreamDoctor.Core.Diagnosis;

/// <summary>
/// Transparent confidence scoring, per design doc §10 step 3:
/// <c>confidence = base_weight × supporting − contradicting</c>.
/// Two deliberate corrections are applied here (documented in docs/design-review.md):
/// repeated log lines collapse into one signal, and the result is clamped to 0..1.
/// </summary>
public static class ConfidenceModel
{
    /// <summary>Confidence contributed by a single distinct signal.</summary>
    public const double DefaultBaseWeight = 0.35;

    /// <summary>How much one contradicting signal is worth.</summary>
    public const double ContradictionWeight = 0.25;

    /// <summary>Supporting signals beyond this add nothing (prevents noisy-log inflation).</summary>
    public const int MaxSupportingSignals = 5;

    public static double Score(double baseWeight, int supportingSignals, int contradictingSignals)
    {
        if (!double.IsFinite(baseWeight) || baseWeight <= 0)
        {
            return 0;
        }

        var support = Math.Max(0, Math.Min(supportingSignals, MaxSupportingSignals));
        var contradiction = Math.Max(0, Math.Min(contradictingSignals, MaxSupportingSignals));
        var raw = (baseWeight * support) - (ContradictionWeight * contradiction);
        return double.IsFinite(raw) ? Math.Clamp(raw, 0d, 1d) : 0d;
    }
}

/// <summary>
/// Collects supporting and contradicting evidence, counting each distinct signal
/// key once while keeping every occurrence for the UI ("your mic dropped 14 times").
/// </summary>
public sealed class EvidenceTally
{
    private readonly Dictionary<string, List<ScanEvent>> _supporting = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<ScanEvent>> _contradicting = new(StringComparer.Ordinal);

    /// <summary>Adds an observation supporting the hypothesis.</summary>
    public EvidenceTally Support(string signalKey, ScanEvent evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signalKey);
        ArgumentNullException.ThrowIfNull(evidence);

        return Add(_supporting, signalKey, evidence);
    }

    /// <summary>Adds an observation that argues against the hypothesis.</summary>
    public EvidenceTally Contradict(string signalKey, ScanEvent evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signalKey);
        ArgumentNullException.ThrowIfNull(evidence);

        return Add(_contradicting, signalKey, evidence);
    }

    /// <summary>Adds evidence keyed by the device it concerns (or its category).</summary>
    public EvidenceTally Support(ScanEvent evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return Support(evidence.DeviceKey ?? evidence.Category, evidence);
    }

    public int DistinctSupportingSignals => _supporting.Count;

    public int SupportingEventCount => _supporting.Values.Sum(static events => events.Count);

    public int DistinctContradictingSignals => _contradicting.Count;

    /// <summary>All supporting evidence, oldest first, de-duplicated by timestamp/category/device.</summary>
    public IReadOnlyList<ScanEvent> Evidence =>
        _supporting.Values
            .SelectMany(static events => events)
            .DistinctBy(static evidence => (evidence.Timestamp, evidence.Category, evidence.DeviceKey))
            .OrderBy(static evidence => evidence.Timestamp)
            .ThenBy(static evidence => evidence.Sequence)
            .ToList();

    /// <summary>Computes confidence from the distinct signals collected so far.</summary>
    public double Score(double baseWeight = ConfidenceModel.DefaultBaseWeight) =>
        ConfidenceModel.Score(baseWeight, DistinctSupportingSignals, DistinctContradictingSignals);

    private EvidenceTally Add(Dictionary<string, List<ScanEvent>> target, string key, ScanEvent evidence)
    {
        if (!target.TryGetValue(key, out var events))
        {
            events = [];
            target[key] = events;
        }

        events.Add(evidence);
        return this;
    }
}
