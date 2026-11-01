using System.Windows;
using Treemap.App.Services;
using Treemap.Core.Monitoring;
using Treemap.Core.Settings;
using Treemap.Core.Specs;

namespace Treemap.App;

public partial class App : Application
{
    private MonitoringService? _monitoring;
    private OverlayController? _overlay;
    private GlobalHotkey? _hotkey;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var options = StartupOptions.Parse(e.Args);
        _monitoring = new MonitoringService(new SystemMetricsSource(), Dispatcher);
        _overlay = new OverlayController(_monitoring, new SettingsStore(), Dispatcher);
        _hotkey = new GlobalHotkey(_overlay.Toggle);

        var window = new WebHostWindow(new WmiSpecsProvider(), _monitoring, _overlay, _hotkey, options);

        // Registered before the window is shown so the overlay settings read the real state on first display.
        _hotkey.Register(window);
        window.Show();

        _monitoring.Start();
        _overlay.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        _overlay?.Dispose();
        _monitoring?.Dispose();
        base.OnExit(e);
    }
}
