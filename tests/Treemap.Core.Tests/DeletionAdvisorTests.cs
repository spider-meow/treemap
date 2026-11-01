using Treemap.Core.Storage.Cleanup;
using Xunit;

namespace Treemap.Core.Tests;

public class DeletionAdvisorTests
{
    private static DeletionAssessment Classify(string path, long size = 1000, Func<string, string, int?>? steam = null) =>
        DeletionAdvisor.Classify(path, size, steam);

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"C:\Windows")]
    [InlineData(@"C:\Windows\System32\drivers")]
    [InlineData(@"C:\Program Files")]
    [InlineData(@"C:\Program Files (x86)")]
    [InlineData(@"C:\ProgramData")]
    [InlineData(@"C:\Users")]
    [InlineData(@"C:\Users\arthu")]
    [InlineData(@"C:\Users\arthu\AppData")]
    [InlineData(@"C:\Users\arthu\AppData\Local")]
    [InlineData(@"C:\Users\arthu\AppData\Roaming")]
    [InlineData(@"C:\Users\arthu\Documents")]
    [InlineData(@"C:\Users\arthu\OneDrive")]
    [InlineData(@"C:\$Recycle.Bin")]
    [InlineData(@"C:\System Volume Information")]
    [InlineData(@"C:\pagefile.sys")]
    [InlineData(@"C:\hiberfil.sys")]
    [InlineData(@"C:\Program Files\WindowsApps\Some.App_1.0")]
    [InlineData(@"\\serveur\partage\dossier")]
    public void SystemAndPersonalRoots_AreBlocked(string path)
    {
        Assert.Equal(DeletionVerdict.Blocked, Classify(path).Verdict);
    }

    [Fact]
    public void Paths_AreNormalised_SoDotDotCannotEscapeAProtectedFolder()
    {
        Assert.Equal(DeletionVerdict.Blocked, Classify(@"C:\Users\arthu\Downloads\..\..\..\Windows\System32").Verdict);
        Assert.Equal(DeletionVerdict.Blocked, Classify(@"C:\WINDOWS\").Verdict);
    }

    [Theory]
    [InlineData(@"D:\Games\steamapps\common\Elden Ring")]
    [InlineData(@"D:\Games\steamapps\common\Elden Ring\Game\eldenring.exe")]
    [InlineData(@"C:\Program Files\Epic Games\Fortnite")]
    [InlineData(@"D:\XboxGames\Forza Horizon 5")]
    [InlineData(@"C:\Program Files\Mozilla Firefox")]
    [InlineData(@"C:\Program Files (x86)\Steam")]
    [InlineData(@"C:\Users\arthu\AppData\Local\Packages\Microsoft.FlightSimulator_8wekyb3d8bbwe\LocalCache\Packages\Official")]
    [InlineData(@"C:\Users\arthu\AppData\Roaming\Microsoft Flight Simulator\Packages\Official\Steam\fs-base")]
    public void InstalledGamesAndPrograms_GoThroughTheirUninstaller(string path)
    {
        var assessment = Classify(path);

        Assert.Equal(DeletionVerdict.UninstallOnly, assessment.Verdict);
        Assert.NotEmpty(assessment.Uninstall);
        Assert.All(assessment.Uninstall, h => Assert.True(DeletionAdvisor.IsSafeLauncherUri(h.Uri), h.Uri));
    }

    [Fact]
    public void SteamGame_UsesItsManifestAppId_AndFallsBackToTheLibrary()
    {
        var found = Classify(@"D:\Games\steamapps\common\Hades", steam: (library, game) =>
        {
            Assert.Equal(@"D:\Games\steamapps", library);
            Assert.Equal("Hades", game);
            return 1145360;
        });
        var notFound = Classify(@"D:\Games\steamapps\common\Hades", steam: (_, _) => null);

        Assert.Equal("steam://uninstall/1145360", Assert.Single(found.Uninstall).Uri);
        Assert.Equal("steam://nav/games", Assert.Single(notFound.Uninstall).Uri);
    }

    [Theory]
    [InlineData(@"D:\Games\steamapps")]
    [InlineData(@"D:\Games\steamapps\common")]
    [InlineData(@"D:\Games\steamapps\appmanifest_1145360.acf")]
    [InlineData(@"C:\Program Files\Epic Games")]
    [InlineData(@"D:\XboxGames")]
    public void WholeLibrariesAndLauncherFiles_AreBlocked(string path)
    {
        Assert.Equal(DeletionVerdict.Blocked, Classify(path).Verdict);
    }

    [Fact]
    public void FlightSimulator_AddOnsAreDeletable_ButTheGameItselfIsNot()
    {
        var packages = @"C:\Users\arthu\AppData\Local\Packages\Microsoft.FlightSimulator_8wekyb3d8bbwe\LocalCache\Packages";

        Assert.Equal(DeletionVerdict.Allowed, Classify(packages + @"\Community").Verdict);
        Assert.Equal(DeletionVerdict.Allowed, Classify(packages + @"\Community\some-airliner").Verdict);
        Assert.Equal(DeletionVerdict.Allowed, Classify(packages + @"\ROLLINGCACHE.CCC").Verdict);
        Assert.Equal(DeletionVerdict.UninstallOnly, Classify(packages + @"\Official").Verdict);
        Assert.Equal(DeletionVerdict.UninstallOnly, Classify(packages + @"\Official\OneStore\fs-base").Verdict);
    }

    [Fact]
    public void FlightSimulator_OfferstheLauncherThatMatchesTheInstall()
    {
        var store = Classify(@"C:\Users\a\AppData\Local\Packages\Microsoft.FlightSimulator_8wekyb3d8bbwe\LocalCache\Packages\Official");
        var steam = Classify(@"C:\Users\a\AppData\Roaming\Microsoft Flight Simulator\Packages\Official");
        var custom = Classify(@"E:\MSFS\Packages\Official");

        Assert.Equal(["windows"], store.Uninstall.Select(h => h.Launcher));
        Assert.Equal(["steam"], steam.Uninstall.Select(h => h.Launcher));
        Assert.Equal("steam://uninstall/1250410", steam.Uninstall[0].Uri);
        Assert.Equal(["steam", "windows"], custom.Uninstall.Select(h => h.Launcher));
    }

    [Theory]
    [InlineData(@"C:\Users\arthu\AppData\Local\Temp")]
    [InlineData(@"C:\Users\arthu\AppData\Local\Temp\build-123")]
    [InlineData(@"C:\dev\app\node_modules")]
    [InlineData(@"C:\Users\arthu\Downloads\setup.exe")]
    [InlineData(@"D:\Games\steamapps\shadercache\1145360")]
    public void KnownRecreatableItems_NeedOnlyTheStandardConfirmation(string path)
    {
        var assessment = Classify(path, size: 50L << 30);

        Assert.Equal(DeletionVerdict.Allowed, assessment.Verdict);
        Assert.Equal(ConfirmationLevel.Standard, assessment.Confirmation);
    }

    [Theory]
    [InlineData(@"C:\Users\arthu\Documents\Projets")]
    [InlineData(@"C:\Users\arthu\Pictures\2024")]
    [InlineData(@"C:\Users\arthu\Downloads")]
    [InlineData(@"C:\Users\arthu\AppData\Local\Discord")]
    [InlineData(@"C:\ProgramData\SomeVendor")]
    public void PersonalOrApplicationData_NeedsTheTypedConfirmation(string path)
    {
        var assessment = Classify(path);

        Assert.Equal(DeletionVerdict.Allowed, assessment.Verdict);
        Assert.Equal(ConfirmationLevel.Typed, assessment.Confirmation);
    }

    [Fact]
    public void UnknownItems_NeedTheTypedConfirmationOnlyWhenLarge()
    {
        Assert.Equal(ConfirmationLevel.Standard, Classify(@"D:\Stuff\old-backup", size: 2L << 30).Confirmation);
        Assert.Equal(ConfirmationLevel.Typed, Classify(@"D:\Stuff\old-backup", size: 12L << 30).Confirmation);
    }

    [Theory]
    [InlineData("ms-settings:appsfeatures", true)]
    [InlineData("steam://nav/games", true)]
    [InlineData("steam://uninstall/1250410", true)]
    [InlineData("com.epicgames.launcher://apps", true)]
    [InlineData("steam://uninstall/12;calc", false)]
    [InlineData("steam://run/1250410", false)]
    [InlineData("file:///C:/Windows/System32/cmd.exe", false)]
    [InlineData("https://example.com", false)]
    [InlineData("", false)]
    public void OnlyWhitelistedLauncherAddressesAreSafe(string uri, bool expected)
    {
        Assert.Equal(expected, DeletionAdvisor.IsSafeLauncherUri(uri));
    }

    [Fact]
    public void Assess_RefusesMissingItemsAndLinks_AndAllowsARealTemporaryFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "treemap-advisor-" + Guid.NewGuid().ToString("N"));
        var real = Path.Combine(root, "real");
        var link = Path.Combine(root, "link");
        Directory.CreateDirectory(real);
        try
        {
            Assert.Equal(DeletionVerdict.Allowed, DeletionAdvisor.Assess(real, 10).Verdict);
            Assert.Equal(DeletionVerdict.Blocked, DeletionAdvisor.Assess(Path.Combine(root, "missing"), 10).Verdict);

            try
            {
                Directory.CreateSymbolicLink(link, real);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                return; // Creating links needs developer mode or administrator: the rest was checked.
            }

            Assert.Equal(DeletionVerdict.Blocked, DeletionAdvisor.Assess(link, 10).Verdict);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SteamManifest_ReadsTheAppIdOfAnInstalledGame()
    {
        var steamapps = Path.Combine(Path.GetTempPath(), "treemap-steam-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(steamapps);
        try
        {
            File.WriteAllText(Path.Combine(steamapps, "appmanifest_1145360.acf"),
                "\"AppState\"\n{\n\t\"appid\"\t\t\"1145360\"\n\t\"installdir\"\t\t\"Hades\"\n}\n");

            Assert.Equal(1145360, SteamManifest.FindAppId(steamapps, "hades"));
            Assert.Null(SteamManifest.FindAppId(steamapps, "Unknown"));
            Assert.Null(SteamManifest.FindAppId(Path.Combine(steamapps, "nope"), "Hades"));
        }
        finally
        {
            Directory.Delete(steamapps, recursive: true);
        }
    }
}
