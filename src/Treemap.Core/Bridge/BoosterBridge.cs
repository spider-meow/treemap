using System.Text.Json;
using Treemap.Core.Booster;
using Treemap.Core.Settings;

namespace Treemap.Core.Bridge;

public sealed record BoosterGameDto(int Id, string Name, string? Title, bool Known);

public sealed record BoosterAppDto(int Id, string Name, string? Title, long MemoryBytes, bool Picked);

public sealed record BoosterStateDto(
    BoosterSettings Settings,
    IReadOnlyList<BoosterGameDto> Games,
    IReadOnlyList<BoosterAppDto> Apps,
    bool SessionActive,
    BoostReport? Session,
    PowerPlan? PowerPlan,
    bool HighPerformanceAvailable);

/// <summary>The game booster messages of the web interface. All decisions are the engine's; this only carries them.</summary>
public sealed class BoosterBridge
{
    private readonly BoosterEngine _engine;
    private readonly IPowerPlans _plans;

    public BoosterBridge(BoosterEngine engine, IPowerPlans plans)
    {
        _engine = engine;
        _plans = plans;
    }

    private sealed record PidRequest(int Pid);

    public BoosterStateDto State()
    {
        var settings = _engine.Settings;
        var classified = _engine.Classified();
        var picked = settings.CloseList.Select(ProcessRules.Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var known = settings.KnownGames.Select(ProcessRules.Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var games = classified
            .Where(c => c.Role == ProcessRole.Game)
            .Select(c => new BoosterGameDto(c.Process.Id, ProcessRules.Normalize(c.Process.Name), c.Process.WindowTitle, known.Contains(ProcessRules.Normalize(c.Process.Name))))
            .ToList();

        // One line per application, however many processes: the biggest one stands for it.
        var apps = classified
            .Where(c => c.Role == ProcessRole.Closable)
            .GroupBy(c => ProcessRules.Normalize(c.Process.Name), StringComparer.OrdinalIgnoreCase)
            .Select(g => new BoosterAppDto(
                g.OrderByDescending(c => c.Process.MemoryBytes).First().Process.Id,
                g.Key,
                g.Select(c => c.Process.WindowTitle).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)),
                g.Sum(c => c.Process.MemoryBytes),
                picked.Contains(g.Key)))
            .OrderByDescending(a => a.MemoryBytes)
            .ToList();

        return new BoosterStateDto(
            settings, games, apps, _engine.IsActive, _engine.Current, _plans.Active(), _plans.Exists(PowerCfg.HighPerformanceGuid));
    }

    public void Register(BridgeRouter router)
    {
        router.Register("booster.get", () => Task.Run<object?>(State));
        router.Register("booster.update", payload =>
        {
            var settings = (payload.ValueKind == JsonValueKind.Undefined ? null : payload.Deserialize<BoosterSettings>(BridgeRouter.Options))
                ?? throw new ArgumentException("Réglages incomplets.");
            _engine.Apply(settings);
            return Task.Run<object?>(State);
        });
        router.Register("booster.start", payload => Task.Run<object?>(() =>
        {
            var request = payload.ValueKind == JsonValueKind.Undefined ? null : payload.Deserialize<PidRequest>(BridgeRouter.Options);
            _engine.Start((request ?? throw new ArgumentException("Aucun jeu choisi.")).Pid);
            return State();
        }));
        router.Register("booster.stop", () => Task.Run<object?>(() =>
        {
            _engine.Stop();
            return State();
        }));
        router.Register("booster.relaunch", () => Task.Run<object?>(() =>
        {
            _engine.Relaunch();
            return State();
        }));
    }
}
