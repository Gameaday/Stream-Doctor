using System.Collections.ObjectModel;
using System.Globalization;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Usb;

namespace StreamDoctor.Core.Diagnosis.Rules;

/// <summary>
/// Flags controllers whose attached devices exceed the estimated bandwidth budget.
/// Design doc §2.1: isochronous devices (cameras, capture cards, audio interfaces)
/// drop out when a controller saturates even though a port looks free.
/// </summary>
public sealed class UsbBandwidthRule : IFindingRule
{
    private const double BaseWeight = 0.4;

    public string Id => "usb.bandwidth.saturation";

    public ScanModule OwnerModule => ScanModule.UsbSentinel;

    public IEnumerable<Finding> Evaluate(DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Topology is null)
        {
            yield break;
        }

        var loads = UsbBandwidthAnalyzer.Analyze(context.Topology);

        foreach (var load in loads.Where(static load => load.IsAtRisk))
        {
            var controller = load.Controller;
            var capturedAt = context.Topology.CapturedAt;
            var tally = new EvidenceTally();

            var isochronous = load.Devices.Where(static device => device.Class.IsIsochronous()).ToList();
            foreach (var device in isochronous)
            {
                tally.Support($"device:{device.InstanceId}", context.EvidenceFor(device, capturedAt));
            }

            if (controller.Kind == UsbControllerKind.Usb2 && load.IsochronousDeviceCount >= 2)
            {
                tally.Support($"usb2:{controller.InstanceId}", ControllerEvidence(controller, load, capturedAt));
            }

            if (load.IsSaturated)
            {
                tally.Support($"saturated:{controller.InstanceId}", ControllerEvidence(controller, load, capturedAt));
            }

            var headroom = UsbBandwidthAnalyzer.FindHeadroom(loads, load);
            var names = string.Join(", ", isochronous.Select(static device => device.Name));
            var severity = load.IsSaturated
                ? Severity.Critical
                : controller.Kind == UsbControllerKind.Usb2 && load.IsochronousDeviceCount >= 2
                    ? Severity.Warning
                    : Severity.Advisory;

            var explanation = load.IsSaturated
                ? $"{controller.Name} is estimated at {Percent(load.Utilization)} of its bandwidth with {names} attached. One of them will start dropping out under load."
                : $"{names} share {controller.Name}, which only has about {Percent(1 - load.Utilization)} headroom left. A saturated controller shows up as mic crackle or a webcam that freezes.";

            var recommendation = headroom.Count > 0
                ? $"Move one of them to {headroom[0].Controller.Name}, which is at {Percent(headroom[0].Utilization)} load."
                : "Add a powered USB card or move one device to a different controller; every controller on this machine is close to its budget.";

            yield return new Finding
            {
                HypothesisId = Id,
                Module = ScanModule.UsbSentinel,
                Title = $"{names} are fighting over {controller.Name}",
                Explanation = explanation,
                Severity = severity,
                Confidence = tally.Score(BaseWeight),
                Evidence = tally.Evidence,
                RecommendedAction = recommendation,
                Data = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target"] = controller.InstanceId,
                    [ScanEventPayload.Controller] = controller.Name,
                    [ScanEventPayload.Devices] = names,
                    [ScanEventPayload.Utilization] = Percent(load.Utilization),
                }),
            };
        }
    }

    internal static ScanEvent ControllerEvidence(UsbController controller, UsbControllerLoad load, DateTimeOffset timestamp) =>
        new()
        {
            Timestamp = timestamp,
            Source = ScanSource.UsbTopology,
            Module = ScanModule.UsbSentinel,
            Category = ScanEventCodes.UsbControllerObserved,
            Severity = Severity.Info,
            Message = $"{controller.Name} carries {load.Devices.Count} device(s) at an estimated {Percent(load.Utilization)} load.",
            Payload = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ScanEventPayload.Controller] = controller.Name,
                [ScanEventPayload.ControllerKind] = controller.Kind.ToString(),
                [ScanEventPayload.Count] = load.Devices.Count.ToString(CultureInfo.InvariantCulture),
                [ScanEventPayload.Utilization] = Percent(load.Utilization),
            }),
        };

    internal static string Percent(double ratio) =>
        (ratio * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
}
