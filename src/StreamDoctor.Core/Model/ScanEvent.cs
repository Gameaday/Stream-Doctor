using System.Collections.ObjectModel;

namespace StreamDoctor.Core.Model;

/// <summary>
/// Normalized evidence: one thing that happened, from one source, at one moment.
/// Every finding in the app must be traceable back to a set of these.
/// </summary>
public sealed record ScanEvent
{
    /// <summary>An empty, immutable payload, reused to avoid allocations.</summary>
    public static IReadOnlyDictionary<string, string> EmptyPayload { get; } =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(0));

    /// <summary>Wall-clock instant of the observation, normalized to UTC.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Which collector produced this evidence.</summary>
    public required ScanSource Source { get; init; }

    /// <summary>Which product module owns this evidence.</summary>
    public required ScanModule Module { get; init; }

    /// <summary>
    /// Stable, machine-readable category (see <see cref="ScanEventCodes"/>), e.g.
    /// <c>usb.device-reset</c>. Rules match on this, never on localized messages.
    /// </summary>
    public required string Category { get; init; }

    /// <summary>How much this observation matters on its own.</summary>
    public required Severity Severity { get; init; }

    /// <summary>Human-readable detail, ideally already explaining itself to a streamer.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Structured detail (device, scene, port, host...).</summary>
    public IReadOnlyDictionary<string, string> Payload { get; init; } = EmptyPayload;

    /// <summary>
    /// Monotonic ordering stamp assigned by the collector. Lets post-flight merge
    /// events that share the same (second-resolution) timestamp deterministically.
    /// </summary>
    public long Sequence { get; init; }

    /// <summary>True when this observation is a problem rather than an observation.</summary>
    public bool IsFault => Severity >= Severity.Warning;

    /// <summary>Convenience accessor for structured payload values.</summary>
    public string? GetPayload(string key) => Payload.TryGetValue(key, out var value) ? value : null;

    /// <summary>The device this event is about, used to group evidence per device.</summary>
    public string? DeviceKey =>
        GetPayload(ScanEventPayload.Device) ??
        GetPayload(ScanEventPayload.DeviceInstanceId) ??
        GetPayload(ScanEventPayload.SourceName);

    /// <summary>Creates an event with a payload assembled from tuples.</summary>
    public static ScanEvent Create(
        DateTimeOffset timestamp,
        ScanSource source,
        ScanModule module,
        string category,
        Severity severity,
        string message,
        IEnumerable<KeyValuePair<string, string>>? payload = null,
        long sequence = 0)
    {
        var map = payload is null
            ? EmptyPayload
            : new ReadOnlyDictionary<string, string>(
                payload.Where(pair => !string.IsNullOrEmpty(pair.Key))
                      .GroupBy(pair => pair.Key, StringComparer.Ordinal)
                      .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.Ordinal));

        return new ScanEvent
        {
            Timestamp = timestamp,
            Source = source,
            Module = module,
            Category = category,
            Severity = severity,
            Message = message,
            Payload = map,
            Sequence = sequence,
        };
    }
}
