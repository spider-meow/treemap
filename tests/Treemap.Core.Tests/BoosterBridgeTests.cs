using System.Text.Json;
using Treemap.Core.Booster;
using Treemap.Core.Bridge;
using Treemap.Core.Monitoring;
using Treemap.Core.Settings;
using Xunit;

namespace Treemap.Core.Tests;

public class BoosterBridgeTests
{
    private readonly BoosterTests.FakeProcesses _processes = new();
    private readonly BoosterTests.FakePlans _plans = new();
    private readonly BridgeRouter _router = new();
    private BoosterSettings _settings = new() { CloseList = ["Discord"] };
    private int _nextId = 1;

    public BoosterBridgeTests()
    {
        _processes.Running.Add(new RunningProcess(100, "eldenring", @"D:\Games\steamapps\common\ELDEN RING\eldenring.exe", "ELDEN RING", 3L << 30));
        _processes.Running.Add(new RunningProcess(200, "Discord", @"C:\Users\a\Discord\Discord.exe", "Discord", 400L << 20));
        _processes.Running.Add(new RunningProcess(201, "Discord", @"C:\Users\a\Discord\Discord.exe", "Discord", 200L << 20));
        _processes.Running.Add(new RunningProcess(202, "Obsidian", @"C:\Users\a\Obsidian\Obsidian.exe", "Notes", 900L << 20));
        _processes.Running.Add(new RunningProcess(300, "steam", @"C:\Steam\steam.exe", "Steam", 1L << 20));

        var engine = new BoosterEngine(_processes, _plans, () => _settings, change => _settings = change(_settings),
            () => new MetricsSnapshot(DateTime.Now, 10, 8L << 30, 16L << 30, 0, 0, 0, 0, 0, null));
        new BoosterBridge(engine, _plans).Register(_router);
    }

    private async Task<JsonElement> Call(string type, object? payload = null)
    {
        var message = JsonSerializer.Serialize(new { id = _nextId++, type, payload }, BridgeRouter.Options);
        return JsonDocument.Parse((await _router.HandleAsync(message))!).RootElement;
    }

    private async Task<JsonElement> Ok(string type, object? payload = null)
    {
        var reply = await Call(type, payload);
        Assert.True(reply.GetProperty("ok").GetBoolean(), reply.ToString());
        return reply.GetProperty("payload");
    }

    [Fact]
    public async Task Get_ListsGamesAndApplications_NeverProtectedOnes()
    {
        var state = await Ok("booster.get");

        Assert.Equal(["eldenring"], state.GetProperty("games").EnumerateArray().Select(g => g.GetProperty("name").GetString()));
        var apps = state.GetProperty("apps").EnumerateArray().ToList();
        Assert.Equal(["Obsidian", "Discord"], apps.Select(a => a.GetProperty("name").GetString())); // biggest first, steam absent
        Assert.Equal(600L << 20, apps[1].GetProperty("memoryBytes").GetInt64()); // both Discord processes counted once
        Assert.True(apps[1].GetProperty("picked").GetBoolean());
        Assert.False(apps[0].GetProperty("picked").GetBoolean());
        Assert.True(state.GetProperty("highPerformanceAvailable").GetBoolean());
        Assert.False(state.GetProperty("sessionActive").GetBoolean());
    }

    [Fact]
    public async Task Update_SavesTheLists_AndRefusesProtectedNames()
    {
        var state = await Ok("booster.update", new { closeApps = true, highPriority = false, highPerformancePlan = true, auto = true, closeList = new[] { "Obsidian", "steam" }, knownGames = new[] { "indie" } });

        var settings = state.GetProperty("settings");
        Assert.Equal(["Obsidian"], settings.GetProperty("closeList").EnumerateArray().Select(x => x.GetString()));
        Assert.False(settings.GetProperty("highPriority").GetBoolean());
        Assert.True(settings.GetProperty("auto").GetBoolean());
        Assert.Equal(["indie"], _settings.KnownGames);
    }

    [Fact]
    public async Task Start_ThenStop_RunsAndEndsASession()
    {
        var started = await Ok("booster.start", new { pid = 100 });

        Assert.True(started.GetProperty("sessionActive").GetBoolean());
        var closed = started.GetProperty("session").GetProperty("closedApps");
        Assert.Equal("Discord", closed[0].GetProperty("name").GetString());
        Assert.True(closed[0].GetProperty("closed").GetBoolean());

        var stopped = await Ok("booster.stop");
        Assert.False(stopped.GetProperty("sessionActive").GetBoolean());
        Assert.Equal("Équilibré", stopped.GetProperty("session").GetProperty("restoredPowerPlan").GetString());
    }

    [Fact]
    public async Task Start_OfSomethingThatIsNotAGame_IsAnError()
    {
        var reply = await Call("booster.start", new { pid = 202 });

        Assert.False(reply.GetProperty("ok").GetBoolean());
        Assert.Contains("jeu", reply.GetProperty("error").GetString());
        Assert.Empty(_processes.CloseRequests);
    }

    [Fact]
    public async Task Relaunch_StartsTheClosedApplicationsAgain()
    {
        await Ok("booster.start", new { pid = 100 });
        await Ok("booster.stop");

        var state = await Ok("booster.relaunch");

        Assert.True(state.GetProperty("session").GetProperty("closedApps")[0].GetProperty("relaunched").GetBoolean());
        Assert.Single(_processes.Started);
    }
}
