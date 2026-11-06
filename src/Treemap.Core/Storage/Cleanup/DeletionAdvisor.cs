using System.Text.RegularExpressions;

namespace Treemap.Core.Storage.Cleanup;

public enum DeletionVerdict
{
    /// <summary>Can be sent to the Recycle Bin.</summary>
    Allowed,

    /// <summary>Installed software: deleting the folder would leave a broken install, the launcher's uninstaller must do it.</summary>
    UninstallOnly,

    /// <summary>Never offered for deletion (system files, drive roots, personal library roots...).</summary>
    Blocked,
}

public enum ConfirmationLevel
{
    /// <summary>A confirm button after the preview.</summary>
    Standard,

    /// <summary>The user must also type <see cref="DeletionAdvisor.ConfirmationPhrase"/>: personal data or very large items.</summary>
    Typed,
}

/// <summary>A way to start the right uninstaller. <see cref="Uri"/> is only ever one of the whitelisted shapes of <see cref="DeletionAdvisor.IsSafeLauncherUri"/>.</summary>
public sealed record UninstallHint(string Launcher, string Label, string Uri);

public sealed record DeletionAssessment(
    string Path,
    DeletionVerdict Verdict,
    ConfirmationLevel Confirmation,
    string Title,
    string Note,
    IReadOnlyList<UninstallHint> Uninstall);

/// <summary>
/// Decides, for one path, whether the app may offer to delete it and how carefully. This is the single source of truth:
/// the interface only displays it, and the deletion itself asks again right before touching anything.
/// Deleting always means the Recycle Bin, so everything allowed is reversible unless Windows cannot recycle it.
/// </summary>
public static partial class DeletionAdvisor
{
    public const string ConfirmationPhrase = "SUPPRIMER";

    private const long LargeItemBytes = 10L << 30;
    private const int FlightSimulatorSteamAppId = 1250410;

    private static readonly string[] PersonalFolders =
        ["Documents", "Desktop", "Pictures", "Videos", "Music", "Saved Games", "Contacts", "Favorites", "Links", "Searches", "3D Objects"];

    private static readonly string[] ReservedRootFolders =
        ["$Recycle.Bin", "System Volume Information", "Recovery", "Boot", "Config.Msi", "$WinREAgent", "$Windows.~BT", "$Windows.~WS"];

    private static readonly string[] SystemFiles = ["pagefile.sys", "hiberfil.sys", "swapfile.sys"];

    private static readonly UninstallHint WindowsApps =
        new("windows", "Ouvrir « Applications installées »", "ms-settings:appsfeatures");

    /// <summary>Looks at the disk (does it exist, is it a fixed drive, is it a link) then applies <see cref="Classify"/>.</summary>
    public static DeletionAssessment Assess(string path, long sizeBytes)
    {
        string full;
        try
        {
            full = Normalize(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Refuse(path, "Chemin invalide", "Ce chemin n'est pas valide.");
        }

        if (Probe(full) is { } problem)
        {
            return problem;
        }

        return Classify(full, sizeBytes, SteamManifest.FindAppId);
    }

    /// <summary>
    /// The rules alone, without touching the disk. <paramref name="steamAppId"/> maps (steamapps folder, install folder name)
    /// to a Steam app id when the game's manifest is found.
    /// </summary>
    public static DeletionAssessment Classify(string path, long sizeBytes, Func<string, string, int?>? steamAppId = null)
    {
        var full = Normalize(path);
        var root = Path.GetPathRoot(full) ?? string.Empty;
        var parts = full[root.Length..].Split('\\', StringSplitOptions.RemoveEmptyEntries);

        if (root.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return Refuse(full, "Emplacement réseau", "Seuls les disques fixes de ce PC sont gérés : ailleurs, il n'y a pas de corbeille pour récupérer.");
        }

        if (parts.Length == 0)
        {
            return Refuse(full, "Racine d'un disque", "On ne supprime pas un disque entier depuis ici.");
        }

        return Game(full, root, parts, steamAppId)
            ?? SystemPaths(full, parts)
            ?? Profile(full, parts)
            ?? Generic(full, sizeBytes);
    }

    /// <summary>Only these launcher addresses may ever be started by the app.</summary>
    public static bool IsSafeLauncherUri(string uri) =>
        uri == "ms-settings:appsfeatures"
        || uri == "steam://nav/games"
        || uri == "com.epicgames.launcher://apps"
        || SteamUninstallUri().IsMatch(uri);

    // ---- games and add-ons -------------------------------------------------------------------------------------

    private static DeletionAssessment? Game(string full, string root, string[] parts, Func<string, string, int?>? steamAppId)
    {
        var steam = IndexOf(parts, "steamapps");
        if (steam >= 0)
        {
            return SteamRule(full, root, parts, steam, steamAppId);
        }

        // "Packages" also names the Windows app-data folder, so look for the one that holds the simulator's content.
        for (var packages = 0; packages + 1 < parts.Length; packages++)
        {
            if (!Is(parts[packages], "Packages"))
            {
                continue;
            }

            if (Is(parts[packages + 1], "Community"))
            {
                return packages + 2 >= parts.Length
                    ? Allow(full, "Add-ons de Microsoft Flight Simulator",
                        "Tout le contenu additionnel du simulateur (avions, scènes, livrées). Le jeu lui-même reste intact ; le dossier est recréé au lancement et les add-ons sont à réinstaller.")
                    : Allow(full, "Add-on de Microsoft Flight Simulator",
                        "Contenu additionnel installé à la main : le retirer ne touche pas au jeu.");
            }

            if (Is(parts[packages + 1], "Official"))
            {
                return Uninstall(full, "Microsoft Flight Simulator",
                    "Ce sont les fichiers du jeu lui-même (100 Go et plus). En supprimer une partie à la main casse l'installation : désinstalle le jeu depuis Steam ou depuis Windows.",
                    FlightSimulatorHints(parts));
            }

            if (parts.Length == packages + 2 && parts[packages + 1].EndsWith(".CCC", StringComparison.OrdinalIgnoreCase))
            {
                return Allow(full, "Cache de Microsoft Flight Simulator", "Fichier de cache recréé automatiquement par le jeu.");
            }
        }

        var epic = IndexOf(parts, "Epic Games");
        if (epic >= 0)
        {
            return parts.Length == epic + 1
                ? Refuse(full, "Bibliothèque Epic Games", "Désinstalle les jeux un par un depuis le lanceur Epic.")
                : Uninstall(full, parts[epic + 1], "Jeu installé par Epic Games : la désinstallation se fait depuis le lanceur, qui nettoie aussi ses propres traces.",
                    [new UninstallHint("epic", "Ouvrir le lanceur Epic Games", "com.epicgames.launcher://apps")]);
        }

        var xbox = IndexOf(parts, "XboxGames");
        if (xbox >= 0)
        {
            return parts.Length == xbox + 1
                ? Refuse(full, "Bibliothèque Xbox", "Désinstalle les jeux un par un depuis Windows.")
                : Uninstall(full, parts[xbox + 1], "Jeu installé par l'application Xbox : désinstalle-le depuis Windows pour ne pas casser son installation.", [WindowsApps]);
        }

        return null;
    }

    private static DeletionAssessment SteamRule(string full, string root, string[] parts, int steam, Func<string, string, int?>? steamAppId)
    {
        if (parts.Length == steam + 1)
        {
            return Refuse(full, "Bibliothèque Steam", "Désinstalle les jeux un par un depuis Steam.");
        }

        var area = parts[steam + 1];
        if (Is(area, "common"))
        {
            if (parts.Length == steam + 2)
            {
                return Refuse(full, "Jeux Steam", "Désinstalle les jeux un par un depuis Steam.");
            }

            var game = parts[steam + 2];
            var steamapps = root + string.Join('\\', parts[..(steam + 1)]);
            var id = steamAppId?.Invoke(steamapps, game);
            UninstallHint hint = id is { } appId
                ? new("steam", "Désinstaller dans Steam", $"steam://uninstall/{appId}")
                : new("steam", "Ouvrir la bibliothèque Steam", "steam://nav/games");

            return Uninstall(full, game, "Jeu installé par Steam : la désinstallation depuis Steam supprime ses fichiers sans fausser la bibliothèque.", [hint]);
        }

        if (Is(area, "shadercache") || Is(area, "downloading") || Is(area, "temp"))
        {
            return Allow(full, "Cache de Steam", "Fichiers de travail de Steam (shaders compilés, téléchargements incomplets) : recréés au besoin.");
        }

        return Refuse(full, "Fichier de gestion de Steam", "Steam s'en sert pour savoir quels jeux sont installés.");
    }

    private static List<UninstallHint> FlightSimulatorHints(string[] parts)
    {
        var steam = new UninstallHint("steam", "Désinstaller dans Steam", $"steam://uninstall/{FlightSimulatorSteamAppId}");
        var store = new UninstallHint("windows", "Ouvrir « Applications installées »", "ms-settings:appsfeatures");

        var isStore = parts.Any(p => p.StartsWith("Microsoft.FlightSimulator_", StringComparison.OrdinalIgnoreCase));
        var isSteam = parts.Any(p => Is(p, "Microsoft Flight Simulator"));

        return isStore && !isSteam ? [store] : isSteam && !isStore ? [steam] : [steam, store];
    }

    // ---- system ------------------------------------------------------------------------------------------------

    private static DeletionAssessment? SystemPaths(string full, string[] parts)
    {
        var top = parts[0];

        if (Is(top, "Windows"))
        {
            return Refuse(full, "Dossier de Windows", "Windows ne démarrerait plus. Pour récupérer de la place, utilise Nettoyage de disque.");
        }

        if (ReservedRootFolders.Any(r => Is(top, r)))
        {
            return Refuse(full, "Dossier réservé au système", "Géré par Windows (corbeille, restauration système, récupération) : il ne se supprime pas à la main.");
        }

        if (parts.Length == 1 && SystemFiles.Any(f => Is(top, f)))
        {
            return Refuse(full, "Fichier système", "Géré par Windows. Les réglages d'hibernation et de mémoire virtuelle se changent dans Windows, pas en supprimant le fichier.");
        }

        if (parts.Length == 1 && (Is(top, "Program Files") || Is(top, "Program Files (x86)") || Is(top, "ProgramData") || Is(top, "Users")))
        {
            return Refuse(full, "Dossier système", "Contient les programmes et les données de tous les utilisateurs.");
        }

        if (Is(top, "Program Files") || Is(top, "Program Files (x86)"))
        {
            return parts.Length >= 2 && (Is(parts[1], "WindowsApps") || Is(parts[1], "ModifiableWindowsApps"))
                ? Refuse(full, "Applications Windows", "Géré par le Microsoft Store : désinstalle depuis Windows.")
                : Uninstall(full, parts.Length >= 2 ? parts[1] : top,
                    "Programme installé : le supprimer à la main le laisse cassé dans la liste des applications. Désinstalle-le depuis Windows.", [WindowsApps]);
        }

        if (Is(top, "ProgramData") && parts.Length >= 2)
        {
            return Allow(full, "Données partagées de programmes", "Les programmes concernés peuvent perdre leurs réglages ou leur licence.", ConfirmationLevel.Typed);
        }

        return null;
    }

    // ---- user profile --------------------------------------------------------------------------------------------

    private static DeletionAssessment? Profile(string full, string[] parts)
    {
        if (parts.Any(p => Is(p, "node_modules")))
        {
            return Allow(full, "Dépendances JavaScript", "Recréées par npm install : rien n'est perdu.");
        }

        if (!Is(parts[0], "Users") || parts.Length < 2)
        {
            return null;
        }

        if (parts.Length == 2)
        {
            return Refuse(full, "Dossier d'un utilisateur", "Contient tous les documents et réglages de cette session.");
        }

        var area = parts[2];

        if (Is(area, "AppData"))
        {
            if (parts.Length == 3 || (parts.Length == 4 && IsAppDataRoot(parts[3])))
            {
                return Refuse(full, "Données des applications", "Chaque programme y range ses réglages : supprimer ce dossier réinitialise tout.");
            }

            if (parts.Length >= 5 && Is(parts[3], "Local") && Is(parts[4], "Temp"))
            {
                return Allow(full, "Fichiers temporaires", "Fichiers de travail des programmes : sans risque une fois les programmes fermés.");
            }

            return Allow(full, "Données d'une application",
                "Peut contenir des réglages, des comptes connectés ou des sauvegardes. À éviter sauf si tu sais ce que c'est.", ConfirmationLevel.Typed);
        }

        if (PersonalFolders.Any(f => Is(area, f)) || area.StartsWith("OneDrive", StringComparison.OrdinalIgnoreCase))
        {
            return parts.Length == 3
                ? Refuse(full, "Dossier personnel", "C'est un dossier de ta bibliothèque Windows : supprime plutôt ce qu'il contient.")
                : Allow(full, "Fichiers personnels", "Tes propres fichiers : vérifie bien la liste avant de confirmer.", ConfirmationLevel.Typed);
        }

        if (Is(area, "Downloads"))
        {
            return parts.Length == 3
                ? Allow(full, "Téléchargements", "Tout le dossier : installeurs, archives et documents reçus.", ConfirmationLevel.Typed)
                : Allow(full, "Téléchargement", "Un fichier ou dossier téléchargé.");
        }

        return null;
    }

    private static bool IsAppDataRoot(string name) =>
        Is(name, "Local") || Is(name, "Roaming") || Is(name, "LocalLow");

    private static DeletionAssessment Generic(string full, long sizeBytes) =>
        sizeBytes >= LargeItemBytes
            ? Allow(full, "Gros élément", "Ce n'est pas un élément que treemap reconnaît, mais il est volumineux : confirme en connaissance de cause.", ConfirmationLevel.Typed)
            : Allow(full, "Élément", "Aucune particularité connue pour cet élément.");

    // ---- disk probing ------------------------------------------------------------------------------------------

    private static DeletionAssessment? Probe(string full)
    {
        var root = Path.GetPathRoot(full) ?? string.Empty;

        try
        {
            if (!root.StartsWith(@"\\", StringComparison.Ordinal) && new DriveInfo(root).DriveType != DriveType.Fixed)
            {
                return Refuse(full, "Disque non fixe", "Sur un disque amovible ou réseau, Windows supprime sans passer par la corbeille : treemap ne le propose pas.");
            }

            var isDirectory = Directory.Exists(full);
            if (!isDirectory && !File.Exists(full))
            {
                return Refuse(full, "Introuvable", "Cet élément n'existe plus : le disque a changé depuis l'analyse.");
            }

            if (File.GetAttributes(full).HasFlag(FileAttributes.ReparsePoint))
            {
                return Refuse(full, "Lien", "Raccourci vers un autre emplacement (jonction ou lien symbolique) : treemap ne le suit pas et ne le supprime pas.");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Refuse(full, "Illisible", "Windows refuse l'accès à cet élément.");
        }

        return null;
    }

    // ---- small helpers -----------------------------------------------------------------------------------------

    private static string Normalize(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? string.Empty;
        return full.Length > root.Length ? full.TrimEnd('\\') : full;
    }

    private static bool Is(string segment, string name) => segment.Equals(name, StringComparison.OrdinalIgnoreCase);

    private static int IndexOf(string[] parts, string name) => Array.FindIndex(parts, p => Is(p, name));

    private static DeletionAssessment Allow(string path, string title, string note, ConfirmationLevel confirmation = ConfirmationLevel.Standard) =>
        new(path, DeletionVerdict.Allowed, confirmation, title, note, []);

    private static DeletionAssessment Refuse(string path, string title, string note) =>
        new(path, DeletionVerdict.Blocked, ConfirmationLevel.Standard, title, note, []);

    private static DeletionAssessment Uninstall(string path, string title, string note, IReadOnlyList<UninstallHint> hints) =>
        new(path, DeletionVerdict.UninstallOnly, ConfirmationLevel.Standard, title, note, hints);

    [GeneratedRegex(@"^steam://uninstall/\d{1,10}$")]
    private static partial Regex SteamUninstallUri();
}
