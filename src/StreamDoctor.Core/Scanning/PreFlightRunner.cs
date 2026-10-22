using StreamDoctor.Core.Diagnosis;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Obs;
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

    public PreFlightRunner(IEnumerable<IScanSource> sources, DiagnosisEngine engine)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(engine);

        _sources = sources.ToList();
        _engine = engine;
    }

    public IReadOnlyList<IScanSource> Sources => _sources;

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

        foreach (var source in _sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(request.SourceTimeout);

            try
            {
                var result = await source.CollectAsync(request, timeout.Token).ConfigureAwait(false);

                events.AddRange(result.Events);
                scanned.Add(source.OwnerModule);
                topology ??= result.Topology;
                previousTopology ??= result.PreviousTopology;
                powerSnapshots.AddRange(result.PowerSnapshots);
                powerPlan ??= result.PowerPlan;
                obs ??= result.Obs;
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
        };
    }
}
