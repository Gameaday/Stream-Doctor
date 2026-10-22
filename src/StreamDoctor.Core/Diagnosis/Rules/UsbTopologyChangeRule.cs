using System.Collections.ObjectModel;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Usb;

namespace StreamDoctor.Core.Diagnosis.Rules;

/// <summary>
/// Compares the current USB tree with the previous one. A device that silently
/// moved controllers is a finding in itself (design doc §9, shared infrastructure),
/// because every later fix recommendation depends on the map being right.
/// </summary>
public sealed class UsbTopologyChangeRule : IFindingRule
{
    private const double BaseWeight = 0.5;

    public string Id => "usb.topology.changed";

    public ScanModule OwnerModule => ScanModule.UsbSentinel;

    public IEnumerable<Finding> Evaluate(DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Topology is null || context.PreviousTopology is null)
        {
            yield break;
        }

        var changes = UsbTopologyDiff.Compare(context.PreviousTopology, context.Topology)
            .Where(static change => change.Kind == UsbTopologyChangeKind.MovedToAnotherController);

        foreach (var change in changes)
        {
            var tally = new EvidenceTally();
            tally.Support($"after:{change.Device.InstanceId}", context.EvidenceFor(change.Device, context.Topology.CapturedAt));
            tally.Support($"before:{change.Device.InstanceId}", new ScanEvent
            {
                Timestamp = context.PreviousTopology.CapturedAt,
                Source = ScanSource.UsbTopology,
                Module = ScanModule.UsbSentinel,
                Category = ScanEventCodes.UsbTopologyChanged,
                Severity = Severity.Info,
                Message = $"{change.Device.Name} used to hang off {ControllerName(context.PreviousTopology, change.FromController)}.",
                Payload = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ScanEventPayload.Device] = change.Device.Name,
                    [ScanEventPayload.DeviceInstanceId] = change.Device.InstanceId,
                    [ScanEventPayload.Controller] = change.FromController ?? string.Empty,
                }),
            });

            yield return new Finding
            {
                HypothesisId = Id,
                Module = ScanModule.UsbSentinel,
                Title = $"{change.Device.Name} moved to a different USB controller",
                Explanation = $"{change.Device.Name} was on {ControllerName(context.PreviousTopology, change.FromController)} and is now on {ControllerName(context.Topology, change.ToController)}. " +
                              "If you did not move it, a hub or port is re-enumerating — that is worth watching.",
                Severity = Severity.Advisory,
                Confidence = tally.Score(BaseWeight),
                Evidence = tally.Evidence,
                RecommendedAction = "If you moved the cable yourself, re-run the sweep so advice matches reality. Otherwise test that port with the re-enumeration test.",
                Data = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target"] = change.Device.InstanceId,
                    [ScanEventPayload.Device] = change.Device.Name,
                    ["from"] = change.FromController ?? string.Empty,
                    ["to"] = change.ToController ?? string.Empty,
                }),
            };
        }
    }

    private static string ControllerName(UsbTopology topology, string? instanceId) =>
        topology.FindController(instanceId ?? string.Empty)?.Name ?? instanceId ?? "an unknown controller";
}
