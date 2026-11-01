using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

namespace Treemap.Core.Monitoring.Fps;

/// <summary>
/// Counts frames from the Windows event tracing providers Microsoft-Windows-DXGI (DirectX 10/11/12 swap chains) and
/// Microsoft-Windows-D3D9: the same approach as Intel PresentMon, so nothing is injected into the game and anti-cheat
/// systems have nothing to object to. Vulkan and OpenGL games are not covered by these two providers.
/// Opening an event session needs administrator rights.
/// </summary>
public sealed class EtwPresentSource : IPresentSource
{
    private static readonly Guid DxgiProvider = new("ca11c036-0102-4a2d-a6ad-f03cfed5d3c9");
    private static readonly Guid D3D9Provider = new("783aca0a-790e-4d7f-8451-aa850511c6b9");

    /// <summary>"Present" task, start opcode, in each provider (checked against the providers' manifests).</summary>
    private const int DxgiPresentStart = 42;
    private const int D3D9PresentStart = 1;

    /// <summary>Both providers tag their Present events with this keyword.</summary>
    private const ulong EventsKeyword = 0x2;

    private readonly string _sessionName = $"treemap-fps-{Environment.ProcessId}";
    private TraceEventSession? _session;
    private Thread? _thread;

    public event Action<int>? FramePresented;

    public static bool IsElevated => TraceEventSession.IsElevated() == true;

    public FpsSourceState Start()
    {
        if (!IsElevated)
        {
            return new FpsSourceState(
                FpsStatus.NeedsAdministrator,
                "Le FPS se mesure avec les événements de Windows, réservés aux administrateurs.");
        }

        try
        {
            _session = new TraceEventSession(_sessionName) { StopOnDispose = true };
            _session.EnableProvider(DxgiProvider, TraceEventLevel.Informational, EventsKeyword);
            _session.EnableProvider(D3D9Provider, TraceEventLevel.Informational, EventsKeyword);
            _session.Source.AllEvents += OnEvent;

            _thread = new Thread(() => _session.Source.Process()) { IsBackground = true, Name = "treemap-etw" };
            _thread.Start();
            return new FpsSourceState(FpsStatus.Running);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _session?.Dispose();
            _session = null;
            return new FpsSourceState(FpsStatus.Failed, ex.Message);
        }
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
    }

    private void OnEvent(TraceEvent data)
    {
        var isPresent = (data.ProviderGuid == DxgiProvider && (int)data.ID == DxgiPresentStart)
            || (data.ProviderGuid == D3D9Provider && (int)data.ID == D3D9PresentStart);
        if (isPresent)
        {
            FramePresented?.Invoke(data.ProcessID);
        }
    }
}
