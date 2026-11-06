using System.Text.Json;
using System.Text.Json.Serialization;

namespace Treemap.Core.Settings;

/// <summary>Keeps the user's settings in a small JSON file. A missing or damaged file simply means defaults.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // Several parts of the app (overlay, booster) keep their own section: each change is applied to what is on disk now,
    // so one never overwrites the other with a stale copy.
    private static readonly object Gate = new();

    private readonly string _path;

    public SettingsStore(string? path = null)
    {
        _path = path ?? DefaultPath;
    }

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "treemap", "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new AppSettings();
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Options) ?? new AppSettings();
            return settings with { Overlay = settings.Overlay.Normalized(), Booster = settings.Booster.Normalized() };
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    /// <summary>Reads the current settings, applies the change, saves and returns the result.</summary>
    public AppSettings Update(Func<AppSettings, AppSettings> change)
    {
        lock (Gate)
        {
            var updated = change(Load());
            updated = updated with { Overlay = updated.Overlay.Normalized(), Booster = updated.Booster.Normalized() };
            Save(updated);
            return updated;
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write next to the target then swap, so a crash mid-write never leaves a half-written settings file.
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
        File.Move(temp, _path, overwrite: true);
    }
}
