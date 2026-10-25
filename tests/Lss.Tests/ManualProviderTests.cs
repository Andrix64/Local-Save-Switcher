namespace Lss.Tests;

/// <summary>
/// I giochi aggiunti a mano: gioco.ini in una cartella per gioco, sotto la
/// cartella dei dati di prova (TestFolder), mai quella vera di LSS.
/// </summary>
[Collection("AppData")]
public sealed class ManualProviderTests : IDisposable
{
    private readonly TestFolder folder = new();
    private readonly FakePlatform platform = new();
    private readonly string savedRoot = AppData.Root;

    public ManualProviderTests()
    {
        AppData.Root = Path.Combine(folder.Root, "dati");
    }

    public void Dispose()
    {
        AppData.Root = savedRoot;
        folder.Dispose();
    }

    [Fact]
    public void AddWritesAGameIniAndReadsItBack()
    {
        string id = ManualGames.Add(AppData.Root, new ManualGame("Il mio gioco", @"C:\Giochi\gioco.exe", "-fullscreen", ""));

        Assert.Matches("^[0-9a-f]{8}$", id);
        Assert.True(File.Exists(Path.Combine(ManualGames.AccountFolder(AppData.Root), id, "gioco.ini")));

        var read = ManualGames.ReadAll(AppData.Root).Single();
        Assert.Equal(id, read.Id);
        Assert.Equal("Il mio gioco", read.Game.Name);
        Assert.Equal(@"C:\Giochi\gioco.exe", read.Game.Exe);
        Assert.Equal("-fullscreen", read.Game.Arguments);
    }

    [Fact]
    public void WithoutGamesThereIsNoAccount()
    {
        Assert.Empty(new ManualProvider(platform).FindAccounts());
    }

    [Fact]
    public void GamesComeFromTheirFiles()
    {
        string exe = Path.Combine(folder.Root, "Giochi", "gioco.exe");
        string id = ManualGames.Add(AppData.Root, new ManualGame("Gioco", exe, "", "/home/u/Prefissi/gioco"));

        var provider = new ManualProvider(platform);
        GameAccount account = provider.FindAccounts().Single();
        GameInfo game = provider.FindGames(account).Single();

        Assert.Equal("manual", account.Provider);
        Assert.Equal("manual", game.Provider);
        Assert.Equal(id, game.Id);
        Assert.Equal("Gioco", game.Name);
        Assert.Equal(Path.GetDirectoryName(exe), game.InstallPath);
        Assert.Equal(SupportState.NoSaves, game.Support);
        Assert.Equal("/home/u/Prefissi/gioco", provider.PrefixOf(game));
    }

    [Fact]
    public void ExecutableIsTheProgramGivenForTheGame()
    {
        string exe = Path.Combine(folder.Root, "Giochi", "gioco.exe");
        ManualGames.Add(AppData.Root, new ManualGame("Gioco", exe, "", ""));

        var provider = new ManualProvider(platform);
        GameInfo game = provider.FindGames(provider.FindAccounts().Single()).Single();

        Assert.Equal(exe, provider.ExecutableOf(game));
    }

    [Fact]
    public void LaunchStartsTheProgramWithItsArguments()
    {
        string exe = Path.Combine(folder.Root, "Giochi", "gioco.exe");
        ManualGames.Add(AppData.Root, new ManualGame("Gioco", exe, "-fullscreen", ""));

        var provider = new ManualProvider(platform);
        GameInfo game = provider.FindGames(provider.FindAccounts().Single()).Single();

        // Un .exe su Linux non si avvia da LSS: il test vale su ogni sistema.
        Assert.Equal(!OperatingSystem.IsLinux(), provider.CanLaunch(game));

        if (provider.CanLaunch(game))
        {
            Assert.True(provider.Launch(game).Success);
            Assert.Equal(exe + " -fullscreen in " + Path.GetDirectoryName(exe), platform.StartedPrograms.Single());
        }
    }

    [Fact]
    public void GameWithoutProgramCannotBeStarted()
    {
        ManualGames.Add(AppData.Root, new ManualGame("Senza programma", "", "", ""));

        var provider = new ManualProvider(platform);
        GameInfo game = provider.FindGames(provider.FindAccounts().Single()).Single();

        Assert.False(provider.CanLaunch(game));
        Assert.False(provider.Launch(game).Success);
        Assert.Empty(platform.StartedPrograms);
        Assert.Equal("", game.InstallPath);
    }

    [Fact]
    public void RemovedGameGoesToTheRemovedFolderAndIsNoLongerListed()
    {
        string id = ManualGames.Add(AppData.Root, new ManualGame("Via", "", "", ""));

        ManualGames.Remove(AppData.Root, id);

        Assert.Empty(ManualGames.ReadAll(AppData.Root));
        Assert.True(Directory.Exists(Path.Combine(AppData.Root, "manual", "eliminati")));
        Assert.Single(Directory.GetDirectories(Path.Combine(AppData.Root, "manual", "eliminati")));
    }

    [Fact]
    public void RemovingAnUnknownGameDoesNothing()
    {
        ManualGames.Remove(AppData.Root, "00000000");

        Assert.False(Directory.Exists(Path.Combine(AppData.Root, "manual", "eliminati")));
    }
}
