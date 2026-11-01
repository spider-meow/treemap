using System.Windows;
using Microsoft.Web.WebView2.Core;
using Treemap.Core.Bridge;
using Treemap.Core.Specs;
using Treemap.Core.Storage;
using Treemap.Core.Storage.Cleanup;

namespace Treemap.App.Services;

/// <summary>
/// Connects the web interface to the C# engine: incoming messages go through the <see cref="BridgeRouter"/>,
/// live metrics are pushed as events. One instance per WebView2.
/// </summary>
public sealed class WebBridge : IDisposable
{
    private readonly CoreWebView2 _webView;
    private readonly string _trustedOrigin;
    private readonly BridgeRouter _router = new();
    private readonly MonitoringService _monitoring;

    public WebBridge(CoreWebView2 webView, string trustedOrigin, ISpecsProvider specs, MonitoringService monitoring)
    {
        _webView = webView;
        _trustedOrigin = trustedOrigin;
        _monitoring = monitoring;

        _router.Register("specs.get", async () => await specs.GetAsync());
        new StorageBridge(new StorageSession(), new ShellRecycler(), new ShellLauncher(), PostEvent).Register(_router);

        _webView.WebMessageReceived += OnMessage;
        _monitoring.Updated += OnMetrics;
    }

    public void Dispose()
    {
        _monitoring.Updated -= OnMetrics;
        _webView.WebMessageReceived -= OnMessage;
    }

    private async void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        // Only the page we serve may talk to the engine, whatever got navigated to.
        if (!e.Source.StartsWith(_trustedOrigin, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var reply = await _router.HandleAsync(e.WebMessageAsJson);
        if (reply is not null)
        {
            _webView.PostWebMessageAsString(reply);
        }
    }

    private void OnMetrics(object? sender, EventArgs e)
    {
        if (_monitoring.Latest is { } snapshot)
        {
            PostEvent("metrics", snapshot);
        }
    }

    /// <summary>Events may come from scan threads; WebView2 only accepts messages on the UI thread.</summary>
    private void PostEvent(string type, object? payload)
    {
        var json = BridgeRouter.EventJson(type, payload);
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            _webView.PostWebMessageAsString(json);
        }
        else
        {
            dispatcher.BeginInvoke(() => _webView.PostWebMessageAsString(json));
        }
    }
}
