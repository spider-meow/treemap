using System.Windows;
using Treemap.App.ViewModels;
using Treemap.Core.Specs;

namespace Treemap.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var options = StartupOptions.Parse(e.Args);
        var viewModel = new MainViewModel(new WmiSpecsProvider(), options);
        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        await viewModel.InitializeAsync();
    }
}
