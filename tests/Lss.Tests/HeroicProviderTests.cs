using System.Text.Json;

namespace Lss.Tests;

/// <summary>
/// HeroicProvider legge la configurazione di Heroic da una cartella Home finta
/// dentro TestFolder. I formati dei file di Heroic non sono verificati su un PC
/// con Heroic (vedi PROGRESS.md, fase O): i test li scrivono come li descrive il piano.
/// </summary>
public sealed class HeroicProviderTests : IDisposable
{
    private readonly TestFolder folder = new();
    private readonly FakePlatform platform = new();

    private string Home => Path.Combine(folder.Root, "Home");
    private string Config => Path.Combine(Home, ".config", "heroic");
    private string Games => Path.Combine(folder.Root, "Games");

    public HeroicProviderTests()
    {
        platform.Folders = platform.Folders with { Home = Home };
    }

    public void Dispose() => folder.Dispose();

    private void WriteJson(string file, object value) => TestFolder.Write(file, JsonSerializer.Serialize(value));

    [Fact]
    public void WithoutConfigFolderThereIsNoAccount()
    {
        Assert.Empty(new HeroicProvider(platform).FindAccounts());
    }

    [Fact]
    public void ConfigFolderWithoutGamesIsNoAccount()
    {
        Directory.CreateDirectory(Config);

        Assert.Empty(new HeroicProvider(platform).FindAccounts());
    }

    [Fact]
    public void LegendaryGamesComeFromInstalledJson()
    {
        string install = Path.Combine(Games, "Hades");
        Directory.CreateDirectory(install);
        WriteJson(Path.Combine(Config, "legendaryConfig", "legendary", "installed.json"),
            new Dictionary<string, object> { ["Hades"] = new { app_name = "Hades", title = "Hades Titolo", install_path = install } });

        var provider = new HeroicProvider(platform);
        GameAccount account = provider.FindAccounts().Single();
        GameInfo game = provider.FindGames(account).Single();

        Assert.Equal("heroic", account.Provider);
        Assert.Equal(Config, account.Folder);
        Assert.Equal("heroic", game.Provider);
        Assert.Equal("legendary_Hades", game.Id);
        Assert.Equal("Hades Titolo", game.Name);
        Assert.Equal(install, game.InstallPath);
        Assert.Equal(SupportState.NoSaves, game.Support);
    }

    [Fact]
    public void ExecutableIsTheExecutableFieldInTheGameFolder()
    {
        string install = Path.Combine(Games, "Hades");
        Directory.CreateDirectory(install);
        WriteJson(Path.Combine(Config, "legendaryConfig", "legendary", "installed.json"),
            new Dictionary<string, object> { ["Hades"] = new { app_name = "Hades", title = "Hades", install_path = install, executable = "Hades.exe" } });

        var provider = new HeroicProvider(platform);
        GameInfo game = provider.FindGames(provider.FindAccounts().Single()).Single();

        Assert.Equal(Path.Combine(install, "Hades.exe"), provider.ExecutableOf(game));
    }

    [Fact]
    public void GogGamesGetTheirTitleFromTheStoreCache()
    {
        string install = Path.Combine(Games, "gioco");
        Directory.CreateDirectory(install);
        WriteJson(Path.Combine(Config, "gog_store", "installed.json"),
            new { installed = new[] { new { appName = "1207", install_path = install } } });
        WriteJson(Path.Combine(Config, "store_cache", "gog_library.json"),
            new { games = new[] { new { app_name = "1207", title = "The Witcher" } } });

        var provider = new HeroicProvider(platform);
        GameInfo game = provider.FindGames(provider.FindAccounts().Single()).Single();

        Assert.Equal("gog_1207", game.Id);
        Assert.Equal("The Witcher", game.Name);
    }

    [Fact]
    public void MissingInstallFolderIsSkipped()
    {
        WriteJson(Path.Combine(Config, "legendaryConfig", "legendary", "installed.json"),
            new Dictionary<string, object> { ["Assente"] = new { app_name = "Assente", title = "Assente", install_path = Path.Combine(Games, "Assente") } });
        TestFolder.Write(Path.Combine(Config, "nile_config", "nile", "installed.json"), "non è json");

        var provider = new HeroicProvider(platform);

        Assert.Empty(provider.FindGames(provider.FindAccounts().Single()));
    }

    [Fact]
    public void GogSavesBecomeAbsoluteRulesAndTheSearchFindsThem()
    {
        string install = Path.Combine(Games, "gioco");
        string saves = Path.Combine(folder.Root, "Saves", "Witcher");
        Directory.CreateDirectory(install);
        TestFolder.Write(Path.Combine(saves, "slot1.sav"), "partita");
        WriteJson(Path.Combine(Config, "gog_store", "installed.json"),
            new { installed = new[] { new { appName = "1207", install_path = install } } });
        WriteJson(Path.Combine(Config, "GamesConfig", "1207.json"),
            new Dictionary<string, object>
            {
                ["1207"] = new { gogSaves = new[] { new { name = "Salvataggi", location = saves } } }
            });

        var provider = new HeroicProvider(platform);
        GameAccount account = provider.FindAccounts().Single();
        GameInfo game = provider.FindGames(account).Single();

        SaveRule rule = provider.SaveRules(account, game).Single();
        Assert.Equal("Absolute", rule.Root);
        Assert.Equal(saves, rule.Path);
        Assert.Equal(SaveSource.Launcher, game.Source);
        Assert.Equal(SupportState.Supported, game.Support);
        Assert.Equal(saves, game.SaveFolder);
    }

    [Fact]
    public void EpicCloudFolderFromLegendaryMetadataIsAnyFolderOfTheGame()
    {
        string install = Path.Combine(Games, "Hades");
        TestFolder.Write(Path.Combine(install, "Saves", "42", "slot.sav"), "partita");
        WriteJson(Path.Combine(Config, "legendaryConfig", "legendary", "installed.json"),
            new Dictionary<string, object> { ["Hades"] = new { app_name = "Hades", title = "Hades", install_path = install } });
        WriteJson(Path.Combine(Config, "legendaryConfig", "legendary", "metadata", "Hades.json"),
            new { metadata = new { customAttributes = new { CloudSaveFolder = new { type = "STRING", value = "{InstallDir}/Saves/{EpicID}/" } } } });

        var provider = new HeroicProvider(platform);
        GameAccount account = provider.FindAccounts().Single();
        GameInfo game = provider.FindGames(account).Single();

        Assert.Equal(CloudState.Enabled, provider.GetCloudState(account, game));
        Assert.Equal(Path.Combine(install, "Saves", "42"), game.SaveFolder);
    }

    [Fact]
    public void WinePrefixIsTheFolderThatHoldsDriveC()
    {
        string install = Path.Combine(Games, "Hades");
        string prefix = Path.Combine(folder.Root, "Prefixes", "Hades");
        Directory.CreateDirectory(install);
        Directory.CreateDirectory(Path.Combine(prefix, "pfx", "drive_c"));
        WriteJson(Path.Combine(Config, "legendaryConfig", "legendary", "installed.json"),
            new Dictionary<string, object> { ["Hades"] = new { app_name = "Hades", title = "Hades", install_path = install } });
        WriteJson(Path.Combine(Config, "GamesConfig", "Hades.json"),
            new Dictionary<string, object> { ["Hades"] = new { winePrefix = prefix } });

        var provider = new HeroicProvider(platform);
        GameInfo game = provider.FindGames(provider.FindAccounts().Single()).Single();

        Assert.Equal(Path.Combine(prefix, "pfx"), provider.PrefixOf(game));
        Assert.Contains(Path.GetDirectoryName(prefix)!, provider.LauncherFolders(provider.FindAccounts().Single()));
    }

    [Fact]
    public void SideloadedGamesAreListed()
    {
        string install = Path.Combine(Games, "Mio");
        Directory.CreateDirectory(install);
        WriteJson(Path.Combine(Config, "sideload_apps", "library.json"),
            new { games = new[] { new { app_name = "mio1", title = "Il mio gioco", install = new { install_path = install } } } });

        var provider = new HeroicProvider(platform);
        GameInfo game = provider.FindGames(provider.FindAccounts().Single()).Single();

        Assert.Equal("sideload_mio1", game.Id);
        Assert.Equal("Il mio gioco", game.Name);
    }

    [Fact]
    public void HeroicGamesCannotBeStartedFromLssYet()
    {
        string install = Path.Combine(Games, "Hades");
        Directory.CreateDirectory(install);
        WriteJson(Path.Combine(Config, "legendaryConfig", "legendary", "installed.json"),
            new Dictionary<string, object> { ["Hades"] = new { app_name = "Hades", title = "Hades", install_path = install } });

        var provider = new HeroicProvider(platform);
        GameInfo game = provider.FindGames(provider.FindAccounts().Single()).Single();

        Assert.False(provider.CanLaunch(game));
        Assert.False(provider.Launch(game).Success);
        Assert.Empty(platform.OpenedUris);
    }
}
