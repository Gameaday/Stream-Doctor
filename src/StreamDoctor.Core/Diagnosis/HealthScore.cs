using StreamDoctor.Core.Model;

namespace StreamDoctor.Core.Diagnosis;

/// <summary>
/// Turns findings into the 0–100 health score shown on the dashboard.
/// The formula is deliberately simple and explainable:
/// every finding costs a fixed base penalty scaled by confidence, and the sum is
/// subtracted from 100 with a floor at zero.
/// </summary>
public static class HealthScore
{
    public const int Maximum = 100;

    /// <summary>Score at or above which the board is green.</summary>
    public const int GreenThreshold = 85;

    /// <summary>Score at or above which the board is yellow; below it, red.</summary>
    public const int YellowThreshold = 60;

    private const double CriticalPenalty = 45;
    private const double WarningPenalty = 18;
    private const double AdvisoryPenalty = 6;
    private const double MinimumConfidenceFactor = 0.4;

    public static int Compute(IEnumerable<Finding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);

        var penalty = 0d;
        foreach (var finding in findings)
        {
            var basePenalty = finding.Severity switch
            {
                Severity.Critical => CriticalPenalty,
                Severity.Warning => WarningPenalty,
                Severity.Advisory => AdvisoryPenalty,
                _ => 0d,
            };

            // Even a low-confidence critical issue should dent the score, hence the floor.
            var confidenceFactor = MinimumConfidenceFactor + ((1 - MinimumConfidenceFactor) * finding.Confidence);
            penalty += basePenalty * confidenceFactor;
        }

        return (int)Math.Round(Math.Clamp(Maximum - penalty, 0, Maximum), MidpointRounding.AwayFromZero);
    }

    /// <summary>Maps a score to a traffic-light state; nothing scanned means unknown, not green.</summary>
    public static HealthState StateFor(int score, bool anyModuleScanned)
    {
        if (!anyModuleScanned)
        {
            return HealthState.Unknown;
        }

        if (score >= GreenThreshold)
        {
            return HealthState.Green;
        }

        return score >= YellowThreshold ? HealthState.Yellow : HealthState.Red;
    }
}

/// <summary>Builds one gauge per module, distinguishing "clean" from "not scanned".</summary>
public static class ModuleHealthCalculator
{
    public static IReadOnlyList<ModuleHealth> Compute(
        IReadOnlyCollection<ScanModule> scannedModules,
        IReadOnlyList<Finding> findings)
    {
        ArgumentNullException.ThrowIfNull(scannedModules);
        ArgumentNullException.ThrowIfNull(findings);

        var scanned = scannedModules.ToHashSet();
        var byModule = findings
            .GroupBy(static finding => finding.Module)
            .ToDictionary(static group => group.Key, static group => group.ToList());

        var result = new List<ModuleHealth>();
        foreach (var module in Enum.GetValues<ScanModule>())
        {
            var moduleFindings = byModule.GetValueOrDefault(module, []);
            var wasScanned = scanned.Contains(module);
            var highest = moduleFindings.Count == 0 ? Severity.Info : moduleFindings.Max(static finding => finding.Severity);

            var state = (wasScanned, highest) switch
            {
                (false, _) => HealthState.Unknown,
                (true, >= Severity.Critical) => HealthState.Red,
                (true, >= Severity.Advisory) => HealthState.Yellow,
                _ => HealthState.Green,
            };

            result.Add(new ModuleHealth(module, state, moduleFindings.Count, highest, wasScanned));
        }

        return result;
    }
}
