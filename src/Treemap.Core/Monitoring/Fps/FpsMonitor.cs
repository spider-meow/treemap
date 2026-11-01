using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Treemap.Core.Monitoring.Fps;

public enum FpsStatus
{
    NotStarted,
    Running,

    /// <summary>Windows only lets administrators (or Performance Log Users members) listen to frame events.</summary>
    NeedsAdministrator,
    Failed,
}

public readonly record struct FpsSourceState(FpsStatus Status, string? Message = null);

/// <summary>Anything that can tell when a process presents a frame.</summary>
public interface IPresentSource : IDisposable
{
    /// <summary>Raised on a background thread: process id of the process that just presented a frame.</summary>
    event Action<int>? FramePresented;

    FpsSourceState Start();
}

public sealed record FpsReading(int ProcessId, string ProcessName, double Fps);

/// <summary>
/// Reports the frame rate of the application in the foreground, which is what an in-game overlay wants:
/// the desktop compositor presents at the refresh rate all the time and must not be mistaken for a game.
/// The foreground application counts together with the processes it started: browsers and other Chromium-based
/// apps own the window in one process but draw the frames in a child (the GPU process).
/// </summary>
public sealed class FpsMonitor : IDisposable
{
    private readonly IPresentSource _source;
    private readonly FrameRateCounter _counter;
    private readonly Func<int?> _foregroundProcessId;
    private readonly Func<int, IReadOnlyList<int>> _descendants;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public FpsMonitor(
        IPresentSource source,
        FrameRateCounter? counter = null,
        Func<int?>? foregroundProcessId = null,
        Func<int, IReadOnlyList<int>>? descendants = null)
    {
        _source = source;
        _counter = counter ?? new FrameRateCounter();
        _foregroundProcessId = foregroundProcessId ?? ForegroundWindow.GetProcessId;
        _descendants = descendants ?? ProcessTree.Descendants;
    }

    public FpsSourceState State { get; private set; } = new(FpsStatus.NotStarted);

    public void Start()
    {
        if (State.Status == FpsStatus.Running)
        {
            return;
        }

        _source.FramePresented += OnFramePresented;
        State = _source.Start();
        if (State.Status != FpsStatus.Running)
        {
            _source.FramePresented -= OnFramePresented;
        }
    }

    /// <summary>The foreground application's frame rate, or null if it is not presenting frames (not a game, or not started).</summary>
    public FpsReading? GetCurrent()
    {
        if (State.Status != FpsStatus.Running || _foregroundProcessId() is not { } pid)
        {
            return null;
        }

        var now = _clock.Elapsed.TotalSeconds;
        var ownFps = _counter.GetFps(pid, now);
        if (ownFps > 0)
        {
            return new FpsReading(pid, ProcessName(pid), ownFps);
        }

        // Nothing from the window's own process: look at what it started, and keep the busiest renderer.
        FpsReading? best = null;
        foreach (var child in _descendants(pid))
        {
            var fps = _counter.GetFps(child, now);
            if (fps > (best?.Fps ?? 0))
            {
                best = new FpsReading(child, ProcessName(child), fps);
            }
        }

        return best;
    }

    public void Dispose()
    {
        _source.FramePresented -= OnFramePresented;
        _source.Dispose();
    }

    private void OnFramePresented(int processId) => _counter.RecordFrame(processId, _clock.Elapsed.TotalSeconds);

    private static string ProcessName(int processId)
    {
        try
        {
            return Process.GetProcessById(processId).ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return $"PID {processId}";
        }
    }
}

internal static class ForegroundWindow
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    public static int? GetProcessId()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return null;
        }

        _ = GetWindowThreadProcessId(window, out var processId);
        return processId == 0 ? null : (int)processId;
    }
}
