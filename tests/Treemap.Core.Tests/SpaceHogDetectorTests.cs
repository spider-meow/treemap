using Treemap.Core.Storage;

namespace Treemap.Core.Tests;

public sealed class SpaceHogDetectorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "treemap-hogs-" + Guid.NewGuid().ToString("N"));

    public SpaceHogDetectorTests()
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

    private async Task<IReadOnlyList<SpaceHog>> DetectAsync()
    {
        var result = await new DiskScanner().ScanAsync(_root);
        return SpaceHogDetector.Find(result.Root);
    }

    [Fact]
    public async Task FindsSystemFilesByName()
    {
        CreateFile("hiberfil.sys", 5000);
        CreateFile("PageFile.sys", 3000);
        CreateFile("notes.txt", 10);

        var hogs = await DetectAsync();

        Assert.Equal(["hibernation", "pagefile"], hogs.Select(h => h.RuleId));
        Assert.Equal(5000, hogs[0].SizeBytes);
    }

    [Fact]
    public async Task GroupsNodeModulesAcrossProjects()
    {
        CreateFile("projA/node_modules/lib/index.js", 400);
        CreateFile("projB/node_modules/other/index.js", 600);
        CreateFile("projB/src/app.js", 50);

        var hog = Assert.Single(await DetectAsync());

        Assert.Equal("node-modules", hog.RuleId);
        Assert.Equal(1000, hog.SizeBytes);
        Assert.Equal(2, hog.Nodes.Count);
        Assert.Equal(600, hog.Nodes[0].SizeBytes); // largest first
    }

    [Fact]
    public async Task DoesNotCountNestedNodeModulesTwice()
    {
        CreateFile("proj/node_modules/a/node_modules/b/index.js", 700);

        var hog = Assert.Single(await DetectAsync());

        Assert.Equal(700, hog.SizeBytes);
        Assert.Single(hog.Nodes);
    }

    [Fact]
    public async Task FindsVirtualDisksByExtension()
    {
        CreateFile("wsl/ext4.vhdx", 9000);
        CreateFile("vm/disk.VMDK", 1000);

        var hog = Assert.Single(await DetectAsync());

        Assert.Equal("virtual-disk", hog.RuleId);
        Assert.Equal(10000, hog.SizeBytes);
    }

    [Fact]
    public async Task FindsWindowsFoldersByPath()
    {
        CreateFile(@"Windows\WinSxS\component.dll", 800);
        CreateFile(@"Windows\SoftwareDistribution\Download\update.cab", 300);
        CreateFile(@"$Recycle.Bin\S-1\old.bin", 200);

        var hogs = await DetectAsync();

        Assert.Equal(["winsxs", "windows-update-cache", "recycle-bin"], hogs.Select(h => h.RuleId));
    }

    [Fact]
    public async Task ReturnsHogsLargestFirst()
    {
        CreateFile("small/node_modules/x.js", 10);
        CreateFile("hiberfil.sys", 10_000);

        var hogs = await DetectAsync();

        Assert.Equal(["hibernation", "node-modules"], hogs.Select(h => h.RuleId));
    }

    [Fact]
    public async Task ReturnsNothingForOrdinaryTree()
    {
        CreateFile("docs/report.docx", 100);
        CreateFile("photos/a.jpg", 200);

        Assert.Empty(await DetectAsync());
    }

    [Fact]
    public async Task EveryHogHasHumanReadableText()
    {
        CreateFile("hiberfil.sys", 1);
        CreateFile("a/node_modules/x.js", 1);

        foreach (var hog in await DetectAsync())
        {
            Assert.False(string.IsNullOrWhiteSpace(hog.Title));
            Assert.False(string.IsNullOrWhiteSpace(hog.Explanation));
            Assert.False(string.IsNullOrWhiteSpace(hog.Advice));
        }
    }
}
