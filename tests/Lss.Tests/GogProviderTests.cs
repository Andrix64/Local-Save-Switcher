namespace Lss.Tests;

/// <summary>
/// GogProvider legge i giochi dal registro (FakePlatform) e le cartelle dal disco
/// dentro TestFolder. I formati del registro di GOG non sono stati verificati su
/// un PC con GOG (vedi PROGRESS.md, fase O).
/// </summary>
public sealed class GogProviderTests : IDisposable
{
    private const string GamesKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\GOG.com\Games";

    private readonly TestFolder folder = new();
    private readonly FakePlatform platform = new();

    private string ProgramData => Path.Combine(folder.Root, "ProgramData");
    private string Library => Path.Combine(folder.Root, "GOG Games");

    public GogProviderTests()
    {
        platform.Folders = platform.Folders with { ProgramData = ProgramData };
    }

    public void Dispose() => folder.Dispose();

    // Una voce del registro come la scrive GOG (la cartella del gioco non si crea).
    private void Register(string id, string name, string installFolder, string exe)
    {
        string key = GamesKey + @"\" + id;

        if (!platform.RegistryKeys.TryGetValue(GamesKey, out List<string>? ids))
        {
            ids = [];
            platform.RegistryKeys[GamesKey] = ids;
        }

        ids.Add(id);
        platform.RegistryValues[(key, "gameName")] = name;
        platform.RegistryValues[(key, "path")] = installFolder;
        platform.RegistryValues[(key, "exe")] = exe;
    }

    // Una voce del registro con la cartella del gioco sul disco.
    private void AddGame(string id, string name, string installFolder, string exe)
    {
        Directory.CreateDirectory(installFolder);
        Register(id, name, installFolder, exe);
    }

    [Fact]
    public void WithoutRegistryThereIsNoAccount()
    {
        Assert.Empty(new GogProvider(platform).FindAccounts());
    }

    [Fact]
    public void GamesComeFromTheRegistryAndTheirFolderMustExist()
    {
        string install = Path.Combine(Library, "The Witcher");
        AddGame("1207658930", "The Witcher", install, Path.Combine(install, "witcher.exe"));
        Register("1000", "Non c'è più", Path.Combine(Library, "Assente"), "");

        var provider = new GogProvider(platform);
        GameAccount account = provider.FindAccounts().Single();
        GameInfo game = provider.FindGames(account).Single();

        Assert.Equal("gog", account.Provider);
        Assert.Equal("gog", game.Provider);
        Assert.Equal("1207658930", game.Id);
        Assert.Equal("The Witcher", game.Name);
        Assert.Equal(install, game.InstallPath);
        Assert.Equal("The Witcher", game.InstallDir);
        Assert.Equal(Library, game.LibraryPath);
    }

    [Fact]
    public void GameNameFallsBackToTheFolderName()
    {
        string install = Path.Combine(Library, "Cartella");
        AddGame("2000", "", install, "");

        var provider = new GogProvider(platform);
        GameInfo game = provider.FindGames(provider.FindAccounts().Single()).Single();

        Assert.Equal("Cartella", game.Name);
    }

    [Fact]
    public void GogGamesAlwaysUseTheSearchForTheirSaves()
    {
        string install = Path.Combine(Library, "Gioco");
        AddGame("3000", "Gioco", install, Path.Combine(install, "gioco.exe"));

        var provider = new GogProvider(platform);
        GameAccount account = provider.FindAccounts().Single();
        GameInfo game = provider.FindGames(account).Single();

        // GOG non dà regole, né cloud: il gioco passa dalla ricerca (fase M).
        Assert.Empty(provider.SaveRules(account, game));
        Assert.Empty(provider.RootOverrides(account, game));
        Assert.Equal(CloudState.Unknown, provider.GetCloudState(account, game));
        Assert.Equal(SupportState.NoSaves, game.Support);
        Assert.Equal(SaveSource.None, game.Source);
    }

    [Fact]
    public void LaunchStartsTheProgramInTheGameFolder()
    {
        string install = Path.Combine(Library, "Gioco");
        string exe = Path.Combine(install, "gioco.exe");
        AddGame("3000", "Gioco", install, exe);

        var provider = new GogProvider(platform);
        GameInfo game = provider.FindGames(provider.FindAccounts().Single()).Single();

        Assert.True(provider.CanLaunch(game));
        Assert.True(provider.Launch(game).Success);
        Assert.Single(platform.StartedPrograms);
        Assert.StartsWith(exe, platform.StartedPrograms[0], StringComparison.Ordinal);
        Assert.EndsWith(install, platform.StartedPrograms[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutableIsTheRegistryExeInTheGameFolder()
    {
        string install = Path.Combine(Library, "Gioco");
        AddGame("1207", "Gioco", install, "gioco.exe");

        var provider = new GogProvider(platform);
        GameInfo game = provider.FindGames(provider.FindAccounts().Single()).Single();

        Assert.Equal(Path.Combine(install, "gioco.exe"), provider.ExecutableOf(game));
    }

    [Fact]
    public void GameWithoutProgramCannotBeLaunched()
    {
        string install = Path.Combine(Library, "Gioco");
        AddGame("3000", "Gioco", install, "");

        var provider = new GogProvider(platform);
        GameInfo game = provider.FindGames(provider.FindAccounts().Single()).Single();

        Assert.False(provider.CanLaunch(game));
        Assert.False(provider.Launch(game).Success);
        Assert.Empty(platform.StartedPrograms);
    }

    [Fact]
    public void RunningAndLauncherComeFromThePlatform()
    {
        string install = Path.Combine(Library, "Gioco");
        AddGame("3000", "Gioco", install, Path.Combine(install, "gioco.exe"));
        var start = new DateTime(2026, 10, 25, 9, 30, 0);
        platform.Processes.Add((install, 12, start));
        platform.ProcessNames.Add("GalaxyClient");

        var provider = new GogProvider(platform);
        GameInfo game = provider.FindGames(provider.FindAccounts().Single()).Single();

        Assert.True(provider.IsGameRunning(game));
        Assert.Equal(start, provider.GameStartTime(game));
        Assert.True(provider.IsLauncherRunning());
    }

    [Fact]
    public void LauncherFoldersAreGogAndTheGameFolders()
    {
        string install = Path.Combine(Library, "Gioco");
        AddGame("3000", "Gioco", install, "");

        var provider = new GogProvider(platform);
        GameAccount account = provider.FindAccounts().Single();
        provider.FindGames(account);

        Assert.Contains(Path.Combine(ProgramData, "GOG.com"), provider.LauncherFolders(account));
        Assert.Contains(Library, provider.LauncherFolders(account));
    }
}
