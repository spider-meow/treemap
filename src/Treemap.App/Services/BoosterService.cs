using System.Windows.Threading;
using Treemap.Core.Booster;
using Treemap.Core.Bridge;
using Treemap.Core.Settings;

namespace Treemap.App.Services;

/// <summary>
/// Owns the game booster for the life of the app: watches for games every few seconds (to end a session when the game closes,
/// or start one in automatic mode) and puts the power plan back when the app closes during a session.
/// </summary>
public sealed class BoosterService : IDisposable
{
    private readonly SettingsStore _store;
    private readonly DispatcherTimer _timer;
    private BoosterSettings _settings;
    private int _busy;

    public BoosterService(MonitoringService monitoring, SettingsStore store, Dispatcher dispatcher)
    {
        _store = store;
        _settings = store.Load().Booster;

        var plans = new PowerCfg();
        Engine = new BoosterEngine(
            new WindowsProcessSystem(),
            plans,
            () => _settings,
            change => _settings = store.Update(s => s with { Booster = change(s.Booster) }).Booster,
            () => monitoring.Latest);
        Bridge = new BoosterBridge(Engine, plans);

        _timer = new DispatcherTimer(TimeSpan.FromSeconds(3), DispatcherPriority.Background, (_, _) => Tick(), dispatcher);
        _timer.Stop();
    }

    public BoosterEngine Engine { get; }

    public BoosterBridge Bridge { get; }

    public void Start()
    {
        // A previous run may have stopped in the middle of a session: the PC must not stay on the boosted plan.
        Task.Run(Engine.RestorePending);
        _timer.Start();
    }

    public void Dispose()
    {
        _timer.Stop();
        Engine.Stop(); // puts the power plan back; closed applications are not restarted behind the user's back
    }

    private void Tick()
    {
        // Listing processes and closing applications take time: never on the UI thread, never two at once.
        if (Interlocked.Exchange(ref _busy, 1) == 1)
        {
            return;
        }

        Task.Run(() =>
        {
            try
            {
                Engine.Tick();
            }
            finally
            {
                Interlocked.Exchange(ref _busy, 0);
            }
        });
    }
}
