using Treemap.Core.Monitoring;
using Treemap.Core.Monitoring.Fps;
using Treemap.Core.Settings;

namespace Treemap.Core.Tests;

public class OverlayTests
{
    private static readonly FpsSourceState Running = new(FpsStatus.Running);

    private static MetricsSnapshot Metrics(GpuMetrics? gpu = null) => new(
        DateTime.Now,
        CpuLoadPercent: 42.4,
        MemoryUsedBytes: 8L << 30,
        MemoryTotalBytes: 16L << 30,
        0, 0, 0, 0, 0,
        gpu);

    private static GpuMetrics Gpu() => new("RTX", 63.6, 3L << 30, 12L << 30, 71, 150, 1800, 40);

    private static Dictionary<string, string> Lines(OverlaySettings settings, MetricsSnapshot? metrics, FpsReading? fps = null, FpsSourceState? state = null) =>
        OverlayContent.Build(settings, metrics, fps, state ?? Running).ToDictionary(l => l.Label, l => l.Value);

    [Fact]
    public void ShowsOnlyWhatIsEnabled()
    {
        var settings = new OverlaySettings
        {
            ShowFps = false,
            ShowCpu = true,
            ShowGpu = false,
            ShowGpuTemperature = false,
            ShowMemory = false,
            ShowVideoMemory = false,
        };

        var lines = Lines(settings, Metrics(Gpu()));

        Assert.Equal(["CPU"], lines.Keys);
        Assert.Equal("42 %", lines["CPU"]);
    }

    [Fact]
    public void ShowsGpuLinesWhenAGpuIsPresent()
    {
        var settings = new OverlaySettings { ShowVideoMemory = true, ShowFps = false };

        var lines = Lines(settings, Metrics(Gpu()));

        Assert.Equal("64 %", lines["GPU"]);
        Assert.Equal("71 °C", lines["Temp. GPU"]);
        Assert.Equal("25 %", lines["VRAM"]);
        Assert.Equal("50 %", lines["RAM"]);
    }

    [Fact]
    public void OmitsGpuLinesWithoutAGpu()
    {
        var lines = Lines(new OverlaySettings { ShowVideoMemory = true, ShowFps = false }, Metrics(gpu: null));

        Assert.DoesNotContain("GPU", lines.Keys);
        Assert.DoesNotContain("Temp. GPU", lines.Keys);
        Assert.DoesNotContain("VRAM", lines.Keys);
    }

    [Fact]
    public void BeforeTheFirstReading_ShowsPlaceholders()
    {
        var lines = Lines(new OverlaySettings(), metrics: null);

        Assert.Equal("—", lines["CPU"]);
        Assert.Equal("—", lines["GPU"]);
        Assert.Equal("—", lines["RAM"]);
    }

    [Fact]
    public void Fps_ShowsRateAndProcess()
    {
        var lines = Lines(new OverlaySettings(), Metrics(), new FpsReading(10, "FlightSimulator", 143.4));

        Assert.Equal("143  FlightSimulat…", lines["FPS"]);
    }

    [Fact]
    public void Fps_WithoutReading_IsADash()
    {
        var line = OverlayContent.Build(new OverlaySettings(), Metrics(), null, Running).First(l => l.Label == "FPS");

        Assert.Equal("—", line.Value);
        Assert.True(line.Dimmed);
    }

    [Theory]
    [InlineData(FpsStatus.NeedsAdministrator, "admin requis")]
    [InlineData(FpsStatus.Failed, "indisponible")]
    public void Fps_ExplainsWhyItIsMissing(FpsStatus status, string expected)
    {
        var line = OverlayContent.Build(new OverlaySettings(), Metrics(), null, new FpsSourceState(status))
            .First(l => l.Label == "FPS");

        Assert.Equal(expected, line.Value);
        Assert.True(line.Dimmed);
    }

    [Theory]
    [InlineData(OverlayCorner.TopLeft, 16, 16)]
    [InlineData(OverlayCorner.TopRight, 1920 - 200 - 16, 16)]
    [InlineData(OverlayCorner.BottomLeft, 16, 1040 - 100 - 16)]
    [InlineData(OverlayCorner.BottomRight, 1920 - 200 - 16, 1040 - 100 - 16)]
    public void Placement_InsetsFromTheChosenCorner(OverlayCorner corner, double x, double y)
    {
        var (px, py) = OverlayPlacement.Compute(corner, 0, 0, 1920, 1040, 200, 100, 16);

        Assert.Equal(x, px);
        Assert.Equal(y, py);
    }

    [Fact]
    public void Placement_FollowsTheWorkAreaOrigin()
    {
        var (x, y) = OverlayPlacement.Compute(OverlayCorner.TopLeft, areaX: 1920, areaY: 0, 1920, 1080, 100, 50, 10);

        Assert.Equal((1930, 10), (x, y));
    }
}
