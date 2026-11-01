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
    public void Monitor_FindsFramesDrawnByAChildProcess()
    {
        // Chromium-style: the window belongs to process 10, the GPU process 11 (its child) draws the frames.
        var source = new FakeSource(new FpsSourceState(FpsStatus.Running));
        using var monitor = new FpsMonitor(
            source,
            foregroundProcessId: () => 10,
            descendants: pid => pid == 10 ? [11, 12] : []);
        monitor.Start();

        for (var i = 0; i < 40; i++)
        {
            source.Emit(11);
            source.Emit(99); // unrelated process, must be ignored
        }

        source.Emit(12);

        var reading = monitor.GetCurrent();
        Assert.NotNull(reading);
        Assert.Equal(11, reading.ProcessId); // the busiest of the foreground app's processes
    }

    [Fact]
    public void Monitor_PrefersTheForegroundProcessOwnFrames()
    {
        var source = new FakeSource(new FpsSourceState(FpsStatus.Running));
        using var monitor = new FpsMonitor(source, foregroundProcessId: () => 10, descendants: _ => [11]);
        monitor.Start();

        source.Emit(10);
        for (var i = 0; i < 50; i++)
        {
            source.Emit(11);
        }

        Assert.Equal(10, monitor.GetCurrent()!.ProcessId);
    }

    [Fact]
    public void ProcessTree_FollowsParentLinksAtAnyDepth()
    {
        var parentOf = new Dictionary<int, int> { [2] = 1, [3] = 2, [4] = 2, [5] = 3, [9] = 8 };

        var found = ProcessTree.Descendants(1, parentOf);

        Assert.Equal([2, 3, 4, 5], found.Order());
        Assert.DoesNotContain(9, found);
    }

    [Fact]
    public void ProcessTree_SurvivesAParentLoop()
    {
        // A recycled process id can make two entries claim each other as parent.
        var parentOf = new Dictionary<int, int> { [1] = 2, [2] = 1 };

        Assert.Equal([2], ProcessTree.Descendants(1, parentOf));
    }

    [Fact]
    public void ProcessTree_SeesARealChildProcess()
    {
        using var child = System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c ping -n 4 127.0.0.1 >nul") { CreateNoWindow = true, UseShellExecute = false })!;
        try
        {
            Assert.Contains(child.Id, ProcessTree.Descendants(Environment.ProcessId));
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
            }
        }
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
