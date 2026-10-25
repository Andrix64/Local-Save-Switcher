using System.Text;

namespace Lss.Tests;

/// <summary>
/// SaveLookup attraverso SteamProvider.FindGames: regole di appinfo.vdf,
/// prefisso Proton, cartella scelta. Dati finti in una cartella temporanea.
/// </summary>
public sealed class SaveLookupTests : IDisposable
{
    private readonly TestFolder folder = new();
    private readonly FakePlatform platform = new();
    private readonly string savedDataRoot = AppData.Root;

    public SaveLookupTests()
    {
        AppData.Root = folder.Data;
        platform.SteamRoots.Add(SteamRoot);
    }

    public void Dispose()
    {
        AppData.Root = savedDataRoot;
        folder.Dispose();
    }

    private string SteamRoot => Path.Combine(folder.Root, "Steam");
    private string UserData => Path.Combine(SteamRoot, "userdata");
    private string Prefix => Path.Combine(SteamRoot, "steamapps", "compatdata", "105600", "pfx");
    private string Steamuser => Path.Combine(Prefix, "drive_c", "users", "steamuser");

    private static string Manifest(string appId, string name) =>
        $"\"AppState\"\n{{\n\t\"appid\"\t\t\"{appId}\"\n\t\"name\"\t\t\"{name}\"\n\t\"installdir\"\t\t\"{name}\"\n}}\n";

    /// <summary>
    /// appinfo.vdf con una regola per il gioco 105600: una cartella in AppData\Local.
    /// </summary>
    private static byte[] RuleFor105600() => SteamAppInfoTests.AppInfo(true,
        (105600u, (w, t) => SteamAppInfoTests.Block(w, t, "appinfo",
            app => SteamAppInfoTests.Block(w, t, "ufs",
                ufs => SteamAppInfoTests.Block(w, t, "savefiles",
                    files => SteamAppInfoTests.Block(w, t, "0", file =>
                    {
                        SteamAppInfoTests.Value(w, t, "root", "WinAppDataLocal");
                        SteamAppInfoTests.Value(w, t, "path", "Sandfall/Saved");
                        SteamAppInfoTests.Value(w, t, "pattern", "*");
                        SteamAppInfoTests.Value(w, t, "recursive", "1");
                    }))))));

    /// <summary>
    /// Account 111 con il gioco 105600 installato e Steam Cloud attivo
    /// (solo remotecache.vdf in userdata).
    /// </summary>
    private void InstallGame(string appId, string name)
    {
        TestFolder.Write(Path.Combine(SteamRoot, "steamapps", "appmanifest_" + appId + ".acf"), Manifest(appId, name));
        TestFolder.Write(Path.Combine(UserData, "111", appId, "remotecache.vdf"), "cache");
    }

    private IReadOnlyList<GameInfo> Games()
    {
        var provider = new SteamProvider(platform);
        GameAccount account = provider.FindAccounts().Single();

        return provider.FindGames(account);
    }

    [Fact]
    public void ProtonSaveFolderIsFoundThroughTheRule()
    {
        if (!OperatingSystem.IsLinux()) return;

        InstallGame("105600", "Sandfall");
        Directory.CreateDirectory(Path.Combine(SteamRoot, "appcache"));
        File.WriteAllBytes(Path.Combine(SteamRoot, "appcache", "appinfo.vdf"), RuleFor105600());
        string saves = Path.Combine(Steamuser, "AppData", "Local", "Sandfall", "Saved");
        TestFolder.Write(Path.Combine(saves, "slot.sav"), "x");
        Directory.CreateDirectory(Path.Combine(Steamuser, "Documents"));

        GameInfo game = Games().Single(g => g.Id == "105600");

        Assert.Equal(saves, game.SaveFolder);
        Assert.Equal(SaveSource.Launcher, game.Source);
        Assert.Equal(SupportState.Supported, game.Support);
    }

    [Fact]
    public void DocumentsSaveFolderIsFoundThroughTheRule()
    {
        if (!OperatingSystem.IsWindows()) return;

        string home = Path.Combine(folder.Root, "home");
        string documents = Path.Combine(home, "Documents");
        platform.Folders = new KnownFolders(home, "", "", "", documents, "", "", "", "", "", "andrix");
        InstallGame("105600", "Sandfall");
        Directory.CreateDirectory(Path.Combine(SteamRoot, "appcache"));
        File.WriteAllBytes(Path.Combine(SteamRoot, "appcache", "appinfo.vdf"), SteamAppInfoTests.AppInfo(true,
            (105600u, (w, t) => SteamAppInfoTests.Block(w, t, "appinfo",
                app => SteamAppInfoTests.Block(w, t, "ufs",
                    ufs => SteamAppInfoTests.Block(w, t, "savefiles",
                        files => SteamAppInfoTests.Block(w, t, "0", file =>
                        {
                            SteamAppInfoTests.Value(w, t, "root", "WinMyDocuments");
                            SteamAppInfoTests.Value(w, t, "path", "Sandfall");
                            SteamAppInfoTests.Value(w, t, "recursive", "1");
                        })))))));
        string saves = Path.Combine(documents, "Sandfall");
        TestFolder.Write(Path.Combine(saves, "slot.sav"), "x");

        GameInfo game = Games().Single(g => g.Id == "105600");

        Assert.Equal(saves, game.SaveFolder);
        Assert.Equal(SaveSource.Launcher, game.Source);
        Assert.Equal(SupportState.Supported, game.Support);
    }

    [Fact]
    public void GameWithoutRulesStaysAsItIs()
    {
        TestFolder.Write(Path.Combine(UserData, "111", "123450", "remote", "a.sav"), "x");
        TestFolder.Write(Path.Combine(SteamRoot, "steamapps", "appmanifest_814380.acf"), Manifest("814380", "Sekiro"));

        IReadOnlyList<GameInfo> games = Games();

        GameInfo userdata = games.Single(g => g.Id == "123450");
        Assert.Equal(SaveSource.Userdata, userdata.Source);
        Assert.Equal(SupportState.Supported, userdata.Support);
        Assert.Equal(Path.Combine(UserData, "111", "123450"), userdata.SaveFolder);

        GameInfo installed = games.Single(g => g.Id == "814380");
        Assert.Equal(SaveSource.None, installed.Source);
        Assert.Equal(SupportState.NoSaves, installed.Support);
        Assert.Equal("", installed.SaveFolder);
    }

    [Fact]
    public void ChosenFolderIsUsedAndMarkedAsChosen()
    {
        TestFolder.Write(Path.Combine(UserData, "111", "123450", "remotecache.vdf"), "cache");
        string chosen = Path.Combine(folder.Root, "mine");
        TestFolder.Write(Path.Combine(chosen, "slot.sav"), "x");
        new GameSaveSettings(Path.Combine(AppData.Root, "steam", "111", "123450")) { Folder = chosen, Origin = SaveSource.Chosen }.Save();

        GameInfo game = Games().Single(g => g.Id == "123450");

        Assert.Equal(chosen, game.SaveFolder);
        Assert.Equal(SaveSource.Chosen, game.Source);
        Assert.Equal(SupportState.Supported, game.Support);
    }

    [Fact]
    public void ChosenFolderInsideSteamIsUnsafe()
    {
        TestFolder.Write(Path.Combine(UserData, "111", "123450", "remotecache.vdf"), "cache");
        TestFolder.Write(Path.Combine(SteamRoot, "steamapps", "x.sav"), "x");
        new GameSaveSettings(Path.Combine(AppData.Root, "steam", "111", "123450")) { Folder = SteamRoot, Origin = SaveSource.Chosen }.Save();

        GameInfo game = Games().Single(g => g.Id == "123450");

        Assert.Equal(SupportState.Unsafe, game.Support);
        Assert.Equal(SteamRoot, game.SaveFolder);
    }
}
