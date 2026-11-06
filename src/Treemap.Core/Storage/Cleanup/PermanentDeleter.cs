namespace Treemap.Core.Storage.Cleanup;

/// <summary>Deletes one file or folder for good, without the Recycle Bin. Only ever used when the user explicitly forces it.</summary>
public interface IPermanentDeleter
{
    RecycleResult Delete(string path);
}

/// <summary>
/// Plain file-system deletion. Read-only attributes (common in node_modules and game folders) are cleared first,
/// and links inside the folder are removed as links, never followed.
/// </summary>
public sealed class FileSystemDeleter : IPermanentDeleter
{
    public RecycleResult Delete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                ClearReadOnly(path);
                Directory.Delete(path, recursive: true);
            }
            else
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }

            return new RecycleResult(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new RecycleResult(false, Error: $"Suppression impossible : {ex.Message} (un programme l'utilise peut-être, ou il faut les droits administrateur).");
        }
    }

    private static void ClearReadOnly(string folder)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = true,
        };

        foreach (var entry in Directory.EnumerateFileSystemEntries(folder, "*", options))
        {
            var attributes = File.GetAttributes(entry);
            if (attributes.HasFlag(FileAttributes.ReadOnly))
            {
                File.SetAttributes(entry, attributes & ~FileAttributes.ReadOnly);
            }
        }
    }
}
