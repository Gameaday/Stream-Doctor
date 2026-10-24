using System.Diagnostics;
using StreamDoctor.Core.Diagnosis;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Scanning;
using StreamDoctor.Windows;

namespace StreamDoctor.StressTest;

public static partial class Program
{
    private static async Task<int> RunPreFlightStressAsync(StressConfig config)
    {
        Console.WriteLine($"[PRE-FLIGHT] {config.Iterations} sweep(s), delay {config.DelayBetweenIterations.TotalSeconds:0}s");
        Console.WriteLine();

        var runner = WindowsScanSuite.CreatePreFlightRunner(obsLogDirectory: config.ObsLogDirectory);
        var results = new List<PreFlightStressResult>();

        for (var i = 1; i <= config.Iterations; i++)
        {
            Console.Write($"[{i}/{config.Iterations}] sweeping... ");
            var stopwatch = Stopwatch.StartNew();
            var report = await runner.RunAsync(ScanRequest.PreFlight(DateTimeOffset.Now), CancellationToken.None);
            stopwatch.Stop();

            var result = new PreFlightStressResult
            {
                Iteration = i,
                HealthScore = report.Diagnosis.HealthScore,
                HealthState = report.Diagnosis.State.ToString(),
                FindingCount = report.Diagnosis.Findings.Count,
                CriticalFindings = report.Diagnosis.Findings.Count(static finding => finding.Severity == Severity.Critical),
                WarningFindings = report.Diagnosis.Findings.Count(static finding => finding.Severity == Severity.Warning),
                EvidenceCount = report.Events.Count,
                UpdateCount = report.Updates.Count,
                Duration = stopwatch.Elapsed,
                Success = report.IsComplete,
                Failures = report.Failures.Select(static failure => $"{failure.SourceId}: {failure.Message}").ToList(),
            };

            results.Add(result);

            Console.WriteLine(
                $"{result.StateLabel} {result.HealthScore}/100, {result.FindingCount} finding(s), " +
                $"{result.EvidenceCount} evidence line(s), {result.Duration.TotalSeconds:0.0}s" +
                (result.Success ? string.Empty : " [incomplete]"));
        }

        WriteSummary(results, config.OutputFile);
        PrintPreFlightSummary(results);
        return results.Any(static result => !result.Success) ? 1 : 0;
    }

    private static async Task<int> RunPostFlightStressAsync(StressConfig config)
    {
        Console.WriteLine($"[POST-FLIGHT] {config.Iterations} debrief(s)");
        Console.WriteLine();

        var runner = WindowsScanSuite.CreatePreFlightRunner(obsLogDirectory: config.ObsLogDirectory);
        var analyzer = WindowsScanSuite.CreatePostFlightAnalyzer();
        var results = new List<PostFlightStressResult>();

        for (var i = 1; i <= config.Iterations; i++)
        {
            Console.Write($"[{i}/{config.Iterations}] debriefing... ");
            var stopwatch = Stopwatch.StartNew();

            var request = ScanRequest.PostFlight(DateTimeOffset.Now.AddHours(-2), DateTimeOffset.Now);
            var report = await runner.RunAsync(request, CancellationToken.None);

            var context = new DiagnosisContext
            {
                Events = report.Events,
                Topology = report.Topology,
                PowerSnapshots = report.PowerSnapshots,
                PowerPlan = report.PowerPlan,
                Obs = report.Obs,
                Updates = report.Updates,
                UpdatePlan = report.UpdatePlan,
                ScannedModules = report.ScannedModules,
                WindowStart = request.WindowStart,
                WindowEnd = request.WindowEnd,
            };

            var debrief = analyzer.Analyze(context, report.Obs, null, DateTimeOffset.Now);
            stopwatch.Stop();

            var result = new PostFlightStressResult
            {
                Iteration = i,
                HealthScore = debrief.Diagnosis.HealthScore,
                HealthState = debrief.Diagnosis.State.ToString(),
                FindingCount = debrief.Diagnosis.Findings.Count,
                HasSession = debrief.Session is not null,
                SessionDuration = debrief.Session?.Duration ?? TimeSpan.Zero,
                FollowUpChecks = debrief.FollowUpChecks.Count,
                Headline = debrief.Headline,
                Duration = stopwatch.Elapsed,
            };

            results.Add(result);
            Console.WriteLine(
                $"{result.StateLabel} {result.HealthScore}/100, session " +
                $"{(result.HasSession ? result.SessionDuration.TotalMinutes.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "min" : "none")}, " +
                $"{result.Duration.TotalSeconds:0.0}s");
        }

        WriteSummary(results, config.OutputFile);
        return 0;
    }
}
