using Treemap.Core.Storage.Cleanup;
using Xunit;

namespace Treemap.Core.Tests;

public class FileSystemDeleterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "treemap-deleter-" + Guid.NewGuid().ToString("N"));

    public FileSystemDeleterTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Delete_RemovesAFolderIncludingReadOnlyAndHiddenFiles()
    {
        var folder = Path.Combine(_root, "game");
        Directory.CreateDirectory(Path.Combine(folder, "data"));
        var readOnly = Path.Combine(folder, "data", "locked.bin");
        var hidden = Path.Combine(folder, ".hidden");
        File.WriteAllText(readOnly, "x");
        File.WriteAllText(hidden, "x");
        File.SetAttributes(readOnly, FileAttributes.ReadOnly);
        File.SetAttributes(hidden, FileAttributes.Hidden);

        var result = new FileSystemDeleter().Delete(folder);

        Assert.True(result.Success);
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void Delete_RemovesASingleReadOnlyFile()
    {
        var file = Path.Combine(_root, "one.txt");
        File.WriteAllText(file, "x");
        File.SetAttributes(file, FileAttributes.ReadOnly);

        Assert.True(new FileSystemDeleter().Delete(file).Success);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void Delete_ReportsAFileThatIsInUse()
    {
        var file = Path.Combine(_root, "busy.txt");
        File.WriteAllText(file, "x");
        using var held = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);

        var result = new FileSystemDeleter().Delete(file);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void RecycleBin_QueryReadsTheRealBinWithoutChangingIt()
    {
        var info = new ShellRecycleBin().Query();

        Assert.True(info.SizeBytes >= 0);
        Assert.True(info.ItemCount >= 0);
    }
}
