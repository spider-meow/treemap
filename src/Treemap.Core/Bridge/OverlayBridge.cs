using System.Text.Json;
using Treemap.Core.Monitoring.Fps;
using Treemap.Core.Settings;

namespace Treemap.Core.Bridge;

/// <summary>What the overlay messages need from the app: the settings, the frame counter state, and a way to change settings.</summary>
public interface IOverlayControl
{
    OverlaySettings Settings { get; }

    FpsSourceState FpsState { get; }

    /// <summary>The frame rate of the foreground application right now, when the counter is running.</summary>
    FpsReading? CurrentFps { get; }

    /// <summary>Applies the change, keeps the result in range, saves it and updates the overlay window.</summary>
    void Update(Func<OverlaySettings, OverlaySettings> change);
}

public sealed record HotkeyInfo(bool Registered, string Description);

public sealed record OverlayStateDto(
    OverlaySettings Settings,
    double MinOpacity,
    FpsStatus FpsStatus,
    string? FpsMessage,
    bool CanRelaunchAsAdmin,
    HotkeyInfo Hotkey);

public sealed record FpsDto(string ProcessName, double Fps);

/// <summary>
/// The overlay messages of the web interface: read and change the overlay settings, and restart the app as administrator
/// (Windows then shows its own consent prompt) so the frame counter can run.
/// </summary>
public sealed class OverlayBridge
{
    private readonly IOverlayControl _overlay;
    private readonly Func<HotkeyInfo> _hotkey;
    private readonly Func<bool> _relaunchAsAdmin;

    public OverlayBridge(IOverlayControl overlay, Func<HotkeyInfo> hotkey, Func<bool> relaunchAsAdmin)
    {
        _overlay = overlay;
        _hotkey = hotkey;
        _relaunchAsAdmin = relaunchAsAdmin;
    }

    public OverlayStateDto State()
    {
        var fps = _overlay.FpsState;
        return new OverlayStateDto(
            _overlay.Settings,
            OverlaySettings.MinOpacity,
            fps.Status,
            fps.Message,
            _overlay.Settings.ShowFps && fps.Status == FpsStatus.NeedsAdministrator,
            _hotkey());
    }

    /// <summary>The reading to push each second while the counter runs; null otherwise.</summary>
    public FpsDto? Fps() =>
        _overlay.FpsState.Status == FpsStatus.Running && _overlay.CurrentFps is { } reading
            ? new FpsDto(reading.ProcessName, reading.Fps)
            : null;

    public void Register(BridgeRouter router)
    {
        router.Register("overlay.get", () => Task.FromResult<object?>(State()));
        router.Register("overlay.update", payload =>
        {
            var settings = (payload.ValueKind == JsonValueKind.Undefined ? null : payload.Deserialize<OverlaySettings>(BridgeRouter.Options))
                ?? throw new ArgumentException("Réglages incomplets.");
            _overlay.Update(_ => settings);
            return Task.FromResult<object?>(State());
        });
        router.Register("overlay.relaunchAsAdmin", () =>
        {
            if (!State().CanRelaunchAsAdmin)
            {
                throw new InvalidOperationException("Le FPS n'a pas besoin des droits administrateur en ce moment.");
            }

            return Task.FromResult<object?>(_relaunchAsAdmin());
        });
    }
}
