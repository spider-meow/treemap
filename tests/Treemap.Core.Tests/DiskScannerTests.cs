using Treemap.Core.Storage;

namespace Treemap.Core.Tests;

public sealed class DiskScannerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "treemap-tests-" + Guid.NewGuid().ToString("N"));

    public DiskScannerTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    private void CreateFile(string relativePath, int size)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[size]);
    }

    [Fact]
    public async Task Scan_SumsSizesAndCountsFilesRecursively()
    {
        CreateFile("a.bin", 100);
        CreateFile("sub/b.bin", 200);
        CreateFile("sub/deep/c.bin", 300);

        var result = await new DiskScanner().ScanAsync(_root);

        Assert.Equal(600, result.Root.SizeBytes);
        Assert.Equal(3, result.FileCount);
        Assert.Equal(3, result.FolderCount); // root, sub, deep
        Assert.Equal(0, result.InaccessibleFolders);
    }

    [Fact]
    public async Task Scan_SortsChildrenLargestFirst()
    {
        CreateFile("small.bin", 10);
        CreateFile("big/huge.bin", 5000);
        CreateFile("medium.bin", 700);

        var result = await new DiskScanner().ScanAsync(_root);

        Assert.Equal(["big", "medium.bin", "small.bin"], result.Root.Children.Select(c => c.Name));
        Assert.Equal(NodeKind.Folder, result.Root.Children[0].Kind);
        Assert.Equal(NodeKind.File, result.Root.Children[1].Kind);
    }

    [Fact]
    public async Task Scan_FoldsSmallFilesIntoOtherFilesNode()
    {
        for (var i = 1; i <= 10; i++)
        {
            CreateFile($"f{i}.bin", i * 10);
        }

        var result = await new DiskScanner(new ScanOptions { MaxFilesPerFolder = 3 }).ScanAsync(_root);

        var files = result.Root.Children.Where(c => c.Kind == NodeKind.File).ToList();
        var other = Assert.Single(result.Root.Children, c => c.Kind == NodeKind.OtherFiles);

        Assert.Equal(["f10.bin", "f9.bin", "f8.bin"], files.Select(f => f.Name));
        Assert.Equal(7, other.FileCount);
        Assert.Equal(10 + 20 + 30 + 40 + 50 + 60 + 70, other.SizeBytes);
        Assert.Equal(550, result.Root.SizeBytes);
        Assert.Equal(10, result.Root.FileCount);
    }

    [Fact]
    public async Task Scan_SetsParentLinks()
    {
        CreateFile("sub/x.bin", 1);

        var result = await new DiskScanner().ScanAsync(_root);

        var sub = Assert.Single(result.Root.Children);
        Assert.Same(result.Root, sub.Parent);
        Assert.Same(sub, sub.Children[0].Parent);
    }

    [Fact]
    public async Task Scan_EmptyFolder_HasZeroSize()
    {
        var result = await new DiskScanner().ScanAsync(_root);

        Assert.Equal(0, result.Root.SizeBytes);
        Assert.Empty(result.Root.Children);
    }

    [Fact]
    public async Task Scan_CountsHiddenFiles()
    {
        CreateFile("hidden.bin", 123);
        File.SetAttributes(Path.Combine(_root, "hidden.bin"), FileAttributes.Hidden);

        var result = await new DiskScanner().ScanAsync(_root);

        Assert.Equal(123, result.Root.SizeBytes);
    }

    [Fact]
    public async Task Scan_ParallelAndSequentialGiveSameTotals()
    {
        for (var i = 0; i < 6; i++)
        {
            for (var j = 0; j < 4; j++)
            {
                CreateFile($"d{i}/e{j}/file.bin", (i + 1) * 100 + j);
            }
        }

        var sequential = await new DiskScanner(new ScanOptions { ParallelDepth = 0 }).ScanAsync(_root);
        var parallel = await new DiskScanner(new ScanOptions { ParallelDepth = 3 }).ScanAsync(_root);

        Assert.Equal(sequential.Root.SizeBytes, parallel.Root.SizeBytes);
        Assert.Equal(sequential.FileCount, parallel.FileCount);
        Assert.Equal(sequential.FolderCount, parallel.FolderCount);
    }

    [Fact]
    public async Task Scan_MissingRoot_Throws()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => new DiskScanner().ScanAsync(Path.Combine(_root, "does-not-exist")));
    }

    [Fact]
    public async Task Scan_HonoursCancellation()
    {
        CreateFile("a/b.bin", 1);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new DiskScanner().ScanAsync(_root, cancellationToken: cts.Token));
    }
}
