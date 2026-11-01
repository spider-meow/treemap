using System.Text.Json;
using Treemap.Core.Formatting;
using Treemap.Core.Storage;
using Treemap.Core.Storage.Cleanup;

namespace Treemap.Core.Bridge;

/// <summary>What the host does on the desktop for the storage screen. Implemented by the app (Explorer, launchers).</summary>
public interface IShellLauncher
{
    /// <summary>Shows the item in Explorer (read only).</summary>
    void Reveal(string path);

    /// <summary>Starts a launcher address; callers only pass addresses accepted by <see cref="DeletionAdvisor.IsSafeLauncherUri"/>.</summary>
    void OpenUri(string uri);
}

public sealed record DriveDto(string Path, string Label, long TotalBytes, long FreeBytes);

public sealed record ChildDto(
    string Path,
    string Name,
    NodeKind Kind,
    long SizeBytes,
    long FileCount,
    bool HasChildren,
    DeletionVerdict Verdict,
    string Title);

public sealed record NodeDto(
    string Path,
    string Name,
    NodeKind Kind,
    long SizeBytes,
    long FileCount,
    string? ParentPath,
    DeletionVerdict Verdict,
    string Title,
    IReadOnlyList<ChildDto> Children,
    int HiddenCount,
    long HiddenBytes);

public sealed record HogLocationDto(string Path, long SizeBytes, DeletionVerdict Verdict);

public sealed record HogDto(
    string RuleId,
    string Title,
    string Explanation,
    string Advice,
    long SizeBytes,
    IReadOnlyList<HogLocationDto> Locations,
    int MoreCount);

public sealed record ScanSummaryDto(
    string RootPath,
    string RootName,
    long SizeBytes,
    long FileCount,
    long FolderCount,
    double ElapsedSeconds,
    IReadOnlyList<string> Notes);

public sealed record ScanReplyDto(bool Cancelled, ScanSummaryDto? Summary, IReadOnlyList<HogDto> Hogs);

public sealed record DeleteReplyDto(IReadOnlyList<DeletionResult> Results, long FreedBytes, bool Refused, string? Reason, IReadOnlyList<HogDto> Hogs, long RootSizeBytes);

/// <summary>
/// The storage messages of the web interface: scan, browse, preview, delete, reveal, open an uninstaller.
/// Everything that can change the disk goes through <see cref="StorageSession"/>, which checks it again.
/// </summary>
public sealed class StorageBridge
{
    private const int MaxChildren = 200;
    private const int MaxLocationsPerHog = 8;
    private const long ProgressIntervalTicks = TimeSpan.TicksPerMillisecond * 250;

    private readonly StorageSession _session;
    private readonly IRecycler _recycler;
    private readonly IShellLauncher _launcher;
    private readonly Action<string, object?> _emit;
    private long _lastProgressTicks;

    public StorageBridge(StorageSession session, IRecycler recycler, IShellLauncher launcher, Action<string, object?> emit)
    {
        _session = session;
        _recycler = recycler;
        _launcher = launcher;
        _emit = emit;
    }

    private sealed record PathRequest(string? Path);

    private sealed record PathsRequest(string[]? Paths, string? Confirmation);

    private sealed record LauncherRequest(string? Path, string? Launcher);

    public void Register(BridgeRouter router)
    {
        router.Register("storage.drives", () => Task.FromResult<object?>(Drives()));
        router.Register("storage.scan", async p => await ScanAsync(Read<PathRequest>(p).Path));
        router.Register("storage.cancel", () =>
        {
            _session.CancelScan();
            return Task.FromResult<object?>(null);
        });
        router.Register("storage.node", p => Task.FromResult<object?>(Node(Read<PathRequest>(p).Path)));
        router.Register("storage.preview", p => Task.FromResult<object?>(_session.Preview(Read<PathsRequest>(p).Paths ?? [])));
        router.Register("storage.delete", p => Task.Run<object?>(() => Delete(Read<PathsRequest>(p))));
        router.Register("storage.reveal", p =>
        {
            var node = Require(Read<PathRequest>(p).Path);
            _launcher.Reveal(node.FullPath);
            return Task.FromResult<object?>(null);
        });
        router.Register("storage.openUninstaller", p =>
        {
            OpenUninstaller(Read<LauncherRequest>(p));
            return Task.FromResult<object?>(null);
        });
    }

    private static T Read<T>(JsonElement payload) where T : class =>
        (payload.ValueKind == JsonValueKind.Undefined ? null : payload.Deserialize<T>(BridgeRouter.Options))
        ?? throw new ArgumentException("Requête incomplète.");

    private static List<DriveDto> Drives()
    {
        var drives = new List<DriveDto>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType == DriveType.Fixed && drive.IsReady)
                {
                    drives.Add(new DriveDto(drive.Name, drive.VolumeLabel, drive.TotalSize, drive.AvailableFreeSpace));
                }
            }
            catch (IOException)
            {
                // A drive that disappears while listing is simply left out.
            }
        }

        return drives;
    }

    private async Task<ScanReplyDto> ScanAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Aucun dossier à analyser.");
        }

        var progress = new InlineProgress<ScanProgress>(p =>
        {
            // The scanner reports from many threads: keep to a few updates per second.
            var now = DateTime.UtcNow.Ticks;
            var last = Interlocked.Read(ref _lastProgressTicks);
            if (now - last >= ProgressIntervalTicks && Interlocked.CompareExchange(ref _lastProgressTicks, now, last) == last)
            {
                _emit("storage.progress", new { p.FilesScanned, p.BytesScanned, p.CurrentPath });
            }
        });

        try
        {
            var result = await _session.ScanAsync(path, progress, CancellationToken.None);
            var root = result.Root;
            var summary = new ScanSummaryDto(
                root.FullPath, root.Name, root.SizeBytes, result.FileCount, result.FolderCount, result.Elapsed.TotalSeconds, Notes(result));
            return new ScanReplyDto(false, summary, Hogs());
        }
        catch (OperationCanceledException)
        {
            return new ScanReplyDto(true, null, []);
        }
    }

    private NodeDto Node(string? path)
    {
        var node = Require(path);
        var assessment = Classify(node);

        var shown = node.Children.Take(MaxChildren).Select(c =>
        {
            var child = Classify(c);
            return new ChildDto(
                c.Kind == NodeKind.OtherFiles ? c.FullPath + "\\…" : c.FullPath,
                c.Name, c.Kind, c.SizeBytes, c.FileCount, c.IsFolder && c.Children.Count > 0, child.Verdict, child.Title);
        }).ToList();

        var hidden = node.Children.Skip(MaxChildren).ToList();

        return new NodeDto(
            node.FullPath, node.Name, node.Kind, node.SizeBytes, node.FileCount, node.Parent?.FullPath,
            assessment.Verdict, assessment.Title, shown, hidden.Count, hidden.Sum(c => c.SizeBytes));
    }

    private DeleteReplyDto Delete(PathsRequest request)
    {
        var outcome = _session.Delete(request.Paths ?? [], request.Confirmation, _recycler);
        return new DeleteReplyDto(
            outcome.Results, outcome.FreedBytes, outcome.Refused, outcome.Reason, Hogs(), _session.Result?.Root.SizeBytes ?? 0);
    }

    private void OpenUninstaller(LauncherRequest request)
    {
        var node = Require(request.Path);
        var assessment = DeletionAdvisor.Assess(node.FullPath, node.SizeBytes);
        var hint = assessment.Uninstall.FirstOrDefault(h => h.Launcher == request.Launcher)
            ?? throw new InvalidOperationException("Aucun désinstalleur connu pour cet élément.");

        if (!DeletionAdvisor.IsSafeLauncherUri(hint.Uri))
        {
            throw new InvalidOperationException("Adresse de lanceur non autorisée.");
        }

        _launcher.OpenUri(hint.Uri);
    }

    private FolderNode Require(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Aucun chemin.");
        }

        return _session.Find(path) ?? throw new InvalidOperationException("Cet élément n'est pas dans l'analyse en cours.");
    }

    private static DeletionAssessment Classify(FolderNode node) =>
        node.Kind == NodeKind.OtherFiles
            ? new DeletionAssessment(node.FullPath, DeletionVerdict.Blocked, ConfirmationLevel.Standard,
                "Petits fichiers regroupés", "Seuls les plus gros fichiers de chaque dossier sont listés ; ceux-ci sont regroupés.", [])
            : DeletionAdvisor.Classify(node.FullPath, node.SizeBytes);

    private List<HogDto> Hogs()
    {
        if (_session.Result is not { } result)
        {
            return [];
        }

        return SpaceHogDetector.Find(result.Root).Select(h => new HogDto(
            h.RuleId, h.Title, h.Explanation, h.Advice, h.SizeBytes,
            h.Nodes.Take(MaxLocationsPerHog)
                .Select(n => new HogLocationDto(n.FullPath, n.SizeBytes, DeletionAdvisor.Classify(n.FullPath, n.SizeBytes).Verdict))
                .ToList(),
            Math.Max(0, h.Nodes.Count - MaxLocationsPerHog))).ToList();
    }

    /// <summary>Things the numbers alone do not explain: unreadable folders, the gap with what Windows reports, sparse and cloud files.</summary>
    private static List<string> Notes(ScanResult result)
    {
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
            notes.Add($"Fichiers creux ou compressés comptés pour leur taille réelle sur disque ({ByteSize.Format(result.SavedBySparseOrCompressedFiles)} de moins que leur taille affichée par Windows).");
        }

        if (result.CloudOnlyFiles > 0)
        {
            notes.Add($"{result.CloudOnlyFiles:N0} fichiers OneDrive en ligne uniquement ne prennent pas de place locale.");
        }

        return notes;
    }

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

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
