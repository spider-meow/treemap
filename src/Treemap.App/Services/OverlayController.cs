using System.IO;
using System.Windows.Threading;
using Treemap.App.Infrastructure;
using Treemap.Core.Bridge;
using Treemap.Core.Monitoring;
using Treemap.Core.Monitoring.Fps;
using Treemap.Core.Settings;

namespace Treemap.App.Services;

public sealed class OverlayViewModel : ObservableObject
{
    private IReadOnlyList<OverlayLine> _lines = [];

    public IReadOnlyList<OverlayLine> Lines
    {
        get => _lines;
        set => SetProperty(ref _lines, value);
    }
}

/// <summary>
/// Owns the overlay settings and the overlay window: shows or hides it, keeps its content fresh, starts the frame
/// counter only when the overlay actually needs FPS, and saves every change.
/// </summary>
public sealed class OverlayController : IOverlayControl, IDisposable
{
    private readonly MonitoringService _monitoring;
    private readonly SettingsStore _store;
    private readonly OverlayViewModel _viewModel = new();
    private readonly DispatcherTimer _timer;
    private AppSettings _settings;
    private OverlayWindow? _window;
    private FpsMonitor? _fps;

    public OverlayController(MonitoringService monitoring, SettingsStore store, Dispatcher dispatcher)
    {
        _monitoring = monitoring;
        _store = store;
        _settings = store.Load();
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) => Refresh(), dispatcher);
        _timer.Stop();
    }

    public event EventHandler? Changed;

    public OverlaySettings Settings => _settings.Overlay;

    public FpsSourceState FpsState => _fps?.State ?? new FpsSourceState(FpsStatus.NotStarted);

    public FpsReading? CurrentFps => _fps?.GetCurrent();

    public void Start() => Apply();

    public void Update(Func<OverlaySettings, OverlaySettings> change)
    {
        var normalized = change(_settings.Overlay).Normalized();
        _settings = _settings with { Overlay = normalized };
        TrySave(normalized);
        Apply();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle() => Update(s => s with { Enabled = !s.Enabled });

    public void Dispose()
    {
        _timer.Stop();
        _window?.Close();
        _fps?.Dispose();
    }

    private void Apply()
    {
        var overlay = _settings.Overlay;
        if (!overlay.Enabled)
        {
            _timer.Stop();
            _window?.Hide();
            return;
        }

        if (overlay.ShowFps && _fps is null)
        {
            _fps = new FpsMonitor(new EtwPresentSource());
            _fps.Start();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        if (_window is null)
        {
            _window = new OverlayWindow { DataContext = _viewModel };
        }

        _window.Opacity = overlay.Opacity;
        _window.SetCorner(overlay.Corner);
        Refresh();
        _window.Show();
        _timer.Start();
    }

    private void Refresh()
    {
        _viewModel.Lines = OverlayContent.Build(
            _settings.Overlay,
            _monitoring.Latest,
            _fps?.GetCurrent(),
            FpsState);
    }

    private void TrySave(OverlaySettings overlay)
    {
        try
        {
            // Only the overlay section is written, on top of what is on disk now (the booster keeps its own).
            _store.Update(settings => settings with { Overlay = overlay });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not being able to remember the settings must not break the overlay: it just won't persist.
        }
    }
}
