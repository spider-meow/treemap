using System.Text.Json;
using Treemap.Core.Bridge;
using Treemap.Core.Storage.Cleanup;
using Treemap.Core.Storage;
using Xunit;

namespace Treemap.Core.Tests;

public class StorageBridgeTests : IDisposable
{
    private sealed class FakeRecycler : IRecycler
    {
        public List<string> Recycled { get; } = [];

        public RecycleResult Recycle(string path)
        {
            Recycled.Add(path);
            return new RecycleResult(true);
        }
    }

    private sealed class FakeDeleter : IPermanentDeleter
    {
        public List<string> Deleted { get; } = [];

        public RecycleResult Delete(string path)
        {
            Deleted.Add(path);
            return new RecycleResult(true);
        }
    }

    private sealed class FakeBin : IRecycleBin
    {
        public int Emptied { get; private set; }

        public string? EmptyError { get; set; }

        public RecycleBinInfo Query() => new(1234, 5);

        public string? Empty()
        {
            Emptied++;
            return EmptyError;
        }
    }

    private sealed class FakeLauncher : IShellLauncher
    {
        public List<string> Revealed { get; } = [];

        public List<string> Opened { get; } = [];

        public void Reveal(string path) => Revealed.Add(path);

        public void OpenUri(string uri) => Opened.Add(uri);

        public int RecycleBinOpened { get; private set; }

        public void OpenRecycleBin() => RecycleBinOpened++;
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), "treemap-bridge-" + Guid.NewGuid().ToString("N"));
    private readonly FakeRecycler _recycler = new();
    private readonly FakeLauncher _launcher = new();
    private readonly FakeDeleter _deleter = new();
    private readonly FakeBin _bin = new();
    private readonly List<(string Type, object? Payload)> _events = [];
    private readonly BridgeRouter _router = new();
    private int _nextId = 1;

    public StorageBridgeTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "docs"));
        Directory.CreateDirectory(Path.Combine(_root, "node_modules"));
        File.WriteAllBytes(Path.Combine(_root, "docs", "a.bin"), new byte[3000]);
        File.WriteAllBytes(Path.Combine(_root, "node_modules", "b.bin"), new byte[500]);
        File.WriteAllBytes(Path.Combine(_root, "top.bin"), new byte[100]);

        new StorageBridge(new StorageSession(), _recycler, _deleter, _bin, _launcher, (type, payload) => _events.Add((type, payload)))
            .Register(_router);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private async Task<JsonElement> Call(string type, object? payload = null)
    {
        var message = JsonSerializer.Serialize(new { id = _nextId++, type, payload }, BridgeRouter.Options);
        var reply = await _router.HandleAsync(message);
        return JsonDocument.Parse(reply!).RootElement;
    }

    private async Task<JsonElement> Ok(string type, object? payload = null)
    {
        var reply = await Call(type, payload);
        Assert.True(reply.GetProperty("ok").GetBoolean(), reply.ToString());
        return reply.GetProperty("payload");
    }

    private async Task Scan() => await Ok("storage.scan", new { path = _root });

    [Fact]
    public async Task Drives_ListsTheFixedDrivesOfThisMachine()
    {
        var drives = await Ok("storage.drives");

        Assert.True(drives.GetArrayLength() >= 1);
        Assert.True(drives[0].GetProperty("totalBytes").GetInt64() > 0);
    }

    [Fact]
    public async Task Scan_ReturnsSummaryAndSpaceHogs()
    {
        var reply = await Ok("storage.scan", new { path = _root });

        Assert.False(reply.GetProperty("cancelled").GetBoolean());
        var summary = reply.GetProperty("summary");
        Assert.Equal(3600, summary.GetProperty("sizeBytes").GetInt64());
        Assert.Equal(3, summary.GetProperty("fileCount").GetInt64());
        var hog = Assert.Single(reply.GetProperty("hogs").EnumerateArray());
        Assert.Equal("node-modules", hog.GetProperty("ruleId").GetString());
    }

    [Fact]
    public async Task Scan_OfAMissingFolder_RepliesWithAnError()
    {
        var reply = await Call("storage.scan", new { path = Path.Combine(_root, "nope") });

        Assert.False(reply.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Node_ListsChildrenWithTheirVerdict()
    {
        await Scan();

        var node = await Ok("storage.node", new { path = _root });

        Assert.Equal(3600, node.GetProperty("sizeBytes").GetInt64());
        Assert.Equal(JsonValueKind.Null, node.GetProperty("parentPath").ValueKind);
        var children = node.GetProperty("children").EnumerateArray().ToList();
        Assert.Equal(3, children.Count);
        Assert.Equal("docs", children[0].GetProperty("name").GetString());
        Assert.Equal("folder", children[0].GetProperty("kind").GetString());
        Assert.Equal("allowed", children[0].GetProperty("verdict").GetString());
    }

    [Fact]
    public async Task Node_OutsideTheScan_RepliesWithAnError()
    {
        await Scan();

        Assert.False((await Call("storage.node", new { path = @"C:\Windows" })).GetProperty("ok").GetBoolean());
        Assert.False((await Call("storage.node")).GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Preview_ThenDelete_RecyclesAndReturnsFreshHogs()
    {
        await Scan();
        var target = Path.Combine(_root, "node_modules");

        var preview = await Ok("storage.preview", new { paths = new[] { target } });
        Assert.True(preview.GetProperty("canProceed").GetBoolean());
        Assert.Equal(500, preview.GetProperty("totalBytes").GetInt64());

        var reply = await Ok("storage.delete", new { paths = new[] { target } });
        Assert.False(reply.GetProperty("refused").GetBoolean());
        Assert.Equal(500, reply.GetProperty("freedBytes").GetInt64());
        Assert.Equal(3100, reply.GetProperty("rootSizeBytes").GetInt64());
        Assert.Equal(0, reply.GetProperty("hogs").GetArrayLength());
        Assert.Equal([target], _recycler.Recycled, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_OfAPathOutsideTheScan_IsRefusedAndNothingIsRecycled()
    {
        await Scan();

        var reply = await Ok("storage.delete", new { paths = new[] { @"C:\Windows\System32" } });

        Assert.True(reply.GetProperty("refused").GetBoolean());
        Assert.Empty(_recycler.Recycled);
    }

    [Fact]
    public async Task Reveal_OnlyWorksForItemsOfTheScan()
    {
        await Scan();

        await Ok("storage.reveal", new { path = Path.Combine(_root, "docs") });
        var outside = await Call("storage.reveal", new { path = @"C:\Windows" });

        Assert.Equal([Path.Combine(_root, "docs")], _launcher.Revealed, StringComparer.OrdinalIgnoreCase);
        Assert.False(outside.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task OpenUninstaller_StartsTheLauncherOfAnInstalledGame()
    {
        var steamapps = Path.Combine(_root, "steamapps");
        Directory.CreateDirectory(Path.Combine(steamapps, "common", "Hades"));
        File.WriteAllBytes(Path.Combine(steamapps, "common", "Hades", "game.bin"), new byte[10]);
        File.WriteAllText(Path.Combine(steamapps, "appmanifest_1145360.acf"), "\"appid\"\t\t\"1145360\"\n\"installdir\"\t\t\"Hades\"");
        await Scan();
        var game = Path.Combine(steamapps, "common", "Hades");

        var preview = await Ok("storage.preview", new { paths = new[] { game } });
        Assert.False(preview.GetProperty("canProceed").GetBoolean());
        Assert.Equal("uninstallOnly", preview.GetProperty("items")[0].GetProperty("assessment").GetProperty("verdict").GetString());

        await Ok("storage.openUninstaller", new { path = game, launcher = "steam" });
        Assert.Equal(["steam://uninstall/1145360"], _launcher.Opened);

        var deleted = await Ok("storage.delete", new { paths = new[] { game } });
        Assert.True(deleted.GetProperty("refused").GetBoolean());
        Assert.Empty(_recycler.Recycled);
    }

    [Fact]
    public async Task PermanentDelete_AlwaysNeedsTheTypedWord_AndSkipsTheRecycleBin()
    {
        await Scan();
        var target = Path.Combine(_root, "docs");

        var preview = await Ok("storage.preview", new { paths = new[] { target }, permanent = true });
        Assert.Equal("SUPPRIMER", preview.GetProperty("confirmationPhrase").GetString());
        var plain = await Ok("storage.preview", new { paths = new[] { target } });
        Assert.Equal(JsonValueKind.Null, plain.GetProperty("confirmationPhrase").ValueKind);

        var refused = await Ok("storage.delete", new { paths = new[] { target }, permanent = true });
        Assert.True(refused.GetProperty("refused").GetBoolean());
        Assert.Empty(_deleter.Deleted);

        var done = await Ok("storage.delete", new { paths = new[] { target }, permanent = true, confirmation = "supprimer" });
        Assert.False(done.GetProperty("refused").GetBoolean());
        Assert.Equal([target], _deleter.Deleted, StringComparer.OrdinalIgnoreCase);
        Assert.Empty(_recycler.Recycled);
        Assert.Equal(600, done.GetProperty("rootSizeBytes").GetInt64());
    }

    [Fact]
    public async Task PermanentDelete_DoesNotLiftTheProtections()
    {
        var steamapps = Path.Combine(_root, "steamapps", "common", "Hades");
        Directory.CreateDirectory(steamapps);
        File.WriteAllBytes(Path.Combine(steamapps, "game.bin"), new byte[10]);
        await Scan();

        var reply = await Ok("storage.delete", new { paths = new[] { steamapps }, permanent = true, confirmation = "SUPPRIMER" });

        Assert.True(reply.GetProperty("refused").GetBoolean());
        Assert.Empty(_deleter.Deleted);

        var outside = await Ok("storage.delete", new { paths = new[] { @"C:\Windows\System32" }, permanent = true, confirmation = "SUPPRIMER" });
        Assert.True(outside.GetProperty("refused").GetBoolean());
    }

    [Fact]
    public async Task RecycleBin_ReportsItsSize_AndOnlyEmptiesWithTheTypedWord()
    {
        var info = await Ok("storage.recycleBin");
        Assert.Equal(1234, info.GetProperty("sizeBytes").GetInt64());
        Assert.Equal(5, info.GetProperty("itemCount").GetInt64());

        Assert.False((await Call("storage.emptyRecycleBin")).GetProperty("ok").GetBoolean());
        Assert.False((await Call("storage.emptyRecycleBin", new { confirmation = "oui" })).GetProperty("ok").GetBoolean());
        Assert.Equal(0, _bin.Emptied);

        await Ok("storage.emptyRecycleBin", new { confirmation = " supprimer " });
        Assert.Equal(1, _bin.Emptied);
    }

    [Fact]
    public async Task EmptyRecycleBin_ReportsWindowsFailures()
    {
        _bin.EmptyError = "Windows n'a pas pu vider la Corbeille.";

        var reply = await Call("storage.emptyRecycleBin", new { confirmation = "SUPPRIMER" });

        Assert.False(reply.GetProperty("ok").GetBoolean());
        Assert.Contains("Corbeille", reply.GetProperty("error").GetString());
    }

    [Fact]
    public async Task OpenRecycleBin_AsksTheLauncher()
    {
        await Ok("storage.openRecycleBin");

        Assert.Equal(1, _launcher.RecycleBinOpened);
    }

    [Fact]
    public async Task OpenUninstaller_RefusesUnknownLaunchersAndItemsWithoutOne()
    {
        await Scan();

        var plain = await Call("storage.openUninstaller", new { path = Path.Combine(_root, "docs"), launcher = "steam" });
        var inventedLauncher = await Call("storage.openUninstaller", new { path = Path.Combine(_root, "docs"), launcher = "calc" });

        Assert.False(plain.GetProperty("ok").GetBoolean());
        Assert.False(inventedLauncher.GetProperty("ok").GetBoolean());
        Assert.Empty(_launcher.Opened);
    }
}
