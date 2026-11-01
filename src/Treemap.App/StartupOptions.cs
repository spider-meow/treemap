namespace Treemap.App;

/// <summary>
/// Optional command-line switches, mostly handy while developing:
/// <c>--tab=components|storage|monitor</c> opens a tab, <c>--scan=PATH</c> starts a scan of PATH (and opens the storage tab).
/// Unknown arguments are ignored.
/// </summary>
public sealed record StartupOptions(int TabIndex = 0, string? ScanPath = null)
{
    private static readonly string[] TabNames = ["components", "storage", "monitor"];

    public static StartupOptions Parse(IEnumerable<string> args)
    {
        var tab = 0;
        string? scanPath = null;

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
        }

        return new StartupOptions(tab, scanPath);
    }
}
