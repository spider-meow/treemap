using System.Windows.Input;
using Treemap.App.Infrastructure;
using Treemap.App.Services;
using Treemap.Core.Monitoring.Fps;
using Treemap.Core.Settings;

namespace Treemap.App.ViewModels;

public sealed record CornerOption(OverlayCorner Value, string Label);

/// <summary>Binds the overlay settings card to the overlay controller; every edit is applied and saved at once.</summary>
public sealed class OverlaySettingsViewModel : ObservableObject
{
    private readonly OverlayController _controller;
    private readonly Func<bool> _hotkeyRegistered;

    public OverlaySettingsViewModel(OverlayController controller, Func<bool> hotkeyRegistered)
    {
        _controller = controller;
        _hotkeyRegistered = hotkeyRegistered;
        RelaunchAsAdminCommand = new RelayCommand(_ => AdminRelauncher.Relaunch("--tab=monitor"), _ => CanRelaunchAsAdmin);
        _controller.Changed += (_, _) => OnPropertyChanged(string.Empty);
    }

    public IReadOnlyList<CornerOption> Corners { get; } =
    [
        new(OverlayCorner.TopLeft, "En haut à gauche"),
        new(OverlayCorner.TopRight, "En haut à droite"),
        new(OverlayCorner.BottomLeft, "En bas à gauche"),
        new(OverlayCorner.BottomRight, "En bas à droite"),
    ];

    public ICommand RelaunchAsAdminCommand { get; }

    public bool Enabled
    {
        get => _controller.Settings.Enabled;
        set => _controller.Update(s => s with { Enabled = value });
    }

    public bool ShowFps
    {
        get => _controller.Settings.ShowFps;
        set => _controller.Update(s => s with { ShowFps = value });
    }

    public bool ShowCpu
    {
        get => _controller.Settings.ShowCpu;
        set => _controller.Update(s => s with { ShowCpu = value });
    }

    public bool ShowGpu
    {
        get => _controller.Settings.ShowGpu;
        set => _controller.Update(s => s with { ShowGpu = value });
    }

    public bool ShowGpuTemperature
    {
        get => _controller.Settings.ShowGpuTemperature;
        set => _controller.Update(s => s with { ShowGpuTemperature = value });
    }

    public bool ShowMemory
    {
        get => _controller.Settings.ShowMemory;
        set => _controller.Update(s => s with { ShowMemory = value });
    }

    public bool ShowVideoMemory
    {
        get => _controller.Settings.ShowVideoMemory;
        set => _controller.Update(s => s with { ShowVideoMemory = value });
    }

    public OverlayCorner Corner
    {
        get => _controller.Settings.Corner;
        set => _controller.Update(s => s with { Corner = value });
    }

    public double Opacity
    {
        get => _controller.Settings.Opacity;
        set => _controller.Update(s => s with { Opacity = value });
    }

    public double MinOpacity => OverlaySettings.MinOpacity;

    public bool CanRelaunchAsAdmin => ShowFps && _controller.FpsState.Status == FpsStatus.NeedsAdministrator;

    public string HotkeyHint => _hotkeyRegistered()
        ? $"Raccourci pour afficher ou masquer : {GlobalHotkey.Description}."
        : $"Le raccourci {GlobalHotkey.Description} est déjà pris par un autre programme.";

    public string FpsStatusText
    {
        get
        {
            var state = _controller.FpsState;
            return state.Status switch
            {
                FpsStatus.Running => "FPS : mesure active (jeux DirectX 9 à 12 ; Vulkan et OpenGL ne sont pas couverts).",
                FpsStatus.NeedsAdministrator => "FPS : Windows réserve cette mesure aux administrateurs. Relance l'appli en administrateur pour l'activer.",
                FpsStatus.Failed => $"FPS : mesure impossible ({state.Message}).",
                _ => "FPS : la mesure démarre avec l'overlay.",
            };
        }
    }
}
