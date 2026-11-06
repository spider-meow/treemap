using Treemap.Core.Booster;
using Treemap.Core.Monitoring;
using Treemap.Core.Settings;
using Xunit;

namespace Treemap.Core.Tests;

public class BoosterTests
{
    private const string Windows = @"C:\Windows";

    private sealed class FakeProcesses : IProcessSystem
    {
        public List<RunningProcess> Running { get; } = [];

        public HashSet<int> RefuseToClose { get; } = [];

        public List<int> CloseRequests { get; } = [];

        public List<int> Raised { get; } = [];

        public List<string> Started { get; } = [];

        public bool CanRaise { get; set; } = true;

        public int CurrentProcessId => 1;

        public string WindowsDirectory => Windows;

        public IReadOnlyList<RunningProcess> List() => Running.ToList();

        public bool IsRunning(int processId) => Running.Any(p => p.Id == processId);

        public bool CloseGracefully(int processId, TimeSpan wait)
        {
            CloseRequests.Add(processId);
            if (RefuseToClose.Contains(processId))
            {
                return false;
            }

            Running.RemoveAll(p => p.Id == processId);
            return true;
        }

        public bool RaisePriority(int processId)
        {
            Raised.Add(processId);
            return CanRaise;
        }

        public bool Start(string path)
        {
            Started.Add(path);
            return true;
        }
    }

    private sealed class FakePlans : IPowerPlans
    {
        public PowerPlan? Current { get; set; } = new("381b4222-f694-41f0-9685-ff5bb260df2e", "Équilibré");

        public bool HighPerformanceExists { get; set; } = true;

        public List<string> Activated { get; } = [];

        public PowerPlan? Active() => Current;

        public bool Exists(string guid) => guid == PowerCfg.HighPerformanceGuid ? HighPerformanceExists : true;

        public bool Activate(string guid)
        {
            Activated.Add(guid);
            Current = new PowerPlan(guid, guid == PowerCfg.HighPerformanceGuid ? "Hautes performances" : "Autre");
            return true;
        }
    }

    private readonly FakeProcesses _processes = new();
    private readonly FakePlans _plans = new();
    private BoosterSettings _settings = new() { CloseList = ["Discord", "chrome"] };
    private DateTimeOffset _now = new(2026, 11, 6, 20, 0, 0, TimeSpan.Zero);
    private readonly BoosterEngine _engine;

    public BoosterTests()
    {
        _engine = new BoosterEngine(
            _processes, _plans, () => _settings, change => _settings = change(_settings),
            () => new MetricsSnapshot(DateTime.Now, 20, 12L << 30, 16L << 30, 0, 0, 0, 0, 0, null), () => _now);

        _processes.Running.Add(Game(100, "eldenring", @"D:\Games\steamapps\common\ELDEN RING\Game\eldenring.exe"));
        _processes.Running.Add(App(200, "Discord", @"C:\Users\a\AppData\Local\Discord\app\Discord.exe"));
        _processes.Running.Add(App(201, "chrome", @"C:\Program Files\Google\Chrome\chrome.exe"));
        _processes.Running.Add(App(202, "Notepad++", @"C:\Program Files\Notepad++\notepad++.exe"));
        _processes.Running.Add(App(300, "steam", @"C:\Program Files (x86)\Steam\steam.exe"));
        _processes.Running.Add(new RunningProcess(400, "svchost", @"C:\Windows\System32\svchost.exe", null, 1));
    }

    private static RunningProcess Game(int id, string name, string path) => new(id, name, path, name, 3L << 30);

    private static RunningProcess App(int id, string name, string path) => new(id, name, path, name + " - fenêtre", 500L << 20);

    // ---- rules ----

    [Fact]
    public void Rules_ClassifyGamesApplicationsAndProtectedProcesses()
    {
        ProcessRole Role(RunningProcess p) => ProcessRules.Classify(p, [], 1, Windows);

        Assert.Equal(ProcessRole.Game, Role(_processes.Running[0]));
        Assert.Equal(ProcessRole.Closable, Role(_processes.Running[1]));
        Assert.Equal(ProcessRole.Protected, Role(_processes.Running[4]));   // launcher
        Assert.Equal(ProcessRole.Protected, Role(_processes.Running[5]));   // Windows
        Assert.Equal(ProcessRole.Self, ProcessRules.Classify(App(1, "treemap.app", @"C:\x\treemap.app.exe"), [], 1, Windows));
    }

    [Fact]
    public void Rules_LeaveAlonePrograms_WithoutAWindowOrAKnownPath()
    {
        Assert.Equal(ProcessRole.Protected, ProcessRules.Classify(new RunningProcess(5, "updater", @"C:\x\updater.exe", null, 1), [], 1, Windows));
        Assert.Equal(ProcessRole.Protected, ProcessRules.Classify(new RunningProcess(6, "mystery", null, "titre", 1), [], 1, Windows));
        Assert.Equal(ProcessRole.Protected, ProcessRules.Classify(App(7, "anything", @"C:\Windows\System32\anything.exe"), [], 1, Windows));
    }

    [Fact]
    public void Rules_TreatAKnownGameAsAGame_EvenOutsideLibraries()
    {
        var indie = App(8, "MyGame", @"C:\Games\MyGame\MyGame.exe");

        Assert.Equal(ProcessRole.Closable, ProcessRules.Classify(indie, [], 1, Windows));
        Assert.Equal(ProcessRole.Game, ProcessRules.Classify(indie, ["mygame.exe"], 1, Windows));
    }

    [Theory]
    [InlineData("steam", true)]
    [InlineData("EasyAntiCheat", true)]
    [InlineData("explorer.exe", true)]
    [InlineData("Discord", false)]
    public void Rules_ProtectedNamesCanNeverBeClosed(string name, bool isProtected)
    {
        Assert.Equal(isProtected, ProcessRules.IsProtectedName(name));
    }

    // ---- a session ----

    [Fact]
    public void Start_RaisesPriority_SwitchesThePlan_AndClosesOnlyThePickedApplications()
    {
        var report = _engine.Start(100);

        Assert.True(_engine.IsActive);
        Assert.Equal("eldenring", report.GameName);
        Assert.Equal([100], _processes.Raised);
        Assert.Equal([PowerCfg.HighPerformanceGuid], _plans.Activated);
        Assert.Equal("381b4222-f694-41f0-9685-ff5bb260df2e", _settings.PendingPowerPlan);
        Assert.Equal([200, 201], _processes.CloseRequests);
        Assert.DoesNotContain(202, _processes.CloseRequests); // not on the list
        Assert.Equal(["Discord", "chrome"], report.ClosedApps.Select(a => a.Name));
        Assert.All(report.ClosedApps, a => Assert.True(a.Closed));
        Assert.All(report.Steps, s => Assert.True(s.Done, s.Detail));
        Assert.Equal(12L << 30, report.Before.RamUsedBytes);
    }

    [Fact]
    public void Start_NeverClosesProtectedProcesses_EvenIfTheyAreOnTheList()
    {
        _settings = _settings with { CloseList = ["steam", "svchost", "Discord"] };

        _engine.Start(100);

        Assert.Equal([200], _processes.CloseRequests);
    }

    [Fact]
    public void Start_ReportsAnApplicationThatRefusesToClose_WithoutKillingIt()
    {
        _processes.RefuseToClose.Add(200);

        var report = _engine.Start(100);

        var discord = report.ClosedApps.Single(a => a.Name == "Discord");
        Assert.False(discord.Closed);
        Assert.Contains(_processes.Running, p => p.Id == 200);
        Assert.Contains("1 sur 2", report.Steps.Single(s => s.Id == "close").Detail);
    }

    [Fact]
    public void Start_DoesOnlyWhatTheSettingsAllow()
    {
        _settings = _settings with { CloseApps = false, HighPriority = false, HighPerformancePlan = false };

        var report = _engine.Start(100);

        Assert.Empty(_processes.Raised);
        Assert.Empty(_plans.Activated);
        Assert.Empty(_processes.CloseRequests);
        Assert.All(report.Steps, s => Assert.False(s.Done));
    }

    [Fact]
    public void Start_RefusesAnythingThatIsNotAGame_OrASecondSession()
    {
        Assert.Throws<InvalidOperationException>(() => _engine.Start(202));
        Assert.Throws<InvalidOperationException>(() => _engine.Start(300));
        Assert.Throws<InvalidOperationException>(() => _engine.Start(9999));

        _engine.Start(100);
        Assert.Throws<InvalidOperationException>(() => _engine.Start(100));
    }

    [Fact]
    public void Start_ReportsAMissingHighPerformancePlan_AndKeepsTheCurrentOne()
    {
        _plans.HighPerformanceExists = false;

        var report = _engine.Start(100);

        Assert.False(report.Steps.Single(s => s.Id == "power").Done);
        Assert.Empty(_plans.Activated);
        Assert.Null(_settings.PendingPowerPlan);
    }

    [Fact]
    public void Start_DoesNotTouchThePlan_WhenHighPerformanceIsAlreadyActive()
    {
        _plans.Current = new PowerPlan(PowerCfg.HighPerformanceGuid, "Hautes performances");

        _engine.Start(100);
        _engine.Stop();

        Assert.Empty(_plans.Activated);
    }

    [Fact]
    public void Stop_PutsThePreviousPlanBack_AndClearsThePendingMarker()
    {
        _engine.Start(100);

        _engine.Stop();

        Assert.False(_engine.IsActive);
        Assert.Equal("381b4222-f694-41f0-9685-ff5bb260df2e", _plans.Current!.Guid);
        Assert.Null(_settings.PendingPowerPlan);
        Assert.Equal("Équilibré", _engine.Current!.RestoredPowerPlan);
        Assert.NotNull(_engine.Current.EndedAt);
    }

    [Fact]
    public void Stop_LeavesThePlanAlone_WhenTheUserChoseAnotherOneMeanwhile()
    {
        _engine.Start(100);
        _plans.Current = new PowerPlan("a1841308-3541-4fab-bc81-f71556f20b4a", "Économie d'énergie");

        _engine.Stop();

        Assert.Equal("a1841308-3541-4fab-bc81-f71556f20b4a", _plans.Current.Guid);
        Assert.Null(_engine.Current!.RestoredPowerPlan);
    }

    [Fact]
    public void Tick_EndsTheSessionWhenTheGameCloses()
    {
        _engine.Start(100);
        _processes.Running.RemoveAll(p => p.Id == 100);

        _engine.Tick();

        Assert.False(_engine.IsActive);
        Assert.Equal("381b4222-f694-41f0-9685-ff5bb260df2e", _plans.Current!.Guid);
    }

    [Fact]
    public void Tick_MeasuresTheEffectOnlyAfterTheDelay()
    {
        _engine.Start(100);

        _engine.Tick();
        Assert.Null(_engine.Current!.After);

        _now += BoosterEngine.MeasureDelay + TimeSpan.FromSeconds(1);
        _engine.Tick();

        Assert.NotNull(_engine.Current!.After);
        Assert.True(_engine.Current.After!.ProcessCount < _engine.Current.Before.ProcessCount); // two applications closed
    }

    // ---- automatic mode ----

    [Fact]
    public void Tick_StartsASessionByItself_OnlyWhenAutoIsOn()
    {
        _engine.Tick();
        Assert.False(_engine.IsActive);

        _settings = _settings with { Auto = true };
        _engine.Tick();

        Assert.True(_engine.IsActive);
    }

    [Fact]
    public void Tick_DoesNotRestartTheSameGame_AfterTheUserStoppedTheSession()
    {
        _settings = _settings with { Auto = true };
        _engine.Tick();
        _engine.Stop();

        _engine.Tick();

        Assert.False(_engine.IsActive);
    }

    // ---- after the game ----

    [Fact]
    public void Relaunch_StartsAgainWhatWasClosed_OnceAndOnlyWhenPossible()
    {
        _processes.Running.Add(App(203, "Discord", @"C:\Users\a\AppData\Local\Discord\app\Discord.exe")); // same app: second window
        _settings = _settings with { CloseList = ["Discord", "chrome", "Spotify"] };
        _processes.Running.Add(App(204, "Spotify", @"C:\Program Files\WindowsApps\Spotify_1.0\Spotify.exe"));
        _engine.Start(100);
        _engine.Stop();

        _engine.Relaunch();
        _engine.Relaunch();

        Assert.Single(_processes.Started, p => p.EndsWith("Discord.exe"));
        Assert.Single(_processes.Started, p => p.EndsWith("chrome.exe"));
        Assert.DoesNotContain(_processes.Started, p => p.Contains("WindowsApps")); // cannot be restarted from a path
        Assert.True(_engine.Current!.ClosedApps.Single(a => a.Name == "chrome").Relaunched);
        Assert.False(_engine.Current.ClosedApps.Single(a => a.Name == "Spotify").Relaunchable);
    }

    // ---- settings ----

    [Fact]
    public void Apply_DropsProtectedNames_AndKeepsTheEngineOwnedMarker()
    {
        _settings = _settings with { PendingPowerPlan = "381b4222-f694-41f0-9685-ff5bb260df2e" };

        var saved = _engine.Apply(new BoosterSettings { CloseList = ["steam", "Discord", " ", "discord"], PendingPowerPlan = null });

        Assert.Equal(["Discord"], saved.CloseList);
        Assert.Equal("381b4222-f694-41f0-9685-ff5bb260df2e", saved.PendingPowerPlan);
    }

    [Fact]
    public void RestorePending_PutsThePlanBackAfterACrash()
    {
        _plans.Current = new PowerPlan(PowerCfg.HighPerformanceGuid, "Hautes performances");
        _settings = _settings with { PendingPowerPlan = "381b4222-f694-41f0-9685-ff5bb260df2e" };

        _engine.RestorePending();

        Assert.Equal("381b4222-f694-41f0-9685-ff5bb260df2e", _plans.Current.Guid);
        Assert.Null(_settings.PendingPowerPlan);
    }

    [Fact]
    public void RestorePending_DoesNothingIfThePlanWasChangedByTheUser()
    {
        _settings = _settings with { PendingPowerPlan = "381b4222-f694-41f0-9685-ff5bb260df2e" };

        _engine.RestorePending();

        Assert.Empty(_plans.Activated);
        Assert.Null(_settings.PendingPowerPlan);
    }

    [Theory]
    [InlineData("Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)", "381b4222-f694-41f0-9685-ff5bb260df2e", "Balanced")]
    [InlineData("GUID du modèle de gestion de l'alimentation : 8C5E7FDA-E8BF-4A96-9A85-A6E23A8C635C  (Hautes performances)", "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", "Hautes performances")]
    public void PowerCfg_ParsesTheActivePlanInAnyLanguage(string output, string guid, string name)
    {
        Assert.Equal(new PowerPlan(guid, name), PowerCfg.ParsePlan(output));
        Assert.Null(PowerCfg.ParsePlan("rien d'utile"));
    }

    [Fact]
    public void SettingsStore_UpdateKeepsTheOtherSection_AndCleansTheLists()
    {
        var path = Path.Combine(Path.GetTempPath(), "treemap-booster-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new SettingsStore(path);
            store.Update(s => s with { Overlay = s.Overlay with { Corner = OverlayCorner.BottomRight } });
            store.Update(s => s with { Booster = s.Booster with { CloseList = ["Discord", "discord", " "] } });

            var loaded = store.Load();

            Assert.Equal(OverlayCorner.BottomRight, loaded.Overlay.Corner);
            Assert.Equal(["Discord"], loaded.Booster.CloseList);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WindowsProcessSystem_ListsRealProcessesIncludingThisOne()
    {
        var list = new WindowsProcessSystem().List();

        Assert.Contains(list, p => p.Id == Environment.ProcessId);
        Assert.True(new WindowsProcessSystem().IsRunning(Environment.ProcessId));
    }
}
