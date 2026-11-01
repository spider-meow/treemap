using System.Windows.Threading;
using Treemap.Core.Monitoring;

namespace Treemap.App.Services;

/// <summary>
/// Shared by the Monitor tab and the overlay: samples the machine once a second and keeps a short history
/// of each metric. <see cref="Updated"/> is raised on the UI thread, so view models can read everything freely.
/// </summary>
public sealed class MonitoringService : IDisposable
{
    public const int HistoryLength = 120;

    private readonly MetricsSampler _sampler;
    private readonly Dispatcher _dispatcher;

    public MonitoringService(IMetricsSource source, Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _sampler = new MetricsSampler(source, TimeSpan.FromSeconds(1));
        _sampler.SnapshotReady += OnSnapshot;
    }

    public event EventHandler? Updated;

    public MetricsSnapshot? Latest { get; private set; }

    public MetricHistory Cpu { get; } = new(HistoryLength);

    public MetricHistory Memory { get; } = new(HistoryLength);

    public MetricHistory GpuLoad { get; } = new(HistoryLength);

    public MetricHistory GpuTemperature { get; } = new(HistoryLength);

    public MetricHistory VideoMemory { get; } = new(HistoryLength);

    public MetricHistory DiskRead { get; } = new(HistoryLength);

    public MetricHistory DiskWrite { get; } = new(HistoryLength);

    public MetricHistory NetworkDown { get; } = new(HistoryLength);

    public MetricHistory NetworkUp { get; } = new(HistoryLength);

    public void Start() => _sampler.Start();

    public void Dispose() => _sampler.Dispose();

    private void OnSnapshot(MetricsSnapshot snapshot) =>
        _dispatcher.BeginInvoke(() => Apply(snapshot));

    private void Apply(MetricsSnapshot snapshot)
    {
        Latest = snapshot;
        Cpu.Add(snapshot.CpuLoadPercent);
        Memory.Add(snapshot.MemoryPercent);
        DiskRead.Add(snapshot.DiskReadBytesPerSecond);
        DiskWrite.Add(snapshot.DiskWriteBytesPerSecond);
        NetworkDown.Add(snapshot.NetworkDownBytesPerSecond);
        NetworkUp.Add(snapshot.NetworkUpBytesPerSecond);

        if (snapshot.Gpu is { } gpu)
        {
            GpuLoad.Add(gpu.LoadPercent);
            GpuTemperature.Add(gpu.TemperatureCelsius ?? 0);
            VideoMemory.Add(gpu.MemoryPercent);
        }

        Updated?.Invoke(this, EventArgs.Empty);
    }
}
