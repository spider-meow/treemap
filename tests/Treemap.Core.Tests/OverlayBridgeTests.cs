using System.Text.Json;
using Treemap.Core.Bridge;
using Treemap.Core.Monitoring.Fps;
using Treemap.Core.Settings;
using Xunit;

namespace Treemap.Core.Tests;

public class OverlayBridgeTests
{
    private sealed class FakeOverlay : IOverlayControl
    {
        public OverlaySettings Settings { get; private set; } = new();

        public FpsSourceState FpsState { get; set; } = new(FpsStatus.NotStarted);

        public FpsReading? CurrentFps { get; set; }

        public void Update(Func<OverlaySettings, OverlaySettings> change) => Settings = change(Settings).Normalized();
    }

    private readonly FakeOverlay _overlay = new();
    private readonly BridgeRouter _router = new();
    private readonly OverlayBridge _bridge;
    private int _relaunches;
    private int _nextId = 1;

    public OverlayBridgeTests()
    {
        _bridge = new OverlayBridge(_overlay, () => new HotkeyInfo(true, "Ctrl+Alt+F10"), () =>
        {
            _relaunches++;
            return true;
        });
        _bridge.Register(_router);
    }

    private async Task<JsonElement> Call(string type, object? payload = null)
    {
        var message = JsonSerializer.Serialize(new { id = _nextId++, type, payload }, BridgeRouter.Options);
        return JsonDocument.Parse((await _router.HandleAsync(message))!).RootElement;
    }

    [Fact]
    public async Task Get_DescribesSettingsHotkeyAndFpsState()
    {
        var reply = await Call("overlay.get");

        Assert.True(reply.GetProperty("ok").GetBoolean());
        var state = reply.GetProperty("payload");
        Assert.False(state.GetProperty("settings").GetProperty("enabled").GetBoolean());
        Assert.Equal("topLeft", state.GetProperty("settings").GetProperty("corner").GetString());
        Assert.Equal(0.3, state.GetProperty("minOpacity").GetDouble());
        Assert.Equal("notStarted", state.GetProperty("fpsStatus").GetString());
        Assert.Equal("Ctrl+Alt+F10", state.GetProperty("hotkey").GetProperty("description").GetString());
    }

    [Fact]
    public async Task Update_AppliesAndReturnsTheNormalisedSettings()
    {
        var reply = await Call("overlay.update", new { enabled = true, showFps = false, showCpu = true, showGpu = true, showGpuTemperature = true, showMemory = true, showVideoMemory = true, corner = "bottomRight", opacity = 0.05 });

        var settings = reply.GetProperty("payload").GetProperty("settings");
        Assert.True(settings.GetProperty("enabled").GetBoolean());
        Assert.Equal("bottomRight", settings.GetProperty("corner").GetString());
        Assert.Equal(0.3, settings.GetProperty("opacity").GetDouble());
        Assert.Equal(OverlayCorner.BottomRight, _overlay.Settings.Corner);
        Assert.False(_overlay.Settings.ShowFps);
    }

    [Fact]
    public async Task Update_WithoutSettings_IsAnError()
    {
        var reply = await Call("overlay.update");

        Assert.False(reply.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Relaunch_IsOnlyOfferedWhenFpsNeedsAdministratorRights()
    {
        var refused = await Call("overlay.relaunchAsAdmin");
        Assert.False(refused.GetProperty("ok").GetBoolean());
        Assert.Equal(0, _relaunches);

        _overlay.FpsState = new FpsSourceState(FpsStatus.NeedsAdministrator);
        Assert.True(_bridge.State().CanRelaunchAsAdmin);

        var accepted = await Call("overlay.relaunchAsAdmin");
        Assert.True(accepted.GetProperty("payload").GetBoolean());
        Assert.Equal(1, _relaunches);
    }

    [Fact]
    public void Relaunch_IsNotOfferedWhenTheOverlayDoesNotShowFps()
    {
        _overlay.Update(s => s with { ShowFps = false });
        _overlay.FpsState = new FpsSourceState(FpsStatus.NeedsAdministrator);

        Assert.False(_bridge.State().CanRelaunchAsAdmin);
    }

    [Fact]
    public void Fps_IsReportedOnlyWhileTheCounterRuns()
    {
        _overlay.CurrentFps = new FpsReading(10, "game", 143.5);
        Assert.Null(_bridge.Fps());

        _overlay.FpsState = new FpsSourceState(FpsStatus.Running);
        Assert.Equal(143.5, _bridge.Fps()!.Fps);
        Assert.Equal("game", _bridge.Fps()!.ProcessName);

        _overlay.CurrentFps = null;
        Assert.Null(_bridge.Fps());
    }
}
