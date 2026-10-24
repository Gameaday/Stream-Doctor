using System.Text.Json;

namespace StreamDoctor.StressTest;

public static partial class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static void PrintPreFlightSummary(List<PreFlightStressResult> results)
    {
        if (results.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Summary:");
        Console.WriteLine($"  sweeps          : {results.Count}");
        Console.WriteLine($"  complete        : {results.Count(static result => result.Success)}");
        Console.WriteLine($"  avg duration    : {results.Average(static result => result.Duration.TotalSeconds):0.0}s");
        Console.WriteLine($"  max duration    : {results.Max(static result => result.Duration.TotalSeconds):0.0}s");
        Console.WriteLine($"  health min/avg  : {results.Min(static result => result.HealthScore)} / {results.Average(static result => result.HealthScore):0.0}");
        Console.WriteLine($"  evidence lines  : {results.Average(static result => result.EvidenceCount):0.0} avg");
        Console.WriteLine($"  findings        : {results.Average(static result => result.FindingCount):0.0} avg");
    }

    private static void WriteJsonOutput(object data, string? outputFile)
    {
        if (string.IsNullOrWhiteSpace(outputFile))
        {
            return;
        }

        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(outputFile, json);
        Console.WriteLine($"Results written to {outputFile}");
    }

    private static void WriteSummary<T>(List<T> results, string? outputFile)
    {
        if (results.Count == 0)
        {
            return;
        }

        var summary = new
        {
            GeneratedAt = DateTimeOffset.Now,
            Iterations = results.Count,
            Results = results,
        };

        WriteJsonOutput(summary, outputFile);
    }
}
