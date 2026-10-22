using Microsoft.UI.Xaml;
using StreamDoctor.App.ViewModels;

namespace StreamDoctor.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "Stream Doctor";
    }

    public DashboardViewModel ViewModel { get; } = new();
}
