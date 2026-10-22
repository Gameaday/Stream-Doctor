using StreamDoctor.Core.Diagnosis;
using StreamDoctor.Core.PostFlight;
using StreamDoctor.Core.Scanning;

namespace StreamDoctor.Windows;

/// <summary>
/// The single place where the Windows collectors are composed into a sweep.
/// The UI only talks to this class, never to individual collectors.
/// </summary>
public static class WindowsScanSuite
{
    public static IReadOnlyList<IScanSource> CreateSources(string? obsLogDirectory = null) =>
    [
        new Collectors.UsbTopologyScanSource(),
        new Collectors.UsbPowerScanSource(),
        new Collectors.WindowsEventLogScanSource(),
        new Collectors.SystemInventoryScanSource(),
        new Collectors.ObsLogScanSource(obsLogDirectory),
    ];

    public static PreFlightRunner CreatePreFlightRunner(
        DiagnosisOptions? options = null,
        string? obsLogDirectory = null) =>
        new(CreateSources(obsLogDirectory), new DiagnosisEngine(DefaultRuleSet.Create(), options));

    public static PostFlightAnalyzer CreatePostFlightAnalyzer(DiagnosisOptions? options = null) =>
        new(new DiagnosisEngine(DefaultRuleSet.Create(), options));
}
