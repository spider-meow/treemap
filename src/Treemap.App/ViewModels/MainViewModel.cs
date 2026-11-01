using Treemap.App.Infrastructure;
using Treemap.Core.Specs;

namespace Treemap.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private int _selectedTabIndex;

    public MainViewModel(ISpecsProvider specsProvider, StartupOptions options)
    {
        Components = new ComponentsViewModel(specsProvider);
        _selectedTabIndex = options.TabIndex;
    }

    public ComponentsViewModel Components { get; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public Task InitializeAsync() => Components.LoadAsync();
}
