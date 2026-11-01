using Treemap.App.Infrastructure;
using Treemap.App.Services;
using Treemap.Core.Specs;

namespace Treemap.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly StartupOptions _options;
    private int _selectedTabIndex;

    public MainViewModel(ISpecsProvider specsProvider, MonitoringService monitoring, StartupOptions options)
    {
        _options = options;
        Components = new ComponentsViewModel(specsProvider);
        Storage = new StorageViewModel();
        Monitor = new MonitorViewModel(monitoring);
        _selectedTabIndex = options.TabIndex;
    }

    public ComponentsViewModel Components { get; }

    public StorageViewModel Storage { get; }

    public MonitorViewModel Monitor { get; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public async Task InitializeAsync()
    {
        var components = Components.LoadAsync();
        if (_options.ScanPath is { } path)
        {
            await Storage.ScanPathAsync(path);
        }

        await components;
    }
}
