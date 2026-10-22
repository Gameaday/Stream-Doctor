using StreamDoctor.Core.Model;
using StreamDoctor.Core.Obs;
using StreamDoctor.Core.Usb;

namespace StreamDoctor.Core.Diagnosis;

/// <summary>
/// Everything the rules may look at. Each collector contributes its own slice;
/// rules must not perform I/O, so all of this is already in memory.
/// </summary>
public sealed record DiagnosisContext
{
    /// <summary>All normalized evidence, from every source.</summary>
    public required IReadOnlyList<ScanEvent> Events { get; init; }

    /// <summary>Current USB tree, when the USB collector ran.</summary>
    public UsbTopology? Topology { get; init; }

    /// <summary>Previous USB tree, used to detect devices that moved controllers.</summary>
    public UsbTopology? PreviousTopology { get; init; }

    /// <summary>Power-management flags per device.</summary>
    public IReadOnlyList<UsbPowerSnapshot> PowerSnapshots { get; init; } = [];

    /// <summary>Active power plan, when the system inventory collector ran.</summary>
    public PowerPlanInfo? PowerPlan { get; init; }

    /// <summary>Parsed OBS logs (sessions and events).</summary>
    public ObsLogDocument? Obs { get; init; }

    /// <summary>Modules that actually produced data, used to render "not scanned" honestly.</summary>
    public IReadOnlyCollection<ScanModule>? ScannedModules { get; init; }

    /// <summary>Optional bound of the analysis window (post-flight session, or the sweep).</summary>
    public DateTimeOffset? WindowStart { get; init; }

    public DateTimeOffset? WindowEnd { get; init; }

    public static DiagnosisContext Empty(DateTimeOffset now) => new()
    {
        Events = [],
        WindowStart = now,
        WindowEnd = now,
    };

    /// <summary>Events matching a category constant.</summary>
    public IReadOnlyList<ScanEvent> ByCategory(string category) =>
        Events.Where(evidence => string.Equals(evidence.Category, category, StringComparison.Ordinal))
            .OrderBy(static evidence => evidence.Timestamp)
            .ThenBy(static evidence => evidence.Sequence)
            .ToList();

    /// <summary>Events of a category, restricted to a time window when one is set.</summary>
    public IReadOnlyList<ScanEvent> ByCategoryInWindow(string category)
    {
        var events = ByCategory(category);
        if (WindowStart is null && WindowEnd is null)
        {
            return events;
        }

        return events.Where(IsInWindow).ToList();
    }

    /// <summary>True when the event lies inside the configured analysis window.</summary>
    public bool IsInWindow(ScanEvent evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        if (WindowStart is not null && evidence.Timestamp < WindowStart)
        {
            return false;
        }

        return WindowEnd is null || evidence.Timestamp <= WindowEnd;
    }

    /// <summary>
    /// Finds the topology evidence event for a device, falling back to a synthesized
    /// event so rules still produce traceable findings for hand-built contexts.
    /// </summary>
    public ScanEvent EvidenceFor(UsbDevice device, DateTimeOffset fallbackTimestamp)
    {
        ArgumentNullException.ThrowIfNull(device);

        var existing = Events.FirstOrDefault(evidence =>
            string.Equals(evidence.Category, ScanEventCodes.UsbDeviceObserved, StringComparison.Ordinal) &&
            string.Equals(evidence.GetPayload(ScanEventPayload.DeviceInstanceId), device.InstanceId, StringComparison.OrdinalIgnoreCase));

        return existing ?? UsbTopologyEvents.ForDevice(device, fallbackTimestamp);
    }
}

/// <summary>
/// A diagnosis rule: evidence in, ranked hypotheses out. Rules are pure,
/// deterministic and side-effect free — they never read from the machine.
/// </summary>
public interface IFindingRule
{
    /// <summary>Stable identifier used in <see cref="Finding.HypothesisId"/>.</summary>
    string Id { get; }

    /// <summary>Module this rule belongs to.</summary>
    ScanModule OwnerModule { get; }

    /// <summary>Evaluates the rule; may return zero or more findings.</summary>
    IEnumerable<Finding> Evaluate(DiagnosisContext context);
}

/// <summary>Tuning knobs for the diagnosis pass.</summary>
public sealed record DiagnosisOptions
{
    /// <summary>
    /// Confidence at or above which a playbook may run without the user clicking
    /// through the wizard (only when auto-fix mode is enabled). Design doc §3.
    /// </summary>
    public double AutoFixConfidenceThreshold { get; init; } = 0.85;

    /// <summary>Cap on evidence kept per finding, newest wins.</summary>
    public int MaxEvidencePerFinding { get; init; } = 25;
}
