using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Treemap.App.Services;
using Treemap.Core.Bridge;
using Treemap.Core.Specs;

namespace Treemap.App;

/// <summary>
/// Hosts the web interface. The built interface is served from the <c>web</c> folder next to the executable
/// through a virtual host name (nothing listens on the network); <c>--dev</c> points at the Vite dev server instead.
/// </summary>
public partial class WebHostWindow : Window
{
    private const string VirtualHost = "treemap.app";
    private const string DevServer = "http://localhost:5173";

    private readonly ISpecsProvider _specs;
    private readonly MonitoringService _monitoring;
    private readonly bool _dev;
    private readonly OverlayController _overlay;
    private readonly GlobalHotkey _hotkey;
    private readonly BoosterService _booster;
    private WebBridge? _bridge;

    private static readonly string[] Pages = ["composants", "stockage", "moniteur", "jeu"];

    private readonly string _startPage;
    private readonly string? _scanPath;
    private readonly bool _view3d;

    public WebHostWindow(ISpecsProvider specs, MonitoringService monitoring, OverlayController overlay, GlobalHotkey hotkey, BoosterService booster, StartupOptions options)
    {
        InitializeComponent();
        _specs = specs;
        _monitoring = monitoring;
        _overlay = overlay;
        _hotkey = hotkey;
        _booster = booster;
        _dev = options.DevServer;
        _startPage = Pages[Math.Clamp(options.TabIndex, 0, Pages.Length - 1)];
        _scanPath = options.ScanPath;
        _view3d = options.View3d;
        Loaded += async (_, _) => await InitializeWebAsync();
        Closed += (_, _) => _bridge?.Dispose();
    }

    private async Task InitializeWebAsync()
    {
        var webRoot = Path.Combine(AppContext.BaseDirectory, "web");
        if (!_dev && !File.Exists(Path.Combine(webRoot, "index.html")))
        {
            Fail("L'interface web n'est pas empaquetée avec l'application.\nConstruisez-la (npm run build dans src/Treemap.Web) puis recompilez, ou lancez avec --dev.");
            return;
        }

        try
        {
            var userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "treemap", "webview");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userData);
            await Web.EnsureCoreWebView2Async(environment);
        }
        catch (Exception ex) when (ex is WebView2RuntimeNotFoundException or InvalidOperationException or IOException)
        {
            Fail($"WebView2 n'a pas pu démarrer : {ex.Message}");
            return;
        }

        var core = Web.CoreWebView2;
        Web.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0xF4, 0xF1, 0xE1);

        core.Settings.AreBrowserAcceleratorKeysEnabled = _dev;
        core.Settings.AreDefaultContextMenusEnabled = _dev;
        core.Settings.AreDevToolsEnabled = _dev;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;

        var origin = _dev ? DevServer : $"https://{VirtualHost}";
        core.SetVirtualHostNameToFolderMapping(VirtualHost, webRoot, CoreWebView2HostResourceAccessKind.Deny);

        // The interface is the only page: no popups, no navigation away from our own origin.
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.NavigationStarting += (_, e) =>
        {
            if (!e.Uri.StartsWith(origin, StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
            }
        };

        _bridge = new WebBridge(
            core, origin, _specs, _monitoring, _overlay, _booster,
            () => new HotkeyInfo(_hotkey.IsRegistered, GlobalHotkey.Description),
            _dev ? "--dev --tab=monitor" : "--tab=monitor");
        var hash = _scanPath is not null
            ? $"{_startPage}?scan={Uri.EscapeDataString(_scanPath)}"
            : _view3d ? $"{_startPage}?view=3d" : _startPage;
        core.Navigate($"{origin}/index.html#{hash}");
    }

    private void Fail(string message) =>
        MessageBox.Show(this, message, "treemap", MessageBoxButton.OK, MessageBoxImage.Warning);
}
