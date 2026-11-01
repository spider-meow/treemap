namespace Treemap.Core.Storage;

/// <summary>Edits a scanned tree after something was deleted, so the view stays right without a new scan.</summary>
public static class StorageTree
{
    /// <summary>Detaches the node from its parent and takes its size and file count off every ancestor.</summary>
    public static void Remove(FolderNode node)
    {
        var parent = node.Parent ?? throw new InvalidOperationException("The root of a scan cannot be removed.");

        parent.Children = parent.Children.Where(c => !ReferenceEquals(c, node)).ToList();
        for (var ancestor = parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            ancestor.SizeBytes -= node.SizeBytes;
            ancestor.FileCount -= node.FileCount;
        }

        node.Parent = null;
    }

    /// <summary>Finds the node for a path by walking down from the root; null when the path is not part of the tree.</summary>
    public static FolderNode? Find(FolderNode root, string path)
    {
        var wanted = Trim(path);
        var current = root;

        while (true)
        {
            var currentPath = Trim(current.FullPath);
            if (currentPath.Equals(wanted, StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }

            if (!IsInside(wanted, currentPath))
            {
                return null;
            }

            var next = current.Children.FirstOrDefault(c =>
            {
                if (c.Kind == NodeKind.OtherFiles)
                {
                    return false;
                }

                var childPath = Trim(c.FullPath);
                return childPath.Equals(wanted, StringComparison.OrdinalIgnoreCase) || (c.IsFolder && IsInside(wanted, childPath));
            });

            if (next is null)
            {
                return null;
            }

            current = next;
        }
    }

    /// <summary>True when <paramref name="path"/> is strictly inside <paramref name="folder"/> (both already trimmed).</summary>
    public static bool IsInside(string path, string folder) =>
        path.Length > folder.Length
        && path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)
        && (folder.EndsWith('\\') || path[folder.Length] == '\\');

    public static string Trim(string path)
    {
        var root = Path.GetPathRoot(path) ?? string.Empty;
        return path.Length > root.Length ? path.TrimEnd('\\') : path;
    }
}
