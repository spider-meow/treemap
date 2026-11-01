namespace Treemap.Core.Monitoring;

public sealed record GpuMetrics(
    string Name,
    double LoadPercent,
    long MemoryUsedBytes,
    long MemoryTotalBytes,
    int? TemperatureCelsius,
    double? PowerWatts,
    int? ClockMHz,
    int? FanPercent)
{
    public double MemoryPercent => MemoryTotalBytes <= 0 ? 0 : 100.0 * MemoryUsedBytes / MemoryTotalBytes;
}

/// <summary>One reading of the machine. Rates are per second, computed over the time since the previous reading.</summary>
public sealed record MetricsSnapshot(
    DateTime Timestamp,
    double CpuLoadPercent,
    long MemoryUsedBytes,
    long MemoryTotalBytes,
    double DiskReadBytesPerSecond,
    double DiskWriteBytesPerSecond,
    double DiskActivePercent,
    double NetworkDownBytesPerSecond,
    double NetworkUpBytesPerSecond,
    GpuMetrics? Gpu)
{
    public double MemoryPercent => MemoryTotalBytes <= 0 ? 0 : 100.0 * MemoryUsedBytes / MemoryTotalBytes;
}
