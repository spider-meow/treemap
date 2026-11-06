namespace Treemap.Core.Booster;

public sealed record RunningProcess(int Id, string Name, string? Path, string? WindowTitle, long MemoryBytes)
{
    /// <summary>Has a window the user can see (open or minimised): that is what "an application" means here.</summary>
    public bool HasWindow => !string.IsNullOrWhiteSpace(WindowTitle);
}

public enum ProcessRole
{
    /// <summary>A game, by its install folder or because the user added it.</summary>
    Game,

    /// <summary>A normal application the user may choose to close before playing.</summary>
    Closable,

    /// <summary>Never touched: Windows, security, drivers, game launchers and anti-cheat.</summary>
    Protected,

    /// <summary>This app.</summary>
    Self,
}

/// <summary>
/// Decides what the booster may do with a process. The list of protected names is deliberately broad: an application that is
/// not clearly a normal window the user opened stays out of reach.
/// </summary>
public static class ProcessRules
{
    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Windows itself
        "system", "idle", "registry", "memory compression", "smss", "csrss", "wininit", "winlogon", "services", "lsass", "lsaiso",
        "svchost", "dwm", "explorer", "fontdrvhost", "sihost", "taskhostw", "ctfmon", "searchhost", "searchindexer",
        "startmenuexperiencehost", "shellexperiencehost", "textinputhost", "runtimebroker", "applicationframehost", "systemsettings",
        "audiodg", "spoolsv", "wmiprvse", "dllhost", "conhost", "taskmgr", "msedgewebview2", "treemap.app",
        // Security
        "msmpeng", "nissrv", "securityhealthservice", "securityhealthsystray", "smartscreen",
        // Game launchers: the game needs them running
        "steam", "steamwebhelper", "steamservice", "epicgameslauncher", "epicwebhelper", "eadesktop", "eabackgroundservice", "ealauncher",
        "origin", "battle.net", "agent", "ubisoftconnect", "upc", "uplay", "galaxyclient", "goggalaxy", "riotclientservices",
        "riotclientux", "riotclientuxrender", "xboxpcapp", "gamingservices", "gamebar", "gamebarpresencewriter",
        // Anti-cheat
        "vgc", "vgtray", "easyanticheat", "easyanticheat_eos", "beservice", "battleye", "faceit", "esea",
        // Graphics driver and device tools games rely on
        "nvcontainer", "nvdisplay.container", "nvidia overlay", "nvidia share", "radeonsoftware", "amdrssrcext", "msiafterburner",
        "rtss", "rivatuner", "lghub", "icue", "synapse", "razer synapse 3",
    };

    /// <summary>Programs installed next to games (Steam library, game folders) that are tools, not games.</summary>
    private static readonly HashSet<string> NotGames = new(StringComparer.OrdinalIgnoreCase)
    {
        "wallpaper32", "wallpaper64", "wallpaperengine", "webwallpaper32", "webwallpaper64", "winrtutil32", "winrtutil64", "ui32",
        "steamvr", "vrserver", "vrmonitor", "vrcompositor", "vrdashboard", "vrstartup", "gamingrepairtool", "crashhandler", "crashreportclient",
        "unitycrashhandler64", "unitycrashhandler32", "dxsetup", "vc_redist.x64", "vcredist_x64", "unins000",
    };

    private static readonly string[] GameFolders =
    [
        @"\steamapps\common\", @"\Epic Games\", @"\XboxGames\", @"\GOG Galaxy\Games\", @"\GOG Games\", @"\Riot Games\",
        @"\EA Games\", @"\Origin Games\", @"\Ubisoft Game Launcher\games\", @"\Rockstar Games\",
    ];

    /// <summary>True for names that can never be put on a close list, whatever the process list says.</summary>
    public static bool IsProtectedName(string name) => ProtectedNames.Contains(Normalize(name));

    public static ProcessRole Classify(RunningProcess process, IReadOnlyCollection<string> knownGames, int selfProcessId, string windowsDirectory)
    {
        if (process.Id == selfProcessId)
        {
            return ProcessRole.Self;
        }

        var name = Normalize(process.Name);
        if (ProtectedNames.Contains(name) || string.IsNullOrEmpty(process.Path) || IsUnder(process.Path, windowsDirectory))
        {
            return ProcessRole.Protected;
        }

        var knownByUser = knownGames.Any(g => Normalize(g).Equals(name, StringComparison.OrdinalIgnoreCase));
        if (knownByUser || (InGameFolder(process.Path) && !NotGames.Contains(name)))
        {
            return ProcessRole.Game;
        }

        return process.HasWindow ? ProcessRole.Closable : ProcessRole.Protected;
    }

    public static string Normalize(string name) =>
        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;

    private static bool InGameFolder(string path) =>
        GameFolders.Any(f => path.Contains(f, StringComparison.OrdinalIgnoreCase));

    private static bool IsUnder(string path, string folder) =>
        !string.IsNullOrEmpty(folder) && path.StartsWith(folder.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
}
