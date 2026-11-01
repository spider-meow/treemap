using System.Windows;
using Treemap.App.Services;
using Treemap.App.ViewModels;
using Treemap.Core.Monitoring;
using Treemap.Core.Specs;

namespace Treemap.App;

public partial class App : Application
{
    private MonitoringService? _monitoring;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var options = StartupOptions.Parse(e.Args);
        _monitoring = new MonitoringService(new SystemMetricsSource(), Dispatcher);
        var viewModel = new MainViewModel(new WmiSpecsProvider(), _monitoring, options);
        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        _monitoring.Start();
        await viewModel.InitializeAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _monitoring?.Dispose();
        base.OnExit(e);
    }
}
