using Treemap.Core.Monitoring.Fps;
using Treemap.Core.Settings;

namespace Treemap.Core.Monitoring;

public sealed record OverlayLine(string Label, string Value, bool Dimmed = false);

/// <summary>Decides which lines the in-game overlay shows, from the user's settings and the latest readings.</summary>
public static class OverlayContent
{
    private const string Unknown = "—";
    private const int MaxProcessNameLength = 14;

    public static IReadOnlyList<OverlayLine> Build(
        OverlaySettings settings,
        MetricsSnapshot? metrics,
        FpsReading? fps,
        FpsSourceState fpsState)
    {
        var lines = new List<OverlayLine>();

        if (settings.ShowFps)
        {
            lines.Add(FpsLine(fps, fpsState));
        }

        if (settings.ShowCpu)
        {
            lines.Add(new OverlayLine("CPU", Percent(metrics?.CpuLoadPercent)));
        }

        var gpu = metrics?.Gpu;
        if (settings.ShowGpu && (gpu is not null || metrics is null))
        {
            lines.Add(new OverlayLine("GPU", Percent(gpu?.LoadPercent)));
        }

        if (settings.ShowGpuTemperature && gpu?.TemperatureCelsius is { } temperature)
        {
            lines.Add(new OverlayLine("Temp. GPU", $"{temperature} °C"));
        }

        if (settings.ShowMemory)
        {
            lines.Add(new OverlayLine("RAM", Percent(metrics?.MemoryPercent)));
        }

        if (settings.ShowVideoMemory && gpu is not null)
        {
            lines.Add(new OverlayLine("VRAM", Percent(gpu.MemoryPercent)));
        }

        return lines;
    }

    private static OverlayLine FpsLine(FpsReading? fps, FpsSourceState state) => state.Status switch
    {
        FpsStatus.NeedsAdministrator => new OverlayLine("FPS", "admin requis", Dimmed: true),
        FpsStatus.Failed => new OverlayLine("FPS", "indisponible", Dimmed: true),
        FpsStatus.Running when fps is not null =>
            new OverlayLine("FPS", $"{fps.Fps:N0}  {ShortName(fps.ProcessName)}"),
        _ => new OverlayLine("FPS", Unknown, Dimmed: true),
    };

    private static string Percent(double? value) => value is { } v ? $"{v:N0} %" : Unknown;

    private static string ShortName(string name) =>
        name.Length <= MaxProcessNameLength ? name : name[..(MaxProcessNameLength - 1)] + "…";
}

/// <summary>Where the overlay window goes: a screen corner, inset by a margin.</summary>
public static class OverlayPlacement
{
    public static (double X, double Y) Compute(
        OverlayCorner corner,
        double areaX,
        double areaY,
        double areaWidth,
        double areaHeight,
        double windowWidth,
        double windowHeight,
        double margin)
    {
        var left = areaX + margin;
        var right = areaX + areaWidth - windowWidth - margin;
        var top = areaY + margin;
        var bottom = areaY + areaHeight - windowHeight - margin;

        return corner switch
        {
            OverlayCorner.TopLeft => (left, top),
            OverlayCorner.TopRight => (right, top),
            OverlayCorner.BottomLeft => (left, bottom),
            OverlayCorner.BottomRight => (right, bottom),
            _ => (left, top),
        };
    }
}
