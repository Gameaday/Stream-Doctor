using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using StreamDoctor.App.Services;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.Scanning;

namespace StreamDoctor.App.ViewModels;

/// <summary>
/// Dashboard state: health board, ranked findings and the two sweep commands.
/// Commands are disabled while a sweep runs; concurrent sweeps would produce
/// contradictory USB evidence.
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly ScanService _scanService;

    private CancellationTokenSource? _cancellation;

    public DashboardViewModel()
        : this(new ScanService())
    {
    }

    public DashboardViewModel(ScanService scanService)
    {
        ArgumentNullException.ThrowIfNull(scanService);
        _scanService = scanService;
    }

    public ObservableCollection<ModuleCardViewModel> Modules { get; } = [];

    public ObservableCollection<FindingViewModel> Findings { get; } = [];

    [ObservableProperty]
    private string _healthHeadline = "Run a pre-flight sweep before you go live.";

    [ObservableProperty]
    private string _healthScoreText = "--";

    [ObservableProperty]
    private string _statusText = "Idle. Nothing runs while you are live or in a game.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunPreFlightCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunPostFlightCommand))]
    private bool _isBusy;

    private bool CanRun() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task RunPreFlightAsync() => RunAsync(
        "Pre-flight sweep",
        async token =>
        {
            var report = await _scanService.RunPreFlightAsync(token).ConfigureAwait(true);
            Apply(report.Diagnosis, DescribeSweep(report));
        });

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task RunPostFlightAsync() => RunAsync(
        "Post-flight debrief",
        async token =>
        {
            var debrief = await _scanService.RunPostFlightAsync(token).ConfigureAwait(true);

            if (debrief is null)
            {
                StatusText = "No OBS stream session found. Point Stream Doctor at your OBS log folder and try again.";
                return;
            }

            Apply(debrief.Diagnosis, debrief.Headline);
        });

    private async Task RunAsync(string label, Func<CancellationToken, Task> operation)
    {
        IsBusy = true;
        StatusText = $"{label} running. Keep OBS closed.";
        _cancellation = new CancellationTokenSource();

        try
        {
            await operation(_cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            StatusText = $"{label} cancelled.";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "{Label} failed", label);
            StatusText = $"{label} failed: {ex.Message}";
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            IsBusy = false;
        }
    }

    private void Apply(DiagnosisReport diagnosis, string status)
    {
        HealthScoreText = diagnosis.HealthScore.ToString(CultureInfo.InvariantCulture);
        HealthHeadline = diagnosis.TopFinding?.Title ?? "No problems found. You are clear to go live.";

        Modules.Clear();
        foreach (var module in diagnosis.Modules)
        {
            Modules.Add(new ModuleCardViewModel(module));
        }

        Findings.Clear();
        foreach (var finding in diagnosis.Findings)
        {
            Findings.Add(new FindingViewModel(finding));
        }

        StatusText = status;
    }

    private static string DescribeSweep(ScanReport report)
    {
        var summary = string.Create(
            CultureInfo.InvariantCulture,
            $"{report.Diagnosis.Findings.Count} finding(s) from {report.Events.Count} evidence lines in {report.Duration.TotalSeconds:0.0}s.");

        if (report.Failures.Count == 0)
        {
            return summary;
        }

        var failures = string.Join("; ", report.Failures.Select(failure => $"{failure.SourceId}: {failure.Message}"));
        return summary + " Some collectors failed: " + failures;
    }
}
