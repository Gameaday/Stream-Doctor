using StreamDoctor.Core.Diagnosis;
using StreamDoctor.Core.Fixes;
using StreamDoctor.Core.Model;
using StreamDoctor.Core.PostFlight;
using StreamDoctor.Core.Scanning;
using StreamDoctor.Core.Updates;
using StreamDoctor.Core.Usb;

namespace StreamDoctor.Tests;

public sealed class PreFlightRunnerTests
{
    [Fact]
    public async Task RunAsync_CollectsEvidenceFromEverySource()
    {
        var mic = TestData.Device("Microphone", UsbDeviceClass.Microphone);
        var topology = TestData.Topology(TestData.Controller("USB 3.0 Controller", UsbControllerKind.Usb3, mic));

        var runner = new PreFlightRunner(
            [
                new FakeSource("usb", ScanModule.UsbSentinel, _ => Task.FromResult(new ScanSourceResult
                {
                    Events = UsbTopologyEvents.FromTopology(topology),
                    Topology = topology,
                    PowerSnapshots = [new UsbPowerSnapshot(mic, new UsbPowerSettings { SelectiveSuspendEnabled = true })],
                    PowerPlan = new PowerPlanInfo("Balanced", "381b4222-f694-41f0-9685-ff5bb260df2e", IsHighPerformance: false),
                })),
                new FakeSource("system", ScanModule.DiskRecording, _ => Task.FromResult(new ScanSourceResult
                {
                    Events = [TestEvents.Disk("C:", 10, 2000)],
                })),
            ],
            new DiagnosisEngine(DefaultRuleSet.Create()));

        var report = await runner.RunAsync(ScanRequest.PreFlight(TestData.At(12)), CancellationToken.None);

        Assert.True(report.IsComplete);
        Assert.Empty(report.Failures);
        Assert.Contains(ScanModule.UsbSentinel, report.ScannedModules);
        Assert.Contains(ScanModule.DiskRecording, report.ScannedModules);
        Assert.NotNull(report.Topology);
        Assert.Single(report.Topology.Peripherals);
        Assert.Single(report.PowerSnapshots);
        Assert.NotNull(report.PowerPlan);
        Assert.Contains(report.Diagnosis.Findings, finding => finding.HypothesisId == "usb.power.selective-suspend");
        Assert.Contains(report.Diagnosis.Findings, finding => finding.HypothesisId == "disk.low-space");
        Assert.True(report.Duration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task RunAsync_RecordsAFailedSourceAndKeepsGoing()
    {
        var runner = new PreFlightRunner(
            [
                new FakeSource("broken", ScanModule.UsbSentinel, _ => throw new InvalidOperationException("driver missing")),
                new FakeSource("system", ScanModule.DiskRecording, _ => Task.FromResult(new ScanSourceResult
                {
                    Events = [TestEvents.Disk("C:", 900, 2000)],
                })),
            ],
            new DiagnosisEngine(DefaultRuleSet.Create()));

        var report = await runner.RunAsync(ScanRequest.PreFlight(TestData.At(12)), CancellationToken.None);

        var failure = Assert.Single(report.Failures);
        Assert.Equal("broken", failure.SourceId);
        Assert.False(failure.TimedOut);
        Assert.False(report.IsComplete);
        Assert.Contains(ScanModule.DiskRecording, report.ScannedModules);
        Assert.DoesNotContain(ScanModule.UsbSentinel, report.ScannedModules);
    }

    [Fact]
    public async Task RunAsync_TimesOutAHungSourceInsteadOfHangingTheSweep()
    {
        var runner = new PreFlightRunner(
            [
                new FakeSource("slow", ScanModule.UsbSentinel, async token =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), token);
                    return ScanSourceResult.Empty;
                }),
            ],
            new DiagnosisEngine([]));

        var request = ScanRequest.PreFlight(TestData.At(12)) with { SourceTimeout = TimeSpan.FromMilliseconds(50) };
        var report = await runner.RunAsync(request, CancellationToken.None);

        var failure = Assert.Single(report.Failures);
        Assert.True(failure.TimedOut);
        Assert.False(report.IsComplete);
    }

    [Fact]
    public async Task RunAsync_MarksOptionalFailuresWithoutFailingTheSweep()
    {
        var runner = new PreFlightRunner(
            [new FakeSource("obs", ScanModule.FramePacing, _ => throw new FileNotFoundException("no logs"), optional: true)],
            new DiagnosisEngine([]));

        var report = await runner.RunAsync(ScanRequest.PreFlight(TestData.At(12)), CancellationToken.None);

        Assert.True(report.IsComplete);
        var failure = Assert.Single(report.Failures);
        Assert.True(failure.Optional);
    }

    [Fact]
    public async Task RunAsync_PlansUpdatesAndFeedsTheDiagnosis()
    {
        var candidate = new UpdateCandidate
        {
            Id = "OBSProject.OBSStudio",
            Name = "OBS Studio",
            Kind = UpdateKind.Application,
            Source = UpdateSource.Winget,
            InstalledVersion = "31.0.1",
            AvailableVersion = "31.0.2",
        };

        var runner = new PreFlightRunner(
            [new FakeSource("updates", ScanModule.StreamingSoftware, _ => Task.FromResult(new ScanSourceResult
            {
                Updates = [candidate],
            }))],
            new DiagnosisEngine(DefaultRuleSet.Create()));

        var report = await runner.RunAsync(ScanRequest.PreFlight(TestData.At(12)), CancellationToken.None);

        Assert.Single(report.Updates);
        Assert.NotNull(report.UpdatePlan);
        Assert.Single(report.UpdatePlan!.AutoApplicable);
        Assert.Contains(
            report.Diagnosis.Findings,
            finding => finding.FixPlaybookId == PlaybookIds.UpdatePlaybook(candidate.Id));
        Assert.Contains(ScanModule.StreamingSoftware, report.ScannedModules);
    }

    [Fact]
    public async Task RunAsync_HonoursCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var runner = new PreFlightRunner(
            [
                new FakeSource("usb", ScanModule.UsbSentinel, async token =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), token);
                    return ScanSourceResult.Empty;
                }),
            ],
            new DiagnosisEngine([]));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.RunAsync(ScanRequest.PreFlight(TestData.At(12)), cancellation.Token));
    }

    private sealed class FakeSource : IScanSource
    {
        private readonly Func<CancellationToken, Task<ScanSourceResult>> _collect;

        public FakeSource(
            string id,
            ScanModule module,
            Func<CancellationToken, Task<ScanSourceResult>> collect,
            bool optional = false)
        {
            Id = id;
            OwnerModule = module;
            IsOptional = optional;
            _collect = collect;
        }

        public string Id { get; }

        public ScanModule OwnerModule { get; }

        public bool IsOptional { get; }

        public Task<ScanSourceResult> CollectAsync(ScanRequest request, CancellationToken cancellationToken) =>
            _collect(cancellationToken);
    }
}
