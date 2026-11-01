namespace Treemap.Core.Monitoring;

/// <summary>Samples a metrics source at a fixed interval on a background task and publishes each reading.</summary>
public sealed class MetricsSampler : IDisposable
{
    private readonly IMetricsSource _source;
    private readonly TimeSpan _interval;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public MetricsSampler(IMetricsSource source, TimeSpan interval)
    {
        _source = source;
        _interval = interval;
    }

    /// <summary>Raised on a background thread for every reading.</summary>
    public event Action<MetricsSnapshot>? SnapshotReady;

    public MetricsSnapshot? Latest { get; private set; }

    /// <summary>The last error raised by the source, if any; sampling continues after an error.</summary>
    public Exception? LastError { get; private set; }

    public void Start()
    {
        if (_loop is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    public void Dispose()
    {
        if (_cts is null)
        {
            return;
        }

        _cts.Cancel();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // The loop ends with a cancellation: expected.
        }

        _cts.Dispose();
        _cts = null;
        _loop = null;
    }

    private async Task RunAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            do
            {
                SampleOnce();
            }
            while (await timer.WaitForNextTickAsync(token));
        }
        catch (OperationCanceledException)
        {
            // Stopped.
        }
    }

    private void SampleOnce()
    {
        try
        {
            var snapshot = _source.Sample();
            Latest = snapshot;
            SnapshotReady?.Invoke(snapshot);
        }
        catch (Exception ex)
        {
            // A failing sensor must not stop the monitor; the error stays available for display.
            LastError = ex;
        }
    }
}
