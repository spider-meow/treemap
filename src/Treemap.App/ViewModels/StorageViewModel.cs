using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using Treemap.App.Infrastructure;
using Treemap.Core.Formatting;
using Treemap.Core.Storage;

namespace Treemap.App.ViewModels;

public sealed record DriveOption(string Path, string Display);

public sealed record HogLocation(string Path, string SizeText);

public sealed class HogViewModel
{
    public HogViewModel(SpaceHog hog, long scannedBytes, ICommand revealCommand)
    {
        Title = hog.Title;
        SizeText = ByteSize.Format(hog.SizeBytes);
        ShareText = scannedBytes > 0 ? (hog.SizeBytes / (double)scannedBytes).ToString("P1") : string.Empty;
        CountText = hog.Nodes.Count > 1 ? $"{hog.Nodes.Count} emplacements" : "1 emplacement";
        Explanation = hog.Explanation;
        Advice = hog.Advice;
        Locations = hog.Nodes.Take(6).Select(n => new HogLocation(n.FullPath, ByteSize.Format(n.SizeBytes))).ToList();
        MoreText = hog.Nodes.Count > Locations.Count ? $"… et {hog.Nodes.Count - Locations.Count} autres" : string.Empty;
        RevealCommand = revealCommand;
    }

    public string Title { get; }

    public string SizeText { get; }

    public string ShareText { get; }

    public string CountText { get; }

    public string Explanation { get; }

    public string Advice { get; }

    public IReadOnlyList<HogLocation> Locations { get; }

    public string MoreText { get; }

    public ICommand RevealCommand { get; }
}

public sealed class TreeNodeViewModel : ObservableObject
{
    private readonly Action<TreeNodeViewModel> _onSelected;
    private ObservableCollection<TreeNodeViewModel>? _children;
    private bool _isExpanded;
    private bool _isSelected;

    public TreeNodeViewModel(FolderNode node, TreeNodeViewModel? parent, Action<TreeNodeViewModel> onSelected)
    {
        Node = node;
        Parent = parent;
        _onSelected = onSelected;

        var parentSize = parent?.Node.SizeBytes ?? node.SizeBytes;
        Fraction = parentSize > 0 ? (double)node.SizeBytes / parentSize : 0;
        SizeText = ByteSize.Format(node.SizeBytes);
        PercentText = Fraction.ToString("P0");
    }

    public FolderNode Node { get; }

    public TreeNodeViewModel? Parent { get; }

    public string Name => Node.Name;

    public bool IsFolder => Node.IsFolder;

    public double Fraction { get; }

    public string SizeText { get; }

    public string PercentText { get; }

    /// <summary>Built on first access so that a whole-drive tree is only materialised where the user looks.</summary>
    public ObservableCollection<TreeNodeViewModel> Children =>
        _children ??= new ObservableCollection<TreeNodeViewModel>(
            Node.Children.Select(c => new TreeNodeViewModel(c, this, _onSelected)));

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value) && value)
            {
                _onSelected(this);
            }
        }
    }
}

public sealed class StorageViewModel : ObservableObject
{
    private CancellationTokenSource? _cts;
    private DriveOption? _selectedTarget;
    private bool _isScanning;
    private bool _hasResult;
    private string _statusText = "Choisis un disque puis lance l'analyse.";
    private string _summaryText = string.Empty;
    private string _infoText = string.Empty;
    private string _hoverText = string.Empty;
    private int _rightTabIndex;
    private TreeNodeViewModel? _currentFolder;
    private TreeNodeViewModel? _selectedNode;

    public StorageViewModel()
    {
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            Targets.Add(new DriveOption(
                drive.RootDirectory.FullName,
                $"{drive.Name} — {ByteSize.Format(drive.AvailableFreeSpace)} libres sur {ByteSize.Format(drive.TotalSize)}"));
        }

        _selectedTarget = Targets.FirstOrDefault();

        ScanCommand = new AsyncRelayCommand(_ => ScanAsync(), _ => !IsScanning && SelectedTarget is not null);
        CancelCommand = new RelayCommand(_ => _cts?.Cancel(), _ => IsScanning);
        BrowseCommand = new RelayCommand(_ => Browse(), _ => !IsScanning);
        GoUpCommand = new RelayCommand(_ => CurrentFolder!.Parent!.IsSelected = true, _ => CurrentFolder?.Parent is not null);
        RevealCommand = new RelayCommand(p => Reveal(p as string ?? SelectedNode?.Node.FullPath), _ => true);
    }

    public ObservableCollection<DriveOption> Targets { get; } = [];

    public ObservableCollection<TreeNodeViewModel> RootNodes { get; } = [];

    public ObservableCollection<HogViewModel> Hogs { get; } = [];

    public ICommand ScanCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand BrowseCommand { get; }

    public ICommand GoUpCommand { get; }

    public ICommand RevealCommand { get; }

    public DriveOption? SelectedTarget
    {
        get => _selectedTarget;
        set => SetProperty(ref _selectedTarget, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                OnPropertyChanged(nameof(ShowEmptyState));
                OnPropertyChanged(nameof(IsIdle));

                // WPF only re-queries commands on input events; a scan started without input must not leave stale buttons.
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsIdle => !IsScanning;

    public bool NoHogs => HasResult && Hogs.Count == 0;

    public bool HasResult
    {
        get => _hasResult;
        private set
        {
            if (SetProperty(ref _hasResult, value))
            {
                OnPropertyChanged(nameof(ShowEmptyState));
                OnPropertyChanged(nameof(NoHogs));
            }
        }
    }

    public bool ShowEmptyState => !IsScanning && !HasResult;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public string InfoText
    {
        get => _infoText;
        private set => SetProperty(ref _infoText, value);
    }

    public string HoverText
    {
        get => _hoverText;
        private set => SetProperty(ref _hoverText, value);
    }

    public int RightTabIndex
    {
        get => _rightTabIndex;
        set => SetProperty(ref _rightTabIndex, value);
    }

    public string HogsHeader => Hogs.Count > 0 ? $"À examiner ({Hogs.Count})" : "À examiner";

    /// <summary>The folder whose entries the treemap currently shows.</summary>
    public TreeNodeViewModel? CurrentFolder
    {
        get => _currentFolder;
        private set
        {
            if (SetProperty(ref _currentFolder, value))
            {
                OnPropertyChanged(nameof(CurrentFolderNode));
                OnPropertyChanged(nameof(CurrentPath));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public FolderNode? CurrentFolderNode => CurrentFolder?.Node;

    public string CurrentPath => CurrentFolder?.Node.FullPath ?? string.Empty;

    public TreeNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        private set => SetProperty(ref _selectedNode, value);
    }

    /// <summary>Starts a scan of the given folder, used for the <c>--scan</c> switch.</summary>
    public Task ScanPathAsync(string path)
    {
        var target = new DriveOption(path, path);
        Targets.Add(target);
        SelectedTarget = target;
        return ScanAsync();
    }

    public void ReportHover(FolderNode? node)
    {
        if (node is null || CurrentFolder is null)
        {
            HoverText = string.Empty;
            return;
        }

        var parentSize = CurrentFolder.Node.SizeBytes;
        var share = parentSize > 0 ? $" · {(double)node.SizeBytes / parentSize:P1} du dossier" : string.Empty;
        HoverText = $"{node.FullPath}{(node.Kind == NodeKind.OtherFiles ? " (fichiers regroupés)" : string.Empty)} — {ByteSize.Format(node.SizeBytes)}{share}";
    }

    /// <summary>Called when a treemap cell is clicked: moves the tree selection to that entry.</summary>
    public void OpenFromTreemap(FolderNode node)
    {
        var target = CurrentFolder?.Children.FirstOrDefault(c => ReferenceEquals(c.Node, node));
        if (target is null)
        {
            return;
        }

        for (var p = target.Parent; p is not null; p = p.Parent)
        {
            p.IsExpanded = true;
        }

        target.IsSelected = true;
    }

    private async Task ScanAsync()
    {
        if (SelectedTarget is not { } target)
        {
            return;
        }

        using var cts = new CancellationTokenSource();
        _cts = cts;
        IsScanning = true;
        HasResult = false;
        RootNodes.Clear();
        Hogs.Clear();
        OnPropertyChanged(nameof(HogsHeader));
        CurrentFolder = null;
        SelectedNode = null;
        SummaryText = string.Empty;
        InfoText = string.Empty;
        HoverText = string.Empty;
        StatusText = $"Analyse de {target.Path}…";

        var progress = new Progress<ScanProgress>(p =>
            StatusText = $"Analyse en cours… {p.FilesScanned:N0} fichiers · {ByteSize.Format(p.BytesScanned)}");

        try
        {
            var result = await new DiskScanner().ScanAsync(target.Path, progress, cts.Token);
            ApplyResult(result);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Analyse annulée.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = $"Analyse impossible : {ex.Message}";
        }
        finally
        {
            _cts = null;
            IsScanning = false;
        }
    }

    private void ApplyResult(ScanResult result)
    {
        var root = new TreeNodeViewModel(result.Root, null, OnNodeSelected) { IsExpanded = true };
        RootNodes.Add(root);
        root.IsSelected = true;

        foreach (var hog in SpaceHogDetector.Find(result.Root))
        {
            Hogs.Add(new HogViewModel(hog, result.Root.SizeBytes, RevealCommand));
        }

        OnPropertyChanged(nameof(HogsHeader));
        RightTabIndex = 0;
        HasResult = true;

        StatusText = "Analyse terminée.";
        SummaryText =
            $"{ByteSize.Format(result.Root.SizeBytes)} dans {result.FileCount:N0} fichiers et {result.FolderCount:N0} dossiers · {result.Elapsed.TotalSeconds:N1} s";

        var notes = new List<string>();
        if (result.InaccessibleFolders > 0)
        {
            notes.Add($"{result.InaccessibleFolders:N0} dossiers n'ont pas pu être lus (droits insuffisants) : leur taille n'est pas comptée.");
        }

        if (UnreadableSpaceNote(result) is { } gap)
        {
            notes.Add(gap);
        }

        if (result.SavedBySparseOrCompressedFiles > 0)
        {
            notes.Add(
                $"Fichiers creux ou compressés comptés pour leur taille réelle sur disque ({ByteSize.Format(result.SavedBySparseOrCompressedFiles)} de moins que leur taille affichée par Windows).");
        }

        if (result.CloudOnlyFiles > 0)
        {
            notes.Add($"{result.CloudOnlyFiles:N0} fichiers OneDrive en ligne uniquement ne prennent pas de place locale.");
        }

        InfoText = string.Join(" ", notes);
    }

    /// <summary>
    /// When a whole drive was scanned, Windows' own "used" figure is usually a bit higher than what could be read
    /// (restore points, NTFS metadata, folders needing administrator rights). Say so instead of leaving it unexplained.
    /// </summary>
    private static string? UnreadableSpaceNote(ScanResult result)
    {
        const long threshold = 1L << 30;
        var root = result.Root.FullPath;
        if (Path.GetPathRoot(root) is not { } driveRoot || !string.Equals(driveRoot, root, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var drive = new DriveInfo(driveRoot);
            var gap = drive.TotalSize - drive.AvailableFreeSpace - result.Root.SizeBytes;
            return gap > threshold
                ? $"Windows compte {ByteSize.Format(gap)} de plus que ce qui a pu être lu : points de restauration, métadonnées NTFS et dossiers réservés au système (visibles seulement avec les droits administrateur)."
                : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private void OnNodeSelected(TreeNodeViewModel node)
    {
        SelectedNode = node;
        CurrentFolder = node.IsFolder ? node : node.Parent ?? node;
    }

    private void Browse()
    {
        var dialog = new OpenFolderDialog { Title = "Choisir un dossier à analyser" };
        if (dialog.ShowDialog() != true || string.IsNullOrEmpty(dialog.FolderName))
        {
            return;
        }

        var target = new DriveOption(dialog.FolderName, dialog.FolderName);
        Targets.Add(target);
        SelectedTarget = target;
    }

    private static void Reveal(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        // Read-only: this only asks Explorer to show the item.
        var arguments = File.Exists(path) ? $"/select,\"{path}\"" : $"\"{path}\"";
        Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = false });
    }
}
