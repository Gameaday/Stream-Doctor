using StreamDoctor.Core.Diagnosis;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Obs;
using StreamDoctor.Core.Updates;
using StreamDoctor.Core.Usb;

namespace StreamDoctor.Core.Scanning;

/// <summary>
/// Runs every collector, assembles one <see cref="DiagnosisContext"/> and hands it
/// to the rules engine. Collectors run sequentially on purpose: concurrent device
/// enumeration makes USB evidence non-deterministic and can trip flaky ports.
/// </summary>
public sealed class PreFlightRunner
{
    private readonly IReadOnlyList<IScanSource> _sources;
    private readonly DiagnosisEngine _engine;
    private readonly UpdatePolicy _updatePolicy;

    public PreFlightRunner(
        IEnumerable<IScanSource> sources,
        DiagnosisEngine engine,
        UpdatePolicy? updatePolicy = null)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(engine);

        _sources = sources.ToList();
        _engine = engine;
        _updatePolicy = updatePolicy ?? UpdatePolicy.Safe;
    }

    public IReadOnlyList<IScanSource> Sources => _sources;

    public UpdatePolicy UpdatePolicy => _updatePolicy;

    public async Task<ScanReport> RunAsync(ScanRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startedAt = request.Now;
        var events = new List<ScanEvent>();
        var failures = new List<ScanSourceFailure>();
        var scanned = new HashSet<ScanModule>();
        UsbTopology? topology = null;
        UsbTopology? previousTopology = null;
        var powerSnapshots = new List<UsbPowerSnapshot>();
        PowerPlanInfo? powerPlan = null;
        ObsLogDocument? obs = null;
        var updates = new List<UpdateCandidate>();

        foreach (var source in _sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(request.SourceTimeout);

            try
            {
                // Race the collector against its deadline. A collector that ignores the
                // cancellation token (blocking COM/WMI call) would otherwise hang the sweep.
                var collectTask = source.CollectAsync(request, timeout.Token);
                var deadlineTask = Task.Delay(request.SourceTimeout, cancellationToken);
                var winner = await Task.WhenAny(collectTask, deadlineTask).ConfigureAwait(false);

                if (!ReferenceEquals(winner, collectTask))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Observe the abandoned task so an eventual fault is not unobserved.
                    _ = collectTask.ContinueWith(
                        static task => _ = task.Exception,
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);

                    failures.Add(new ScanSourceFailure(
                        source.Id,
                        $"Timed out after {request.SourceTimeout.TotalSeconds:0} seconds (collector ignored cancellation).",
                        TimedOut: true,
                        source.IsOptional));
                    continue;
                }

                var result = await collectTask.ConfigureAwait(false);

                events.AddRange(result.Events);
                scanned.Add(source.OwnerModule);
                foreach (var evidence in result.Events)
                {
                    scanned.Add(evidence.Module);
                }

                topology ??= result.Topology;
                previousTopology ??= result.PreviousTopology;
                powerSnapshots.AddRange(result.PowerSnapshots);
                powerPlan ??= result.PowerPlan;
                obs ??= result.Obs;
                updates.AddRange(result.Updates);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                failures.Add(new ScanSourceFailure(
                    source.Id,
                    $"Timed out after {request.SourceTimeout.TotalSeconds:0} seconds.",
                    TimedOut: true,
                    source.IsOptional));
            }
#pragma warning disable CA1031 // One broken collector must not abort the whole sweep.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                failures.Add(new ScanSourceFailure(source.Id, $"{ex.GetType().Name}: {ex.Message}", TimedOut: false, source.IsOptional));
            }
        }

        var context = new DiagnosisContext
        {
            Events = events
                .OrderBy(static evidence => evidence.Timestamp)
                .ThenBy(static evidence => evidence.Sequence)
                .ToList(),
            Topology = topology,
            PreviousTopology = previousTopology,
            PowerSnapshots = powerSnapshots,
            PowerPlan = powerPlan,
            Obs = obs,
            Updates = updates,
            UpdatePlan = UpdatePlanner.Plan(updates, _updatePolicy),
            ScannedModules = scanned,
            WindowStart = request.WindowStart,
            WindowEnd = request.WindowEnd,
        };

        var diagnosis = _engine.Analyze(context, request.Now, cancellationToken);

        return new ScanReport
        {
            Diagnosis = diagnosis,
            Events = context.Events,
            ScannedModules = scanned,
            Failures = failures,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.UtcNow,
            Obs = obs,
            Topology = topology,
            PowerSnapshots = powerSnapshots,
            PowerPlan = powerPlan,
            Updates = context.Updates,
            UpdatePlan = context.UpdatePlan,
        };
    }
}
