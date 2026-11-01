using Treemap.Core.Settings;

namespace Treemap.Core.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "treemap-settings-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private string FilePath => Path.Combine(_dir, "settings.json");

    [Fact]
    public void Load_WithNoFile_ReturnsDefaults()
    {
        var settings = new SettingsStore(FilePath).Load();

        Assert.False(settings.Overlay.Enabled);
        Assert.True(settings.Overlay.ShowFps);
        Assert.Equal(OverlayCorner.TopLeft, settings.Overlay.Corner);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = new SettingsStore(FilePath);
        var saved = new AppSettings
        {
            Overlay = new OverlaySettings
            {
                Enabled = true,
                ShowCpu = false,
                ShowVideoMemory = true,
                Corner = OverlayCorner.BottomRight,
                Opacity = 0.6,
            },
        };

        store.Save(saved);
        var loaded = store.Load();

        Assert.Equal(saved, loaded);
        Assert.Contains("BottomRight", File.ReadAllText(FilePath)); // readable, not a number
    }

    [Fact]
    public void Load_WithDamagedFile_ReturnsDefaultsInsteadOfThrowing()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ this is not json");

        Assert.Equal(new AppSettings(), new SettingsStore(FilePath).Load());
    }

    [Theory]
    [InlineData(0.0, 0.3)]
    [InlineData(5.0, 1.0)]
    [InlineData(0.7, 0.7)]
    public void Load_ClampsOpacity(double stored, double expected)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, $$"""{ "Overlay": { "Opacity": {{stored.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } }""");

        Assert.Equal(expected, new SettingsStore(FilePath).Load().Overlay.Opacity, precision: 6);
    }

    [Fact]
    public void Load_KeepsDefaultsForMissingFields()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "Overlay": { "Enabled": true } }""");

        var overlay = new SettingsStore(FilePath).Load().Overlay;

        Assert.True(overlay.Enabled);
        Assert.True(overlay.ShowFps);
        Assert.Equal(0.85, overlay.Opacity);
    }

    [Fact]
    public void Save_CreatesTheFolderAndLeavesNoTempFile()
    {
        new SettingsStore(FilePath).Save(new AppSettings());

        Assert.True(File.Exists(FilePath));
        Assert.False(File.Exists(FilePath + ".tmp"));
    }
}
