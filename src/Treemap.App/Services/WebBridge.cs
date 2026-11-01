using System.Windows;
using Microsoft.Web.WebView2.Core;
using Treemap.Core.Bridge;
using Treemap.Core.Specs;

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
        _router.Register("specs.copySummary", async () =>
        {
            Clipboard.SetText(SpecsSummary.ToPlainText(await specs.GetAsync()));
            return null;
        });

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
            _webView.PostWebMessageAsString(BridgeRouter.EventJson("metrics", snapshot));
        }
    }
}
