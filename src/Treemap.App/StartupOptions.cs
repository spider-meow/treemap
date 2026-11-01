namespace Treemap.App;

/// <summary>
/// Optional command-line switches, mostly handy while developing: <c>--tab=components|storage</c>.
/// Unknown arguments are ignored.
/// </summary>
public sealed record StartupOptions(int TabIndex = 0)
{
    private static readonly string[] TabNames = ["components", "storage"];

    public static StartupOptions Parse(IEnumerable<string> args)
    {
        var tab = 0;
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
        }

        return new StartupOptions(tab);
    }
}
