using Microsoft.UI.Xaml;
using Serilog;

namespace StreamDoctor.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();

        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StreamDoctor",
            "logs");
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logDirectory, "streamdoctor-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        UnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Unhandled UI exception");
            args.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
