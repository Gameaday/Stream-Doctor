using StreamDoctor.Core.Diagnosis;
using StreamDoctor.Core.Model;

namespace StreamDoctor.Tests;

public sealed class DefaultRuleSetTests
{
    [Fact]
    public void AllRulesHaveUniqueIds()
    {
        var rules = DefaultRuleSet.Create();

        Assert.Equal(rules.Count, rules.Select(rule => rule.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryRuleDeclaresTheModuleItReports()
    {
        Assert.All(DefaultRuleSet.Create(), rule => Assert.True(Enum.IsDefined(rule.OwnerModule)));
    }

    [Fact]
    public void EveryPlaybookReferencedByTheRulesExists()
    {
        var document = TestData.ParseObs("bad-session.log");
        var topology = TestData.Topology(
            TestData.Controller(
                "Standard Enhanced PCI to USB Host Controller",
                Core.Usb.UsbControllerKind.Usb2,
                TestData.Device("Webcam", Core.Usb.UsbDeviceClass.Webcam),
                TestData.Device("Capture Card", Core.Usb.UsbDeviceClass.CaptureCard)));
        var mic = topology.Peripherals[0];

        var report = TestEvents.Analyze(new DiagnosisContext
        {
            Events = document.Events.Concat(Core.Usb.UsbTopologyEvents.FromTopology(topology)).ToList(),
            Obs = document,
            Topology = topology,
            PowerSnapshots = [new Core.Usb.UsbPowerSnapshot(mic, new Core.Usb.UsbPowerSettings { SelectiveSuspendEnabled = true })],
            PowerPlan = new Core.Usb.PowerPlanInfo("Balanced", "guid", IsHighPerformance: false),
        });

        var referenced = report.Findings
            .Select(finding => finding.FixPlaybookId)
            .Where(id => id is not null)
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal);

        Assert.NotEmpty(referenced);
        Assert.All(referenced, id => Assert.NotNull(Fixes.PlaybookCatalog.Find(id)));
    }
}
