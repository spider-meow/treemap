using System.Windows.Controls;
using Treemap.App.Controls;
using Treemap.App.ViewModels;

namespace Treemap.App.Views;

public partial class StorageView : UserControl
{
    public StorageView()
    {
        InitializeComponent();
    }

    private StorageViewModel? ViewModel => DataContext as StorageViewModel;

    private void OnTreemapHover(object? sender, TreemapNodeEventArgs e) => ViewModel?.ReportHover(e.Node);

    private void OnTreemapClicked(object? sender, TreemapNodeEventArgs e)
    {
        if (e.Node is { } node)
        {
            ViewModel?.OpenFromTreemap(node);
        }
    }
}
