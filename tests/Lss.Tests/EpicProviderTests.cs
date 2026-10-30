using System.Text;
using System.Text.Json;

namespace Lss.Tests;

/// <summary>
/// EpicProvider legge i manifest (*.item) e il catalogo di Epic da una
/// ProgramData finta, dentro TestFolder. Il formato dei file è quello del
/// launcher (non verificato su un PC con Epic).
/// </summary>
public sealed class EpicProviderTests : IDisposable
{
    private readonly TestFolder folder = new();
    private readonly FakePlatform platform = new();

    private string ProgramData => Path.Combine(folder.Root, "ProgramData");
    private string Manifests => Path.Combine(ProgramData, "Epic", "EpicGamesLauncher", "Data", "Manifests");
    private string Catalog => Path.Combine(ProgramData, "Epic", "EpicGamesLauncher", "Data", "Catalog", "catcache.bin");
    private string Library => Path.Combine(folder.Root, "Epic");

    public EpicProviderTests()
    {
        platform.Folders = platform.Folders with
        {
            ProgramData = ProgramData,
            LocalAppData = Path.Combine(folder.Root, "Local"),
            Home = Path.Combine(folder.Root, "Home"),
        };
    }

    public void Dispose() => folder.Dispose();

    // Scrive un manifest come lo scrive il launcher, con i campi usati da LSS.
    private void WriteItem(string file, string appName, string name, string location, bool incomplete = false, string launch = "") =>
        TestFolder.Write(Path.Combine(Manifests, file), JsonSerializer.Serialize(new
        {
            AppName = appName,
            DisplayName = name,
            InstallLocation = location,
            CatalogNamespace = "ns" + appName,
            CatalogItemId = "item" + appName,
            bIsIncompleteInstall = incomplete,
            LaunchExecutable = launch,
        }));

    // Scrive il catalogo (base64 di un array JSON) con il CloudSaveFolder dato per ogni CatalogItemId.
    private void WriteCatalog(params (string ItemId, string Value)[] entries)
    {
        string json = "[" + string.Join(",", entries.Select(entry =>
            "{\"id\":\"" + entry.ItemId + "\",\"customAttributes\":{\"CloudSaveFolder\":{\"type\":\"STRING\",\"value\":\""
            + entry.Value.Replace("\\", "\\\\") + "\"}}}")) + "]";

        TestFolder.Write(Catalog, Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void WithoutManifestsThereIsNoAccount()
    {
        Assert.Empty(new EpicProvider(platform).FindAccounts());
    }

    [Fact]
    public void WithoutProgramDataThereIsNothing()
    {
        platform.Folders = platform.Folders with { ProgramData = "" };

        var provider = new EpicProvider(platform);

        Assert.Empty(provider.FindAccounts());
        Assert.Empty(provider.LauncherFolders(new GameAccount("epic", "local", "", "", null)));
    }

    [Fact]
    public void AccountIsTheManifestsFolderWhenItHasAGame()
    {
        WriteItem("a.item", "Hades", "Hades", Path.Combine(Library, "Hades"));

        GameAccount account = new EpicProvider(platform).FindAccounts().Single();

        Assert.Equal("epic", account.Provider);
        Assert.Equal("local", account.Id);
        Assert.Equal(Manifests, account.Folder);
    }

    [Fact]
    public void IncompleteAndNamelessInstallsAreSkipped()
    {
        WriteItem("a.item", "Hades", "Hades", Path.Combine(Library, "Hades"));
        WriteItem("b.item", "Rotto", "Rotto", Path.Combine(Library, "Rotto"), incomplete: true);
        WriteItem("c.item", "", "Senza nome", Path.Combine(Library, "Senza"));
        TestFolder.Write(Path.Combine(Manifests, "d.item"), "non è json");

        var provider = new EpicProvider(platform);
        IReadOnlyList<GameInfo> games = provider.FindGames(provider.FindAccounts().Single());

        Assert.Equal(new[] { "Hades" }, games.Select(game => game.Id));
    }

    [Fact]
    public void GamesComeFromTheManifests()
    {
        string install = Path.Combine(Library, "Hades");
        WriteItem("a.item", "Hades", "Hades Nome", install);

        var provider = new EpicProvider(platform);
        GameInfo game = provider.FindGames(provider.FindAccounts().Single()).Single();

        Assert.Equal("epic", game.Provider);
        Assert.Equal("Hades", game.Id);
        Assert.Equal("Hades Nome", game.Name);
        Assert.Equal(install, game.InstallPath);
        Assert.Equal("Hades", game.InstallDir);
        Assert.Equal(Library, game.LibraryPath);
        Assert.Equal(SupportState.NoSaves, game.Support);
        Assert.Equal(SaveSource.None, game.Source);
    }

    [Fact]
    public void ExecutableIsLaunchExecutableInTheInstallFolder()
    {
        string install = Path.Combine(Library, "Fez");
        WriteItem("a.item", "Fez", "Fez", install, launch: "FEZ.exe");
        WriteItem("b.item", "Senza", "Senza", Path.Combine(Library, "Senza"));

        var provider = new EpicProvider(platform);
        IReadOnlyList<GameInfo> games = provider.FindGames(provider.FindAccounts().Single());

        Assert.Equal(Path.Combine(install, "FEZ.exe"), provider.ExecutableOf(games.Single(game => game.Id == "Fez")));
        Assert.Equal("", provider.ExecutableOf(games.Single(game => game.Id == "Senza")));
    }

    [Fact]
    public void GameWithoutCloudFolderIsSearchedOnTheDisk()
    {
        WriteItem("a.item", "Hades", "Hades", Path.Combine(Library, "Hades"));
        WriteCatalog(("itemAltro", "{AppData}/Altro/"));

        var provider = new EpicProvider(platform);
        GameAccount account = provider.FindAccounts().Single();
        GameInfo game = provider.FindGames(account).Single();

        Assert.Equal(CloudState.Unknown, provider.GetCloudState(account, game));
        Assert.Empty(provider.SaveRules(account, game));
        Assert.Equal(SupportState.NoSaves, game.Support);
    }

    [Fact]
    public void CloudFolderInTheGameInstallIsUsedAndFound()
    {
        // {InstallDir} vale su ogni sistema, così il test non dipende da Windows.
        string install = Path.Combine(Library, "Hades");
        WriteItem("a.item", "Hades", "Hades", install);
        WriteCatalog(("itemHades", "{InstallDir}/Saves/{EpicID}/"));
        TestFolder.Write(Path.Combine(install, "Saves", "42", "slot.sav"), "partita");

        var provider = new EpicProvider(platform);
        GameAccount account = provider.FindAccounts().Single();
        GameInfo game = provider.FindGames(account).Single();

        Assert.Equal(CloudState.Enabled, provider.GetCloudState(account, game));
        Assert.Equal(SaveSource.Launcher, game.Source);
        Assert.Equal(SupportState.Supported, game.Support);
        Assert.Equal(Path.Combine(install, "Saves", "42"), game.SaveFolder);
    }

    [Fact]
    public void AppDataCloudFolderIsFoundOnWindows()
    {
        if (!OperatingSystem.IsWindows()) return;

        string install = Path.Combine(Library, "Hades");
        WriteItem("a.item", "Hades", "Hades", install);
        WriteCatalog(("itemHades", "{AppData}/Hades/Saves/{EpicID}/"));
        TestFolder.Write(Path.Combine(folder.Root, "Local", "Hades", "Saves", "42", "slot.sav"), "partita");

        var provider = new EpicProvider(platform);
        GameInfo game = provider.FindGames(provider.FindAccounts().Single()).Single();

        Assert.Equal(Path.Combine(folder.Root, "Local", "Hades", "Saves", "42"), game.SaveFolder);
    }

    [Fact]
    public void LaunchUsesTheEpicAddress()
    {
        WriteItem("a.item", "Hades", "Hades", Path.Combine(Library, "Hades"));

        var provider = new EpicProvider(platform);
        GameInfo game = provider.FindGames(provider.FindAccounts().Single()).Single();

        Assert.True(provider.CanLaunch(game));
        Result result = provider.Launch(game);

        Assert.True(result.Success);
        Assert.Equal(new[] { "com.epicgames.launcher://apps/nsHades%3AitemHades%3AHades?action=launch&silent=true" },
            platform.OpenedUris);
    }

    [Fact]
    public void GameNotInTheManifestsCannotBeLaunched()
    {
        var provider = new EpicProvider(platform);

        var game = new GameInfo("epic", "Fantasma", "Fantasma", "", "", "", [], SupportState.NoSaves);

        Assert.False(provider.CanLaunch(game));
        Assert.False(provider.Launch(game).Success);
        Assert.Empty(platform.OpenedUris);
    }

    [Fact]
    public void RunningAndLauncherComeFromThePlatform()
    {
        string install = Path.Combine(Library, "Hades");
        WriteItem("a.item", "Hades", "Hades", install);
        var start = new DateTime(2026, 10, 25, 10, 0, 0);
        platform.Processes.Add((install, 77, start));
        platform.ProcessNames.Add("EpicGamesLauncher");

        var provider = new EpicProvider(platform);
        GameInfo game = provider.FindGames(provider.FindAccounts().Single()).Single();

        Assert.True(provider.CanDetectRunning(game));
        Assert.True(provider.IsGameRunning(game));
        Assert.Equal(start, provider.GameStartTime(game));
        Assert.True(provider.IsLauncherRunning());
    }

    [Fact]
    public void LauncherFoldersAreEpicAndTheLibraryFolders()
    {
        WriteItem("a.item", "Hades", "Hades", Path.Combine(Library, "Hades"));

        var provider = new EpicProvider(platform);
        GameAccount account = provider.FindAccounts().Single();
        provider.FindGames(account);

        Assert.Contains(Path.Combine(ProgramData, "Epic"), provider.LauncherFolders(account));
        Assert.Contains(Library, provider.LauncherFolders(account));
    }

    [Fact]
    public void WithoutGamesThereAreNoRulesOrPrefix()
    {
        var provider = new EpicProvider(platform);

        Assert.Empty(provider.SaveRules(new GameAccount("epic", "local", "", "", null),
            new GameInfo("epic", "x", "x", "", "", "", [], SupportState.NoSaves)));
        Assert.Equal("", provider.PrefixOf(new GameInfo("epic", "x", "x", "", "", "", [], SupportState.NoSaves)));
    }
}
