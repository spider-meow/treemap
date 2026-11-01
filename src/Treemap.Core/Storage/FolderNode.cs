namespace Treemap.Core.Storage;

public enum NodeKind
{
    Folder,
    File,

    /// <summary>Aggregate of the smaller files of a folder, kept out of the tree to save memory.</summary>
    OtherFiles,
}

/// <summary>
/// One entry of the scanned tree. Folders keep every sub-folder but only the largest files;
/// the remaining files of a folder are folded into a single <see cref="NodeKind.OtherFiles"/> node
/// so that scanning a whole drive stays within a reasonable amount of memory.
/// </summary>
public sealed class FolderNode
{
    public FolderNode(string name, string fullPath, NodeKind kind)
    {
        Name = name;
        FullPath = fullPath;
        Kind = kind;
    }

    public string Name { get; }

    public string FullPath { get; }

    public NodeKind Kind { get; }

    public FolderNode? Parent { get; internal set; }

    /// <summary>Total size of this entry (for folders: everything below it).</summary>
    public long SizeBytes { get; internal set; }

    /// <summary>Number of files below this entry (1 for a file).</summary>
    public long FileCount { get; internal set; }

    /// <summary>Children sorted by size, largest first.</summary>
    public IReadOnlyList<FolderNode> Children { get; internal set; } = [];

    public bool IsFolder => Kind == NodeKind.Folder;

    public override string ToString() => $"{Name} ({SizeBytes} bytes)";
}
