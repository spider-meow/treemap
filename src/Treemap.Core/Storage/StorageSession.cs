using Treemap.Core.Storage.Cleanup;

namespace Treemap.Core.Storage;

public sealed record DeletionPreviewItem(
    string Path,
    string Name,
    long SizeBytes,
    long FileCount,
    bool IsFolder,
    DeletionAssessment Assessment);

public sealed record DeletionPreview(
    IReadOnlyList<DeletionPreviewItem> Items,
    long TotalBytes,
    bool CanProceed,
    string? ConfirmationPhrase);

public sealed record DeletionResult(string Path, bool Ok, string? Error);

/// <param name="Refused">Nothing was touched: the request did not pass the checks (see <paramref name="Reason"/>).</param>
public sealed record DeletionOutcome(IReadOnlyList<DeletionResult> Results, long FreedBytes, bool Refused, string? Reason);

/// <summary>
/// The scan the user is looking at, and everything that may be done to it. Deleting never trusts the caller:
/// the paths must belong to this scan, are assessed again against the disk, and the confirmation is checked here.
/// </summary>
public sealed class StorageSession
{
    private readonly object _gate = new();
    private CancellationTokenSource? _scanCancellation;

    public ScanResult? Result { get; private set; }

    public bool IsScanning => _scanCancellation is not null;

    public async Task<ScanResult> ScanAsync(string path, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        CancellationTokenSource cts;
        lock (_gate)
        {
            if (_scanCancellation is not null)
            {
                throw new InvalidOperationException("Une analyse est déjà en cours.");
            }

            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _scanCancellation = cts;
            Result = null;
        }

        try
        {
            var result = await new DiskScanner().ScanAsync(path, progress, cts.Token);
            Result = result;
            return result;
        }
        finally
        {
            lock (_gate)
            {
                _scanCancellation = null;
            }

            cts.Dispose();
        }
    }

    public void CancelScan()
    {
        lock (_gate)
        {
            _scanCancellation?.Cancel();
        }
    }

    public FolderNode? Find(string path) =>
        Result is { } result ? StorageTree.Find(result.Root, path) : null;

    /// <summary>What deleting these paths would do, with the verdict for each. Nested selections count once, under the outer folder.</summary>
    /// <param name="permanent">The deletion will skip the Recycle Bin: the typed confirmation is then always required.</param>
    public DeletionPreview Preview(IEnumerable<string> paths, bool permanent = false)
    {
        var items = new List<DeletionPreviewItem>();

        foreach (var path in Outermost(paths))
        {
            var node = Find(path);
            if (node is null || node.Kind == NodeKind.OtherFiles || node.Parent is null)
            {
                items.Add(new DeletionPreviewItem(path, System.IO.Path.GetFileName(path), 0, 0, false,
                    new DeletionAssessment(path, DeletionVerdict.Blocked, ConfirmationLevel.Standard,
                        "Hors de l'analyse", "Seuls les éléments vus dans l'analyse en cours peuvent être supprimés.", [])));
                continue;
            }

            items.Add(new DeletionPreviewItem(
                node.FullPath, node.Name, node.SizeBytes, node.FileCount, node.IsFolder,
                DeletionAdvisor.Assess(node.FullPath, node.SizeBytes)));
        }

        var canProceed = items.Count > 0 && items.All(i => i.Assessment.Verdict == DeletionVerdict.Allowed);
        var typed = items.Any(i => i.Assessment.Confirmation == ConfirmationLevel.Typed);

        return new DeletionPreview(
            items,
            items.Sum(i => i.SizeBytes),
            canProceed,
            canProceed && (typed || permanent) ? DeletionAdvisor.ConfirmationPhrase : null);
    }

    public DeletionOutcome Delete(IEnumerable<string> paths, string? confirmation, IRecycler recycler) =>
        Run(paths, confirmation, permanent: false, recycler.Recycle);

    /// <summary>
    /// Forced deletion: no Recycle Bin, no way back. Same checks as <see cref="Delete"/> (protected items stay protected),
    /// and the typed confirmation is always required.
    /// </summary>
    public DeletionOutcome DeletePermanently(IEnumerable<string> paths, string? confirmation, IPermanentDeleter deleter) =>
        Run(paths, confirmation, permanent: true, deleter.Delete);

    private DeletionOutcome Run(IEnumerable<string> paths, string? confirmation, bool permanent, Func<string, RecycleResult> remove)
    {
        var preview = Preview(paths, permanent);

        if (!preview.CanProceed)
        {
            return Refuse(preview.Items.Count == 0
                ? "Rien à supprimer."
                : "Au moins un élément ne peut pas être supprimé d'ici : rien n'a été touché.");
        }

        if (preview.ConfirmationPhrase is { } phrase
            && !phrase.Equals(confirmation?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Refuse($"Confirmation manquante : tape {phrase} pour continuer.");
        }

        var results = new List<DeletionResult>();
        long freed = 0;

        foreach (var item in preview.Items)
        {
            var node = Find(item.Path);
            if (node is null)
            {
                results.Add(new DeletionResult(item.Path, false, "L'élément n'est plus dans l'analyse."));
                continue;
            }

            var outcome = remove(node.FullPath);
            if (outcome.Success)
            {
                freed += node.SizeBytes;
                StorageTree.Remove(node);
                results.Add(new DeletionResult(item.Path, true, null));
            }
            else
            {
                results.Add(new DeletionResult(item.Path, false, outcome.Declined
                    ? "Windows a demandé confirmation (élément trop gros pour la corbeille) et la suppression a été refusée."
                    : outcome.Error));
            }
        }

        return new DeletionOutcome(results, freed, false, null);
    }

    private static DeletionOutcome Refuse(string reason) => new([], 0, true, reason);

    /// <summary>Drops duplicates and any path that lies inside another selected path.</summary>
    private static List<string> Outermost(IEnumerable<string> paths)
    {
        var trimmed = paths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => StorageTree.Trim(SafeFullPath(p)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return trimmed.Where(p => !trimmed.Any(other => StorageTree.IsInside(p, other))).ToList();
    }

    private static string SafeFullPath(string path)
    {
        try
        {
            return System.IO.Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }
}
