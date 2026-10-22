namespace StreamDoctor.Core.Usb;

/// <summary>Estimated load on one controller, derived from device classes.</summary>
public sealed record UsbControllerLoad
{
    public required UsbController Controller { get; init; }

    /// <summary>Sum of <see cref="UsbDevice.BandwidthWeight"/> for the controller's peripherals.</summary>
    public required double LoadUnits { get; init; }

    /// <summary>Estimated capacity of the controller, in the same units.</summary>
    public required double BudgetUnits { get; init; }

    public required IReadOnlyList<UsbDevice> Devices { get; init; }

    public double Utilization => BudgetUnits <= 0 ? 0 : LoadUnits / BudgetUnits;

    /// <summary>Devices that cannot tolerate a saturated bus without visible dropouts.</summary>
    public int IsochronousDeviceCount => Devices.Count(static device => device.Class.IsIsochronous());

    /// <summary>Nothing else can be added, and dropouts are likely right now.</summary>
    public bool IsSaturated => Utilization >= 1.0;

    /// <summary>
    /// Risk without proof of saturation: either the budget is mostly spoken for, or
    /// two or more isochronous devices share a USB 2.0 controller (the classic
    /// "camtasia + capture card on one hub" failure).
    /// </summary>
    public bool IsAtRisk => IsSaturated || Utilization >= 0.75 ||
        (Controller.Kind == UsbControllerKind.Usb2 && IsochronousDeviceCount >= 2);
}

/// <summary>
/// Static (non-perturbing) bandwidth planning analysis. Design doc §2.1 asks for
/// controller-level saturation detection; v1 estimates demand from device class
/// weights and the test bench replaces the estimate with measurements later.
/// </summary>
public static class UsbBandwidthAnalyzer
{
    /// <summary>Fraction of the budget at which we warn about headroom.</summary>
    public const double HeadroomWarningThreshold = 0.75;

    public static IReadOnlyList<UsbControllerLoad> Analyze(UsbTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);

        return topology.Controllers
            .Select(controller =>
            {
                var devices = controller.Peripherals
                    .OrderByDescending(static device => device.BandwidthWeight)
                    .ThenBy(static device => device.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new UsbControllerLoad
                {
                    Controller = controller,
                    LoadUnits = devices.Sum(static device => device.BandwidthWeight),
                    BudgetUnits = controller.Kind.BudgetUnits(),
                    Devices = devices,
                };
            })
            .OrderByDescending(static load => load.Utilization)
            .ThenBy(static load => load.Controller.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>The idle controllers an overloaded device could move to.</summary>
    public static IReadOnlyList<UsbControllerLoad> FindHeadroom(
        IReadOnlyList<UsbControllerLoad> loads,
        UsbControllerLoad overloaded)
    {
        ArgumentNullException.ThrowIfNull(loads);
        ArgumentNullException.ThrowIfNull(overloaded);

        return loads
            .Where(load => !ReferenceEquals(load.Controller, overloaded.Controller))
            .Where(load => load.Utilization + (overloaded.LoadUnits / Math.Max(load.BudgetUnits, 0.001)) <= 1.0)
            .OrderBy(static load => load.Utilization)
            .ToList();
    }
}
