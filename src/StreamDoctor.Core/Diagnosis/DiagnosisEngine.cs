using StreamDoctor.Core.Model;

namespace StreamDoctor.Core.Diagnosis;

/// <summary>
/// Runs every rule against one context, merges duplicate hypotheses, ranks the
/// result and produces the health board. A faulty rule never aborts a scan: its
/// failure is surfaced in <see cref="DiagnosisReport.RuleFailures"/>.
/// </summary>
public sealed class DiagnosisEngine
{
    private readonly IReadOnlyList<IFindingRule> _rules;
    private readonly DiagnosisOptions _options;

    public DiagnosisEngine(IEnumerable<IFindingRule> rules, DiagnosisOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(rules);

        _rules = rules.ToList();
        _options = options ?? new DiagnosisOptions();
    }

    public IReadOnlyList<IFindingRule> Rules => _rules;

    public DiagnosisOptions Options => _options;

    public DiagnosisReport Analyze(
        DiagnosisContext context,
        DateTimeOffset? generatedAt = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var findings = new List<Finding>();
        var failures = new List<RuleFailure>();

        foreach (var rule in _rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                foreach (var finding in rule.Evaluate(context))
                {
                    if (finding is not null)
                    {
                        findings.Add(finding);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // A broken rule must not fail the whole sweep; the failure is reported.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                failures.Add(new RuleFailure(rule.Id, $"{ex.GetType().Name}: {ex.Message}"));
            }
        }

        var normalized = findings
            .Select(finding => Normalize(finding, _options.MaxEvidencePerFinding))
            .ToList();

        var merged = Merge(normalized)
            .OrderByDescending(static finding => finding.Confidence)
            .ThenByDescending(static finding => finding.Severity)
            .ThenBy(static finding => finding.Module)
            .ThenBy(static finding => finding.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var scannedModules = DetermineScannedModules(context);
        var score = HealthScore.Compute(merged);

        return new DiagnosisReport
        {
            Findings = merged,
            Modules = ModuleHealthCalculator.Compute(scannedModules, merged),
            HealthScore = score,
            State = HealthScore.StateFor(score, scannedModules.Count > 0),
            GeneratedAt = generatedAt ?? DateTimeOffset.UtcNow,
            RuleFailures = failures,
        };
    }

    private static Finding Normalize(Finding finding, int maxEvidence)
    {
        ArgumentNullException.ThrowIfNull(finding);

        var evidence = (finding.Evidence ?? [])
            .DistinctBy(static item => (item.Timestamp, item.Category, item.DeviceKey, item.Sequence))
            .OrderBy(static item => item.Timestamp)
            .ThenBy(static item => item.Sequence)
            .ToList();

        if (maxEvidence > 0 && evidence.Count > maxEvidence)
        {
            evidence = evidence.TakeLast(maxEvidence).ToList();
        }

        return finding with { Evidence = evidence };
    }

    /// <summary>
    /// Two rules (or one rule twice) can describe the same problem for the same
    /// device. Merge them so the dashboard shows one row with the strongest score.
    /// </summary>
    private static IEnumerable<Finding> Merge(IEnumerable<Finding> findings) =>
        findings
            .GroupBy(static finding => (
                finding.HypothesisId,
                Target: finding.Data.TryGetValue("target", out var target) ? target : string.Empty))
            .Select(static group =>
            {
                var strongest = group.OrderByDescending(static finding => finding.Confidence).First();
                var evidence = group
                    .SelectMany(static finding => finding.Evidence)
                    .DistinctBy(static item => (item.Timestamp, item.Category, item.DeviceKey, item.Sequence))
                    .OrderBy(static item => item.Timestamp)
                    .ThenBy(static item => item.Sequence)
                    .ToList();

                return strongest with { Evidence = evidence };
            });

    private static IReadOnlyCollection<ScanModule> DetermineScannedModules(DiagnosisContext context)
    {
        if (context.ScannedModules is { Count: > 0 } declared)
        {
            return declared;
        }

        var scanned = context.Events.Select(static evidence => evidence.Module).ToHashSet();
        if (context.Topology is not null || context.PreviousTopology is not null)
        {
            scanned.Add(ScanModule.UsbSentinel);
        }

        if (context.Obs is not null)
        {
            scanned.Add(ScanModule.FramePacing);
            scanned.Add(ScanModule.ObsConfig);
        }

        return scanned;
    }
}
