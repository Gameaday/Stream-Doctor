using System.Collections.ObjectModel;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Usb;

namespace StreamDoctor.Core.Diagnosis.Rules;

/// <summary>
/// Turns the static power audit into findings with a one-click playbook.
/// Design doc §9, Test 5: no perturbation, just risky registry settings.
/// </summary>
public sealed class UsbPowerRule : IFindingRule
{
    private const double BaseWeight = 0.45;

    public string Id => "usb.power.settings";

    public ScanModule OwnerModule => ScanModule.UsbSentinel;

    public IEnumerable<Finding> Evaluate(DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.PowerSnapshots.Count == 0 && context.PowerPlan is null)
        {
            yield break;
        }

        var topology = context.Topology ?? UsbTopology.Empty(DateTimeOffset.UtcNow);
        var risks = UsbPowerAudit.Evaluate(topology, context.PowerSnapshots, context.PowerPlan);
        var now = topology.CapturedAt;

        foreach (var risk in risks)
        {
            var tally = new EvidenceTally();
            if (risk.Device is not null)
            {
                tally.Support($"device:{risk.Device.InstanceId}", context.EvidenceFor(risk.Device, now));
            }

            var flag = new ScanEvent
            {
                Timestamp = now,
                Source = ScanSource.UsbTopology,
                Module = ScanModule.UsbSentinel,
                Category = ScanEventCodes.UsbPowerFlag,
                Severity = risk.Severity,
                Message = risk.Explanation,
                Payload = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["code"] = risk.Code,
                    [ScanEventPayload.Device] = risk.Device?.Name ?? "Windows power plan",
                    [ScanEventPayload.FixPlaybook] = risk.FixPlaybookId,
                }),
            };

            tally.Support($"flag:{risk.Code}:{risk.Device?.InstanceId ?? "system"}", flag);

            yield return new Finding
            {
                HypothesisId = risk.Code,
                Module = ScanModule.UsbSentinel,
                Title = risk.Device is null ? "Windows power plan is working against your USB gear" : risk.Explanation.Split('.')[0],
                Explanation = risk.Explanation,
                Severity = risk.Severity,
                Confidence = tally.Score(BaseWeight),
                Evidence = tally.Evidence,
                FixPlaybookId = risk.FixPlaybookId,
                Data = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target"] = risk.Device?.InstanceId ?? "system-power-plan",
                    [ScanEventPayload.Device] = risk.Device?.Name ?? string.Empty,
                    ["code"] = risk.Code,
                }),
            };
        }
    }
}
