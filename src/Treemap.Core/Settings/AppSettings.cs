namespace Treemap.Core.Settings;

public enum OverlayCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

public sealed record OverlaySettings
{
    public const double MinOpacity = 0.3;

    public bool Enabled { get; init; }

    public bool ShowFps { get; init; } = true;

    public bool ShowCpu { get; init; } = true;

    public bool ShowGpu { get; init; } = true;

    public bool ShowGpuTemperature { get; init; } = true;

    public bool ShowMemory { get; init; } = true;

    public bool ShowVideoMemory { get; init; }

    public OverlayCorner Corner { get; init; } = OverlayCorner.TopLeft;

    public double Opacity { get; init; } = 0.85;

    /// <summary>Pulls hand-edited or outdated values back into a usable range.</summary>
    public OverlaySettings Normalized() =>
        this with
        {
            Opacity = double.IsFinite(Opacity) ? Math.Clamp(Opacity, MinOpacity, 1.0) : 0.85,
            Corner = Enum.IsDefined(Corner) ? Corner : OverlayCorner.TopLeft,
        };
}

public sealed record AppSettings
{
    public OverlaySettings Overlay { get; init; } = new();
}
