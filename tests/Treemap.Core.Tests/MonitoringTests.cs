using Treemap.Core.Monitoring;

namespace Treemap.Core.Tests;

public class MonitoringTests
{
    [Fact]
    public void History_ReturnsValuesOldestFirst()
    {
        var history = new MetricHistory(3);
        history.Add(1);
        history.Add(2);

        Assert.Equal([1d, 2d], history.ToList());
    }

    [Fact]
    public void History_DropsTheOldestWhenFull()
    {
        var history = new MetricHistory(3);
        foreach (var v in new double[] { 1, 2, 3, 4, 5 })
        {
            history.Add(v);
        }

        Assert.Equal([3d, 4d, 5d], history.ToList());
        Assert.Equal(3, history.Count);
    }

    [Fact]
    public void History_RejectsZeroCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MetricHistory(0));
    }

    private sealed class FakeSource : IMetricsSource
    {
        public int Calls;
        public bool FailOnce;

        public MetricsSnapshot Sample()
        {
            var call = Interlocked.Increment(ref Calls);
            if (FailOnce && call == 1)
            {
                throw new InvalidOperationException("sensor glitch");
            }

            return new MetricsSnapshot(DateTime.Now, call, 0, 0, 0, 0, 0, 0, 0, null);
        }

        public void Dispose()
        {
        }
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task Sampler_PublishesReadingsRepeatedly()
    {
        var source = new FakeSource();
        var received = new List<double>();
        using var sampler = new MetricsSampler(source, TimeSpan.FromMilliseconds(20));
        sampler.SnapshotReady += s =>
        {
            lock (received)
            {
                received.Add(s.CpuLoadPercent);
            }
        };

        sampler.Start();
        await WaitUntil(() => source.Calls >= 3);

        Assert.True(source.Calls >= 3);
        Assert.NotNull(sampler.Latest);
        lock (received)
        {
            Assert.Equal(1, received[0]);
        }
    }

    [Fact]
    public async Task Sampler_StopsAfterDispose()
    {
        var source = new FakeSource();
        var sampler = new MetricsSampler(source, TimeSpan.FromMilliseconds(10));
        sampler.Start();
        await WaitUntil(() => source.Calls >= 2);

        sampler.Dispose();
        var callsAtStop = source.Calls;
        await Task.Delay(80);

        Assert.Equal(callsAtStop, source.Calls);
    }

    [Fact]
    public async Task Sampler_SurvivesAFailingReading()
    {
        var source = new FakeSource { FailOnce = true };
        using var sampler = new MetricsSampler(source, TimeSpan.FromMilliseconds(15));

        sampler.Start();
        await WaitUntil(() => sampler.Latest is not null);

        Assert.NotNull(sampler.Latest);
        Assert.IsType<InvalidOperationException>(sampler.LastError);
    }

    [Fact]
    public async Task SystemSource_ReturnsPlausibleValuesOnThisMachine()
    {
        using var source = new SystemMetricsSource();
        await Task.Delay(500);

        var snapshot = source.Sample();

        Assert.InRange(snapshot.CpuLoadPercent, 0, 100);
        Assert.True(snapshot.MemoryTotalBytes > 0);
        Assert.InRange(snapshot.MemoryUsedBytes, 1, snapshot.MemoryTotalBytes);
        Assert.InRange(snapshot.MemoryPercent, 0, 100);
        Assert.True(snapshot.DiskReadBytesPerSecond >= 0);
        Assert.True(snapshot.DiskWriteBytesPerSecond >= 0);
        Assert.InRange(snapshot.DiskActivePercent, 0, 100);
        Assert.True(snapshot.NetworkDownBytesPerSecond >= 0);
        Assert.True(snapshot.NetworkUpBytesPerSecond >= 0);
    }

    [Fact]
    public void Gpu_WhenPresent_ReportsCoherentValues()
    {
        using var reader = NvmlGpuReader.TryCreate();
        if (reader is null)
        {
            return; // No NVIDIA GPU on this machine: nothing to check.
        }

        var gpu = reader.Read();

        Assert.NotNull(gpu);
        Assert.False(string.IsNullOrWhiteSpace(gpu.Name));
        Assert.InRange(gpu.LoadPercent, 0, 100);
        Assert.True(gpu.MemoryTotalBytes > 0);
        Assert.InRange(gpu.MemoryUsedBytes, 0, gpu.MemoryTotalBytes);
        if (gpu.TemperatureCelsius is { } temperature)
        {
            Assert.InRange(temperature, 10, 120);
        }
    }
}
