namespace Treemap.Core.Monitoring.Fps;

/// <summary>
/// Turns "process X presented a frame at time T" observations into frames per second.
/// FPS is the number of frames seen in the last <see cref="Window"/>, so it follows changes within about a second.
/// </summary>
public sealed class FrameRateCounter
{
    private readonly Dictionary<int, Queue<double>> _frames = [];
    private readonly Lock _lock = new();

    public FrameRateCounter(TimeSpan? window = null)
    {
        Window = window ?? TimeSpan.FromSeconds(1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Window, TimeSpan.Zero);
    }

    public TimeSpan Window { get; }

    /// <summary>Records a frame; <paramref name="seconds"/> is any monotonic clock shared with <see cref="GetFps"/>.</summary>
    public void RecordFrame(int processId, double seconds)
    {
        lock (_lock)
        {
            if (!_frames.TryGetValue(processId, out var queue))
            {
                _frames[processId] = queue = new Queue<double>();
            }

            queue.Enqueue(seconds);
            Trim(queue, seconds);
        }
    }

    /// <summary>Frames per second of a process, or 0 when it has not presented recently.</summary>
    public double GetFps(int processId, double nowSeconds)
    {
        lock (_lock)
        {
            if (!_frames.TryGetValue(processId, out var queue))
            {
                return 0;
            }

            Trim(queue, nowSeconds);
            if (queue.Count == 0)
            {
                _frames.Remove(processId);
                return 0;
            }

            return queue.Count / Window.TotalSeconds;
        }
    }

    private void Trim(Queue<double> queue, double nowSeconds)
    {
        var oldest = nowSeconds - Window.TotalSeconds;
        while (queue.Count > 0 && queue.Peek() <= oldest)
        {
            queue.Dequeue();
        }
    }
}
