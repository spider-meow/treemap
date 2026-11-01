namespace Treemap.Core.Storage;

public sealed record ScanOptions
{
    /// <summary>How many of the largest files are kept as individual nodes per folder.</summary>
    public int MaxFilesPerFolder { get; init; } = 20;

    /// <summary>Folders at this depth or shallower are scanned in parallel.</summary>
    public int ParallelDepth { get; init; } = 6;

    public int MaxDegreeOfParallelism { get; init; } = Environment.ProcessorCount;
}

public readonly record struct ScanProgress(long FilesScanned, long BytesScanned, string CurrentPath);

public sealed record ScanResult(
    FolderNode Root,
    long FileCount,
    long FolderCount,
    long InaccessibleFolders,
    long CloudOnlyFiles,
    long SavedBySparseOrCompressedFiles,
    TimeSpan Elapsed);

/// <summary>
/// Walks a directory tree and builds a size tree. Sizes are file lengths, except for sparse and compressed files
/// which count for the space they really occupy; folders reached through junctions or symbolic links are skipped
/// (no double counting, no loops), and cloud-only files (e.g. OneDrive placeholders) count for zero because they
/// take no local space.
/// </summary>
public sealed class DiskScanner
{
    private const FileAttributes CloudPlaceholder =
        FileAttributes.Offline | (FileAttributes)0x40000 /* RecallOnOpen */ | (FileAttributes)0x400000 /* RecallOnDataAccess */;

    private readonly ScanOptions _options;

    public DiskScanner(ScanOptions? options = null)
    {
        _options = options ?? new ScanOptions();
    }

    public Task<ScanResult> ScanAsync(
        string rootPath,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var root = new DirectoryInfo(rootPath);
        if (!root.Exists)
        {
            throw new DirectoryNotFoundException($"Folder not found: {rootPath}");
        }

        return Task.Run(() => Scan(root, progress, cancellationToken), cancellationToken);
    }

    private ScanResult Scan(DirectoryInfo root, IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        var state = new ScanState(progress);

        var name = root.Parent is null ? root.FullName : root.Name;
        var node = ScanFolder(root, name, parent: null, depth: 0, state, ct);

        return new ScanResult(
            node,
            node.FileCount,
            Interlocked.Read(ref state.Folders),
            Interlocked.Read(ref state.Inaccessible),
            Interlocked.Read(ref state.CloudOnly),
            Interlocked.Read(ref state.Saved),
            started.Elapsed);
    }

    private FolderNode ScanFolder(
        DirectoryInfo directory,
        string name,
        FolderNode? parent,
        int depth,
        ScanState state,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Interlocked.Increment(ref state.Folders);

        var node = new FolderNode(name, directory.FullName, NodeKind.Folder) { Parent = parent };
        var subDirectories = new List<DirectoryInfo>();
        var largestFiles = new List<FolderNode>();
        long otherSize = 0;
        long otherCount = 0;
        long directSize = 0;
        long directCount = 0;

        try
        {
            // Hidden and system entries are exactly where hiberfil.sys, pagefile.sys and friends live.
            var enumeration = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = 0,
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = false,
            };

            foreach (var entry in directory.EnumerateFileSystemInfos("*", enumeration))
            {
                if (entry is DirectoryInfo sub)
                {
                    if ((sub.Attributes & FileAttributes.ReparsePoint) == 0)
                    {
                        subDirectories.Add(sub);
                    }

                    continue;
                }

                var file = (FileInfo)entry;
                long length;
                if ((file.Attributes & CloudPlaceholder) != 0)
                {
                    Interlocked.Increment(ref state.CloudOnly);
                    length = 0;
                }
                else
                {
                    var logical = SafeLength(file);
                    length = SizeOnDisk.Of(file, logical);
                    if (length < logical)
                    {
                        Interlocked.Add(ref state.Saved, logical - length);
                    }
                }

                directSize += length;
                directCount++;
                state.ReportFile(length, file.FullName);

                var fileNode = new FolderNode(file.Name, file.FullName, NodeKind.File)
                {
                    Parent = node,
                    SizeBytes = length,
                    FileCount = 1,
                };
                KeepLargest(largestFiles, fileNode, _options.MaxFilesPerFolder, ref otherSize, ref otherCount);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            Interlocked.Increment(ref state.Inaccessible);
        }

        var folderNodes = ScanSubFolders(subDirectories, node, depth, state, ct);

        var children = new List<FolderNode>(folderNodes.Count + largestFiles.Count + 1);
        children.AddRange(folderNodes);
        children.AddRange(largestFiles);
        if (otherCount > 0)
        {
            children.Add(new FolderNode($"{otherCount:N0} autres fichiers", directory.FullName, NodeKind.OtherFiles)
            {
                Parent = node,
                SizeBytes = otherSize,
                FileCount = otherCount,
            });
        }

        children.Sort(CompareBySizeDescending);
        node.Children = children;
        node.SizeBytes = directSize + folderNodes.Sum(f => f.SizeBytes);
        node.FileCount = directCount + folderNodes.Sum(f => f.FileCount);
        return node;
    }

    private List<FolderNode> ScanSubFolders(
        List<DirectoryInfo> subDirectories,
        FolderNode parent,
        int depth,
        ScanState state,
        CancellationToken ct)
    {
        if (subDirectories.Count == 0)
        {
            return [];
        }

        if (depth >= _options.ParallelDepth || subDirectories.Count == 1)
        {
            var results = new List<FolderNode>(subDirectories.Count);
            foreach (var sub in subDirectories)
            {
                results.Add(ScanFolder(sub, sub.Name, parent, depth + 1, state, ct));
            }

            return results;
        }

        var scanned = new FolderNode[subDirectories.Count];
        Parallel.For(
            0,
            subDirectories.Count,
            new ParallelOptions { MaxDegreeOfParallelism = _options.MaxDegreeOfParallelism, CancellationToken = ct },
            i => scanned[i] = ScanFolder(subDirectories[i], subDirectories[i].Name, parent, depth + 1, state, ct));
        return [.. scanned];
    }

    private static long SafeLength(FileInfo file)
    {
        try
        {
            return file.Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    /// <summary>Keeps the N largest files; anything pushed out is folded into the "other files" totals.</summary>
    private static void KeepLargest(
        List<FolderNode> largest,
        FolderNode candidate,
        int max,
        ref long otherSize,
        ref long otherCount)
    {
        if (largest.Count < max)
        {
            largest.Add(candidate);
            return;
        }

        var smallestIndex = 0;
        for (var i = 1; i < largest.Count; i++)
        {
            if (largest[i].SizeBytes < largest[smallestIndex].SizeBytes)
            {
                smallestIndex = i;
            }
        }

        var evicted = candidate;
        if (candidate.SizeBytes > largest[smallestIndex].SizeBytes)
        {
            evicted = largest[smallestIndex];
            largest[smallestIndex] = candidate;
        }

        otherSize += evicted.SizeBytes;
        otherCount++;
    }

    private static int CompareBySizeDescending(FolderNode a, FolderNode b)
    {
        var bySize = b.SizeBytes.CompareTo(a.SizeBytes);
        return bySize != 0 ? bySize : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ScanState(IProgress<ScanProgress>? progress)
    {
        public long Folders;
        public long Inaccessible;
        public long CloudOnly;
        public long Saved;
        private long _files;
        private long _bytes;

        public void ReportFile(long length, string path)
        {
            var files = Interlocked.Increment(ref _files);
            var bytes = Interlocked.Add(ref _bytes, length);
            if (progress is not null && files % 2000 == 0)
            {
                progress.Report(new ScanProgress(files, bytes, path));
            }
        }
    }
}
