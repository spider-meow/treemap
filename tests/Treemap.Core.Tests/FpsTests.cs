using Treemap.Core.Monitoring.Fps;

namespace Treemap.Core.Tests;

public class FpsTests
{
    [Fact]
    public void Counter_ReportsFramesPerSecondOverTheWindow()
    {
        var counter = new FrameRateCounter();
        for (var i = 0; i < 60; i++)
        {
            counter.RecordFrame(1, 10 + i / 60.0); // 60 frames spread over one second
        }

        Assert.Equal(60, counter.GetFps(1, nowSeconds: 10.999), precision: 6);
    }

    [Fact]
    public void Counter_DropsFramesOlderThanTheWindow()
    {
        var counter = new FrameRateCounter();
        counter.RecordFrame(1, 0);
        counter.RecordFrame(1, 0.4);

        Assert.Equal(2, counter.GetFps(1, 0.5));
        Assert.Equal(1, counter.GetFps(1, 1.2)); // the frame at t=0 is out of the window
        Assert.Equal(0, counter.GetFps(1, 5)); // everything is stale
    }

    [Fact]
    public void Counter_KeepsProcessesApart()
    {
        var counter = new FrameRateCounter();
        counter.RecordFrame(1, 0.1);
        counter.RecordFrame(2, 0.1);
        counter.RecordFrame(2, 0.2);

        Assert.Equal(1, counter.GetFps(1, 0.5));
        Assert.Equal(2, counter.GetFps(2, 0.5));
        Assert.Equal(0, counter.GetFps(3, 0.5));
    }

    [Fact]
    public void Counter_RejectsAnEmptyWindow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameRateCounter(TimeSpan.Zero));
    }

    private sealed class FakeSource(FpsSourceState state) : IPresentSource
    {
        public event Action<int>? FramePresented;

        public bool Started { get; private set; }

        public FpsSourceState Start()
        {
            Started = true;
            return state;
        }

        public void Emit(int processId) => FramePresented?.Invoke(processId);

        public void Dispose()
        {
        }
    }

    [Fact]
    public void Monitor_ReportsTheForegroundProcessOnly()
    {
        var source = new FakeSource(new FpsSourceState(FpsStatus.Running));
        var foreground = 100;
        using var monitor = new FpsMonitor(source, foregroundProcessId: () => foreground);
        monitor.Start();

        for (var i = 0; i < 30; i++)
        {
            source.Emit(100); // the game
            source.Emit(200); // e.g. the desktop compositor, presenting all the time
        }

        var reading = monitor.GetCurrent();
        Assert.NotNull(reading);
        Assert.Equal(100, reading.ProcessId);
        Assert.True(reading.Fps > 0);

        foreground = 300; // a window that presents nothing
        Assert.Null(monitor.GetCurrent());
    }

    [Fact]
    public void Monitor_WithNoFrames_ReportsNothing()
    {
        var source = new FakeSource(new FpsSourceState(FpsStatus.Running));
        using var monitor = new FpsMonitor(source, foregroundProcessId: () => 1);
        monitor.Start();

        Assert.Null(monitor.GetCurrent());
    }

    [Fact]
    public void Monitor_ExposesWhyItCannotRun()
    {
        var source = new FakeSource(new FpsSourceState(FpsStatus.NeedsAdministrator, "admin needed"));
        using var monitor = new FpsMonitor(source, foregroundProcessId: () => 1);

        monitor.Start();
        source.Emit(1);

        Assert.Equal(FpsStatus.NeedsAdministrator, monitor.State.Status);
        Assert.Equal("admin needed", monitor.State.Message);
        Assert.Null(monitor.GetCurrent());
    }

    [Fact]
    public void Etw_WithoutAdministratorRights_AsksForThem()
    {
        if (EtwPresentSource.IsElevated)
        {
            return; // Elevated test run: starting would open a real session, which this test does not want.
        }

        using var source = new EtwPresentSource();

        Assert.Equal(FpsStatus.NeedsAdministrator, source.Start().Status);
    }
}
