using StreamDoctor.Core.Diagnosis;
using StreamDoctor.Core.PostFlight;
using StreamDoctor.Core.Scanning;
using StreamDoctor.Windows;
using StreamDoctor.Windows.Collectors;

namespace StreamDoctor.App.Services;

/// <summary>
/// UI-facing wrapper around the Windows scan suite. Every sweep the app runs goes
/// through here so the window never talks to individual collectors.
/// </summary>
public sealed class ScanService
{
    private readonly PreFlightRunner _runner;
    private readonly PostFlightAnalyzer _postFlight;

    public ScanService(string? obsLogDirectory = null, DiagnosisOptions? options = null)
    {
        _runner = WindowsScanSuite.CreatePreFlightRunner(options, obsLogDirectory);
        _postFlight = WindowsScanSuite.CreatePostFlightAnalyzer(options);
    }

    public Task<ScanReport> RunPreFlightAsync(CancellationToken cancellationToken) =>
        _runner.RunAsync(ScanRequest.PreFlight(DateTimeOffset.Now), cancellationToken);

    /// <summary>
    /// Finds the most recent OBS stream session, re-runs the sweep scoped to that
    /// window, and produces the debrief. Returns null when OBS has no session yet.
    /// </summary>
    public async Task<PostFlightDebrief?> RunPostFlightAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.Now;
        var document = ObsLogLoader.LoadLatest(ScanRequest.PreFlight(now), cancellationToken: cancellationToken);
        var session = document?.LastStreamSession;

        if (document is null || session is null)
        {
            return null;
        }

        var windowEnd = session.EndedAt ?? now;
        var report = await _runner
            .RunAsync(ScanRequest.PostFlight(session.StartedAt, windowEnd), cancellationToken)
            .ConfigureAwait(false);

        var scopedDocument = report.Obs ?? document;
        var scopedSession = scopedDocument.Sessions.FirstOrDefault(candidate =>
            candidate.StartedAt == session.StartedAt) ?? session;

        var context = new DiagnosisContext
        {
            Events = report.Events,
            Topology = report.Topology,
            PowerSnapshots = report.PowerSnapshots,
            PowerPlan = report.PowerPlan,
            Obs = scopedDocument,
            ScannedModules = report.ScannedModules,
            WindowStart = scopedSession.StartedAt,
            WindowEnd = scopedSession.EndedAt ?? windowEnd,
        };

        return _postFlight.Analyze(context, scopedDocument, scopedSession, now);
    }
}
