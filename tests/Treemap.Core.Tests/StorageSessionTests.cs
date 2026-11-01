using Treemap.Core.Storage;
using Treemap.Core.Storage.Cleanup;
using Xunit;

namespace Treemap.Core.Tests;

public class StorageSessionTests : IDisposable
{
    private sealed class FakeRecycler : IRecycler
    {
        public List<string> Recycled { get; } = [];

        public Func<string, RecycleResult> Behaviour { get; set; } = _ => new RecycleResult(true);

        public RecycleResult Recycle(string path)
        {
            Recycled.Add(path);
            return Behaviour(path);
        }
    }

    private readonly string _root;

    public StorageSessionTests()
    {
        // Under the temp folder: the advisor treats it as ordinary temporary files (standard confirmation).
        _root = Path.Combine(Path.GetTempPath(), "treemap-session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "big", "inner"));
        Directory.CreateDirectory(Path.Combine(_root, "small"));
        File.WriteAllBytes(Path.Combine(_root, "big", "inner", "a.bin"), new byte[4000]);
        File.WriteAllBytes(Path.Combine(_root, "big", "b.bin"), new byte[1000]);
        File.WriteAllBytes(Path.Combine(_root, "small", "c.bin"), new byte[200]);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private async Task<StorageSession> ScannedAsync(string? path = null)
    {
        var session = new StorageSession();
        await session.ScanAsync(path ?? _root, null, CancellationToken.None);
        return session;
    }

    [Fact]
    public async Task Preview_ReportsSizeAndVerdictOfScannedItems()
    {
        var session = await ScannedAsync();

        var preview = session.Preview([Path.Combine(_root, "big")]);

        Assert.True(preview.CanProceed);
        var item = Assert.Single(preview.Items);
        Assert.Equal(5000, item.SizeBytes);
        Assert.Equal(2, item.FileCount);
        Assert.Equal(5000, preview.TotalBytes);
        Assert.Null(preview.ConfirmationPhrase);
    }

    [Fact]
    public async Task Preview_CountsNestedSelectionsOnce()
    {
        var session = await ScannedAsync();

        var preview = session.Preview([
            Path.Combine(_root, "big", "inner"),
            Path.Combine(_root, "big"),
            Path.Combine(_root, "big") + "\\",
            Path.Combine(_root, "big", "inner", "a.bin"),
        ]);

        Assert.Equal(Path.Combine(_root, "big"), Assert.Single(preview.Items).Path, ignoreCase: true);
        Assert.Equal(5000, preview.TotalBytes);
    }

    [Fact]
    public async Task Delete_RecyclesAndUpdatesTheTree()
    {
        var session = await ScannedAsync();
        var recycler = new FakeRecycler();
        var before = session.Result!.Root.SizeBytes;

        var outcome = session.Delete([Path.Combine(_root, "big")], null, recycler);

        Assert.False(outcome.Refused);
        Assert.Equal(5000, outcome.FreedBytes);
        Assert.All(outcome.Results, r => Assert.True(r.Ok));
        Assert.Equal([Path.Combine(_root, "big")], recycler.Recycled, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(before - 5000, session.Result.Root.SizeBytes);
        Assert.Equal(1, session.Result.Root.FileCount);
        Assert.Null(session.Find(Path.Combine(_root, "big")));
        Assert.NotNull(session.Find(Path.Combine(_root, "small")));
    }

    [Fact]
    public async Task Delete_RefusesEverything_WhenOneItemIsNotAllowed()
    {
        var session = await ScannedAsync();
        var recycler = new FakeRecycler();

        var outcome = session.Delete([Path.Combine(_root, "small"), @"C:\Windows\System32"], null, recycler);

        Assert.True(outcome.Refused);
        Assert.Empty(recycler.Recycled);
        Assert.NotNull(session.Find(Path.Combine(_root, "small")));
    }

    [Fact]
    public async Task Delete_RefusesPathsOutsideTheScan_EvenIfTheyAreHarmless()
    {
        var session = await ScannedAsync(Path.Combine(_root, "small"));
        var recycler = new FakeRecycler();

        var outcome = session.Delete([Path.Combine(_root, "big")], null, recycler);

        Assert.True(outcome.Refused);
        Assert.Empty(recycler.Recycled);
    }

    [Fact]
    public async Task Delete_NeverRemovesTheRootOfTheScan()
    {
        var session = await ScannedAsync();
        var recycler = new FakeRecycler();

        var outcome = session.Delete([_root], null, recycler);

        Assert.True(outcome.Refused);
        Assert.Empty(recycler.Recycled);
    }

    [Fact]
    public async Task Delete_WithoutAScan_IsRefused()
    {
        var outcome = new StorageSession().Delete([_root], null, new FakeRecycler());

        Assert.True(outcome.Refused);
    }

    [Fact]
    public async Task Delete_KeepsTheTreeUnchanged_WhenWindowsFails()
    {
        var session = await ScannedAsync();
        var recycler = new FakeRecycler { Behaviour = _ => new RecycleResult(false, Error: "accès refusé") };
        var before = session.Result!.Root.SizeBytes;

        var outcome = session.Delete([Path.Combine(_root, "small")], null, recycler);

        Assert.False(outcome.Refused);
        Assert.Equal(0, outcome.FreedBytes);
        var failure = Assert.Single(outcome.Results);
        Assert.False(failure.Ok);
        Assert.Equal("accès refusé", failure.Error);
        Assert.Equal(before, session.Result.Root.SizeBytes);
        Assert.NotNull(session.Find(Path.Combine(_root, "small")));
    }

    [Fact]
    public async Task Delete_ReportsADeclinedWarningAsAFailure()
    {
        var session = await ScannedAsync();
        var recycler = new FakeRecycler { Behaviour = _ => new RecycleResult(false, Declined: true) };

        var outcome = session.Delete([Path.Combine(_root, "small")], null, recycler);

        Assert.False(Assert.Single(outcome.Results).Ok);
        Assert.NotNull(session.Find(Path.Combine(_root, "small")));
    }

    [Fact]
    public async Task PersonalAppData_RequiresTheTypedConfirmation()
    {
        // LocalAppData (not Temp): the advisor treats application data as needing the typed word.
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "treemap-session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(appData, "cache"));
        File.WriteAllBytes(Path.Combine(appData, "cache", "x.bin"), new byte[100]);
        try
        {
            var session = await ScannedAsync(appData);
            var target = Path.Combine(appData, "cache");
            var recycler = new FakeRecycler();

            var preview = session.Preview([target]);
            Assert.True(preview.CanProceed);
            Assert.Equal(DeletionAdvisor.ConfirmationPhrase, preview.ConfirmationPhrase);

            Assert.True(session.Delete([target], null, recycler).Refused);
            Assert.True(session.Delete([target], "oui", recycler).Refused);
            Assert.Empty(recycler.Recycled);

            var outcome = session.Delete([target], " supprimer ", recycler);
            Assert.False(outcome.Refused);
            Assert.Single(recycler.Recycled);
        }
        finally
        {
            Directory.Delete(appData, recursive: true);
        }
    }

    [Fact]
    public async Task ScanAsync_RejectsASecondScanWhileOneRuns()
    {
        var session = new StorageSession();
        using var cts = new CancellationTokenSource();
        var first = session.ScanAsync(@"C:\Windows\System32", null, cts.Token);

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ScanAsync(_root, null, CancellationToken.None));

        session.CancelScan();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.False(session.IsScanning);
    }

    [Fact]
    public async Task Tree_FindIsCaseInsensitiveAndIgnoresTrailingSlash()
    {
        var session = await ScannedAsync();

        Assert.NotNull(session.Find(Path.Combine(_root, "BIG", "INNER") + "\\"));
        Assert.Null(session.Find(Path.Combine(_root, "nope")));
        Assert.Null(session.Find(@"C:\Windows"));
    }
}
