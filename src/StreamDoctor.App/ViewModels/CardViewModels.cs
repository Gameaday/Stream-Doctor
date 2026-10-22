using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using StreamDoctor.Core.Model;
using Windows.UI;

namespace StreamDoctor.App.ViewModels;

/// <summary>One dashboard gauge: module name, traffic-light state and finding count.</summary>
public sealed class ModuleCardViewModel
{
    public ModuleCardViewModel(ModuleHealth health)
    {
        ArgumentNullException.ThrowIfNull(health);

        Name = health.Module.DisplayName();
        Detail = health.FindingCount switch
        {
            0 => health.WasScanned ? "No findings" : "Not scanned",
            1 => "1 finding",
            _ => $"{health.FindingCount} findings",
        };

        StatusText = !health.WasScanned
            ? "Not scanned"
            : health.State switch
            {
                HealthState.Green => "Healthy",
                HealthState.Yellow => "Needs attention",
                HealthState.Red => "Problem found",
                _ => "Unknown",
            };

        StateBrush = new SolidColorBrush(health.State switch
        {
            HealthState.Green => Color.FromArgb(255, 46, 160, 67),
            HealthState.Yellow => Color.FromArgb(255, 210, 153, 34),
            HealthState.Red => Color.FromArgb(255, 248, 81, 73),
            _ => Color.FromArgb(255, 139, 148, 158),
        });
    }

    public string Name { get; }

    public string StatusText { get; }

    public string Detail { get; }

    public SolidColorBrush StateBrush { get; }
}

/// <summary>One ranked finding row, already formatted for the dashboard.</summary>
public sealed class FindingViewModel
{
    public FindingViewModel(Finding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);

        Title = finding.Title;
        Explanation = finding.Explanation;
        ModuleName = finding.Module.DisplayName();
        SeverityText = finding.Severity.ToString().ToUpperInvariant();
        ConfidenceText = $"{(finding.Confidence * 100):0}% confidence";

        ActionText = finding.IsActionable
            ? "Fix available: open the Fix Wizard and Stream Doctor will journal the change so it can be undone."
            : finding.RecommendedAction ?? "No automated fix; follow the explanation above.";

        EvidenceText = finding.Occurrences == 1
            ? "1 piece of evidence"
            : $"{finding.Occurrences} pieces of evidence";

        SeverityBrush = new SolidColorBrush(finding.Severity switch
        {
            Severity.Critical => Color.FromArgb(255, 248, 81, 73),
            Severity.Warning => Color.FromArgb(255, 210, 153, 34),
            Severity.Advisory => Color.FromArgb(255, 88, 166, 255),
            _ => Color.FromArgb(255, 139, 148, 158),
        });
    }

    public string Title { get; }

    public string Explanation { get; }

    public string ModuleName { get; }

    public string SeverityText { get; }

    public string ConfidenceText { get; }

    public string ActionText { get; }

    public string EvidenceText { get; }

    public SolidColorBrush SeverityBrush { get; }
}
