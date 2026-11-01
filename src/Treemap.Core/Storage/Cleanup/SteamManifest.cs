using System.Text.RegularExpressions;

namespace Treemap.Core.Storage.Cleanup;

/// <summary>Finds the Steam app id of an installed game by reading the <c>appmanifest_*.acf</c> files of a library.</summary>
public static partial class SteamManifest
{
    public static int? FindAppId(string steamappsFolder, string installFolderName)
    {
        try
        {
            foreach (var manifest in Directory.EnumerateFiles(steamappsFolder, "appmanifest_*.acf"))
            {
                var text = File.ReadAllText(manifest);
                var install = InstallDir().Match(text);
                if (!install.Success || !install.Groups[1].Value.Equals(installFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var id = AppId().Match(text);
                if (id.Success && int.TryParse(id.Groups[1].Value, out var appId))
                {
                    return appId;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // No manifest readable: the caller falls back to opening the Steam library.
        }

        return null;
    }

    [GeneratedRegex("\"appid\"\\s+\"(\\d+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex AppId();

    [GeneratedRegex("\"installdir\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex InstallDir();
}
