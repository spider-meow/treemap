using System.Windows;
using System.Windows.Media;
using Treemap.App.Services;
using Treemap.App.ViewModels;
using Treemap.Core.Monitoring;
using Treemap.Core.Settings;
using Treemap.Core.Specs;

namespace Treemap.App;

public partial class App : Application
{
    private MonitoringService? _monitoring;
    private OverlayController? _overlay;
    private GlobalHotkey? _hotkey;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        UseSystemAccent();

        var options = StartupOptions.Parse(e.Args);
        _monitoring = new MonitoringService(new SystemMetricsSource(), Dispatcher);
        _overlay = new OverlayController(_monitoring, new SettingsStore(), Dispatcher);
        _hotkey = new GlobalHotkey(_overlay.Toggle);

        var overlaySettings = new OverlaySettingsViewModel(_overlay, () => _hotkey.IsRegistered);
        var viewModel = new MainViewModel(new WmiSpecsProvider(), _monitoring, overlaySettings, options);
        var window = new MainWindow { DataContext = viewModel };

        // Registered before the window is shown so the settings card reads the real state on first display.
        _hotkey.Register(window);
        window.Show();

        _monitoring.Start();
        _overlay.Start();
        await viewModel.InitializeAsync();
    }

    /// <summary>Graphs and bars follow the accent colour of Windows, like the standard controls already do.</summary>
    private void UseSystemAccent()
    {
        var accent = SystemColors.AccentColor;
        var solid = new SolidColorBrush(accent);
        var fill = new SolidColorBrush(Color.FromArgb(0x30, accent.R, accent.G, accent.B));
        solid.Freeze();
        fill.Freeze();
        Resources["AccentBrush"] = solid;
        Resources["AccentFillBrush"] = fill;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        _overlay?.Dispose();
        _monitoring?.Dispose();
        base.OnExit(e);
    }
}
