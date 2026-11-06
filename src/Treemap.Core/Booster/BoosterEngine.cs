using Treemap.Core.Monitoring;
using Treemap.Core.Settings;

namespace Treemap.Core.Booster;

public sealed record BoostSnapshot(long RamUsedBytes, int ProcessCount, double CpuPercent);

public sealed record BoostStep(string Id, string Title, bool Done, string Detail);

/// <param name="Closed">The application agreed to close.</param>
/// <param name="Relaunchable">Its executable is known, so it can be started again after the game.</param>
public sealed record ClosedApp(string Name, string? Path, bool Closed, bool Relaunchable, bool Relaunched);

public sealed record BoostReport(
    string GameName,
    int GameProcessId,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    BoostSnapshot Before,
    BoostSnapshot? After,
    IReadOnlyList<BoostStep> Steps,
    IReadOnlyList<ClosedApp> ClosedApps,
    string? RestoredPowerPlan);

/// <summary>
/// The game booster: when a game starts it may raise the game's priority, switch to the High performance power plan and
/// ask the applications the user picked to close; when the game ends it puts the power plan back. Every action is optional,
/// nothing is forced (no process is ever killed), and the gain is measured rather than promised.
/// </summary>
public sealed class BoosterEngine
{
    public static readonly TimeSpan MeasureDelay = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan CloseWait = TimeSpan.FromSeconds(4);

    private readonly IProcessSystem _processes;
    private readonly IPowerPlans _plans;
    private readonly Func<BoosterSettings> _settings;
    private readonly Action<Func<BoosterSettings, BoosterSettings>> _update;
    private readonly Func<MetricsSnapshot?> _metrics;
    private readonly Func<DateTimeOffset> _clock;
    private readonly object _gate = new();
    private readonly HashSet<int> _dismissed = [];
    private bool _seenInitialProcesses;
    private PowerPlan? _previousPlan;

    public BoosterEngine(
        IProcessSystem processes,
        IPowerPlans plans,
        Func<BoosterSettings> settings,
        Action<Func<BoosterSettings, BoosterSettings>> update,
        Func<MetricsSnapshot?> metrics,
        Func<DateTimeOffset>? clock = null)
    {
        _processes = processes;
        _plans = plans;
        _settings = settings;
        _update = update;
        _metrics = metrics;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public event EventHandler? Changed;

    /// <summary>The running session, or the report of the last one that ended.</summary>
    public BoostReport? Current { get; private set; }

    public bool IsActive => Current is { EndedAt: null };

    public BoosterSettings Settings => _settings();

    public IReadOnlyList<(RunningProcess Process, ProcessRole Role)> Classified()
    {
        var known = _settings().KnownGames;
        return _processes.List()
            .Select(p => (Process: p, Role: ProcessRules.Classify(p, known, _processes.CurrentProcessId, _processes.WindowsDirectory)))
            .ToList();
    }

    /// <summary>Applies new settings. Protected names can never be on the close list, whatever was sent.</summary>
    public BoosterSettings Apply(BoosterSettings requested)
    {
        var safe = requested.Normalized() with
        {
            PendingPowerPlan = _settings().PendingPowerPlan, // owned by the engine, not by the interface
        };
        safe = safe with { CloseList = safe.CloseList.Where(n => !ProcessRules.IsProtectedName(n)).ToArray() };
        _update(_ => safe);
        Changed?.Invoke(this, EventArgs.Empty);
        return safe;
    }

    /// <summary>Puts the power plan back if an earlier run stopped in the middle of a session.</summary>
    public void RestorePending()
    {
        var pending = _settings().PendingPowerPlan;
        if (pending is null || IsActive)
        {
            return;
        }

        if (_plans.Active()?.Guid == PowerCfg.HighPerformanceGuid && _plans.Exists(pending))
        {
            _plans.Activate(pending);
        }

        _update(s => s with { PendingPowerPlan = null });
    }

    public BoostReport Start(int gameProcessId)
    {
        BoostReport report;
        lock (_gate)
        {
            if (IsActive)
            {
                throw new InvalidOperationException("Une session est déjà en cours.");
            }

            var game = Classified().FirstOrDefault(c => c.Process.Id == gameProcessId);
            if (game.Process is null || game.Role != ProcessRole.Game)
            {
                throw new InvalidOperationException("Ce programme n'est pas un jeu connu : ajoute-le d'abord à la liste des jeux.");
            }

            var settings = _settings();
            var before = Snapshot();
            var steps = new List<BoostStep>();
            var closed = new List<ClosedApp>();

            steps.Add(settings.HighPriority ? PriorityStep(game.Process) : new BoostStep("priority", "Priorité du jeu", false, "Désactivé dans les réglages."));
            steps.Add(settings.HighPerformancePlan ? PowerStep() : new BoostStep("power", "Plan d'alimentation", false, "Désactivé dans les réglages."));

            if (settings.CloseApps)
            {
                CloseApps(settings, closed);
                var done = closed.Count(c => c.Closed);
                steps.Add(new BoostStep("close", "Applications fermées", done > 0 || closed.Count == 0,
                    closed.Count == 0 ? "Aucune application de la liste n'est ouverte."
                        : $"{done} sur {closed.Count} fermées{(done < closed.Count ? " ; les autres ont refusé (travail non enregistré ?)." : ".")}"));
            }
            else
            {
                steps.Add(new BoostStep("close", "Applications fermées", false, "Désactivé dans les réglages."));
            }

            report = new BoostReport(
                ProcessRules.Normalize(game.Process.Name), game.Process.Id, _clock(), null, before, null, steps, closed, null);
            Current = report;
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return report;
    }

    /// <summary>Ends the session now: the power plan goes back, the closed applications stay closed until relaunched.</summary>
    public void Stop()
    {
        BoostReport? ended;
        lock (_gate)
        {
            if (Current is not { EndedAt: null } report)
            {
                return;
            }

            _dismissed.Add(report.GameProcessId);
            string? restored = null;
            if (_previousPlan is { } previous)
            {
                // Only put it back if the plan is still the one we set: the user may have chosen another meanwhile.
                if (_plans.Active()?.Guid == PowerCfg.HighPerformanceGuid && _plans.Activate(previous.Guid))
                {
                    restored = previous.Name;
                }

                _previousPlan = null;
                _update(s => s with { PendingPowerPlan = null });
            }

            ended = report with { EndedAt = _clock(), RestoredPowerPlan = restored };
            Current = ended;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Called every few seconds: notices the game closing, takes the "after" measure, and starts automatic sessions.</summary>
    public void Tick()
    {
        var changed = false;
        BoostReport? current;
        lock (_gate)
        {
            current = Current;
        }

        if (current is { EndedAt: null })
        {
            if (!_processes.IsRunning(current.GameProcessId))
            {
                Stop();
                return;
            }

            if (current.After is null && _clock() - current.StartedAt >= MeasureDelay)
            {
                lock (_gate)
                {
                    if (Current is { EndedAt: null, After: null } active)
                    {
                        Current = active with { After = Snapshot() };
                        changed = true;
                    }
                }
            }
        }
        else if (!_seenInitialProcesses)
        {
            // Games already open when the app starts are left to the manual button: automatic mode is for games opened later.
            _seenInitialProcesses = true;
            foreach (var running in Classified().Where(c => c.Role == ProcessRole.Game))
            {
                _dismissed.Add(running.Process.Id);
            }
        }
        else if (_settings().Auto)
        {
            var game = Classified().FirstOrDefault(c => c.Role == ProcessRole.Game && c.Process.HasWindow && !_dismissed.Contains(c.Process.Id));
            if (game.Process is not null)
            {
                try
                {
                    Start(game.Process.Id);
                }
                catch (InvalidOperationException)
                {
                    _dismissed.Add(game.Process.Id);
                }
            }
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Starts again the applications that were closed for the game (the ones whose executable is known).</summary>
    public void Relaunch()
    {
        lock (_gate)
        {
            if (Current is not { } report)
            {
                return;
            }

            var running = _processes.List().Select(p => ProcessRules.Normalize(p.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var apps = report.ClosedApps.Select(app =>
            {
                if (!app.Closed || !app.Relaunchable || app.Relaunched || app.Path is null || running.Contains(app.Name))
                {
                    return app;
                }

                return app with { Relaunched = _processes.Start(app.Path) };
            }).ToList();

            Current = report with { ClosedApps = apps };
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private BoostStep PriorityStep(RunningProcess game) =>
        _processes.RaisePriority(game.Id)
            ? new BoostStep("priority", "Priorité du jeu", true, "Priorité haute pour le jeu pendant la partie.")
            : new BoostStep("priority", "Priorité du jeu", false, "Windows a refusé de changer la priorité de ce jeu.");

    private BoostStep PowerStep()
    {
        var active = _plans.Active();
        if (active is null)
        {
            return new BoostStep("power", "Plan d'alimentation", false, "Plan d'alimentation actuel illisible.");
        }

        if (active.Guid == PowerCfg.HighPerformanceGuid)
        {
            return new BoostStep("power", "Plan d'alimentation", true, "Le plan Hautes performances était déjà actif.");
        }

        if (!_plans.Exists(PowerCfg.HighPerformanceGuid))
        {
            return new BoostStep("power", "Plan d'alimentation", false, "Le plan Hautes performances n'existe pas sur ce PC.");
        }

        // Written before switching: if the app stops mid-session, the next start puts the previous plan back.
        _update(s => s with { PendingPowerPlan = active.Guid });
        if (!_plans.Activate(PowerCfg.HighPerformanceGuid))
        {
            _update(s => s with { PendingPowerPlan = null });
            return new BoostStep("power", "Plan d'alimentation", false, "Windows a refusé de changer de plan.");
        }

        _previousPlan = active;
        return new BoostStep("power", "Plan d'alimentation", true, $"Plan Hautes performances pendant la partie (retour à « {active.Name} » ensuite).");
    }

    private void CloseApps(BoosterSettings settings, List<ClosedApp> closed)
    {
        var picked = settings.CloseList.Select(ProcessRules.Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // The role is checked again here: only an ordinary application window can ever be asked to close.
        // One entry per application, however many windows it has, so it is reported and relaunched once.
        var targets = Classified()
            .Where(c => c.Role == ProcessRole.Closable && picked.Contains(ProcessRules.Normalize(c.Process.Name)))
            .GroupBy(c => ProcessRules.Normalize(c.Process.Name), StringComparer.OrdinalIgnoreCase);

        foreach (var app in targets)
        {
            var first = app.First().Process;
            var allClosed = app.Select(c => _processes.CloseGracefully(c.Process.Id, CloseWait)).ToList().All(ok => ok);
            closed.Add(new ClosedApp(app.Key, first.Path, allClosed, first.Path is not null && !first.Path.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase), false));
        }
    }

    private BoostSnapshot Snapshot()
    {
        var metrics = _metrics();
        return new BoostSnapshot(metrics?.MemoryUsedBytes ?? 0, _processes.List().Count, metrics?.CpuLoadPercent ?? 0);
    }
}
