namespace Treemap.App;

/// <summary>
/// Optional command-line switches, mostly handy while developing:
/// <c>--tab=components|storage|monitor</c> opens a screen, <c>--scan=PATH</c> starts a scan of PATH (and opens the storage screen),
/// <c>--dev</c> loads the interface from the Vite dev server instead of the built one.
/// Unknown arguments are ignored (<c>--web</c>, from the time the web interface was opt-in, included).
/// </summary>
public sealed record StartupOptions(int TabIndex = 0, string? ScanPath = null, bool DevServer = false)
{
    private static readonly string[] TabNames = ["components", "storage", "monitor"];

    public static StartupOptions Parse(IEnumerable<string> args)
    {
        var tab = 0;
        string? scanPath = null;
        var dev = false;

        foreach (var arg in args)
        {
            if (arg.StartsWith("--tab=", StringComparison.OrdinalIgnoreCase))
            {
                var index = Array.IndexOf(TabNames, arg["--tab=".Length..].ToLowerInvariant());
                if (index >= 0)
                {
                    tab = index;
                }
            }
            else if (arg.StartsWith("--scan=", StringComparison.OrdinalIgnoreCase))
            {
                var path = arg["--scan=".Length..].Trim('"');
                if (path.Length > 0)
                {
                    scanPath = path;
                    tab = Array.IndexOf(TabNames, "storage");
                }
            }
            else if (arg.Equals("--dev", StringComparison.OrdinalIgnoreCase))
            {
                dev = true;
            }
        }

        return new StartupOptions(tab, scanPath, dev);
    }
}
