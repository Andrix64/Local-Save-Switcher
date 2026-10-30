namespace Lss.Tests;

public sealed class SteamLibraryTests : IDisposable
{
    private readonly TestFolder folder = new();

    private string SteamRoot => Path.Combine(folder.Root, "Steam");

    public void Dispose() => folder.Dispose();

    /// <summary>
    /// libraryfolders.vdf con una voce per cartella, con i percorsi scritti come li scrive Steam.
    /// </summary>
    private static string LibraryFoldersFile(params string[] paths)
    {
        string entries = string.Concat(paths.Select((path, i) =>
        {
            string escaped = path.Replace("\\", "\\\\");
            return $"\t\"{i}\"\n\t{{\n\t\t\"path\"\t\t\"{escaped}\"\n\t}}\n";
        }));

        return "\"libraryfolders\"\n{\n" + entries + "}\n";
    }

    /// <summary>
    /// Manifest appmanifest_&lt;AppID&gt;.acf con nome e cartella di installazione.
    /// </summary>
    private static string Manifest(string appId, string name, string installDir) =>
        $"\"AppState\"\n{{\n\t\"appid\"\t\t\"{appId}\"\n\t\"name\"\t\t\"{name}\"\n\t\"installdir\"\t\t\"{installDir}\"\n}}\n";

    [Fact]
    public void LibraryFolders_WithoutFile_ReturnsSteamRoot()
    {
        Assert.Equal(new[] { SteamRoot }, SteamLibrary.LibraryFolders(SteamRoot).ToArray());
    }

    [Fact]
    public void LibraryFolders_ReadsAllExistingPaths()
    {
        string second = Path.Combine(folder.Root, "Lib2");
        string missing = Path.Combine(folder.Root, "Mancante");
        Directory.CreateDirectory(second);
        TestFolder.Write(
            Path.Combine(SteamRoot, "steamapps", "libraryfolders.vdf"),
            LibraryFoldersFile(SteamRoot, second, missing));

        Assert.Equal(new[] { SteamRoot, second }, SteamLibrary.LibraryFolders(SteamRoot).ToArray());
    }

    [Fact]
    public void LibraryFolders_BrokenFile_ReturnsSteamRoot()
    {
        TestFolder.Write(
            Path.Combine(SteamRoot, "steamapps", "libraryfolders.vdf"),
            "\"libraryfolders\"\n{\n\"0\"\n{");

        Assert.Equal(new[] { SteamRoot }, SteamLibrary.LibraryFolders(SteamRoot).ToArray());
    }

    [Fact]
    public void ReadManifest_FindsGameInSecondLibrary()
    {
        string second = Path.Combine(folder.Root, "Lib2");
        TestFolder.Write(
            Path.Combine(second, "steamapps", "appmanifest_105600.acf"),
            Manifest("105600", "Terraria", "Terraria"));
        string[] libraries = [SteamRoot, second];

        var manifest = SteamLibrary.ReadManifest(libraries, "105600");

        Assert.NotNull(manifest);
        Assert.Equal("Terraria", manifest.Value.Name);
        Assert.Equal("Terraria", manifest.Value.InstallDir);
        Assert.Equal(second, manifest.Value.Library);
    }

    [Fact]
    public void ReadManifest_MissingGame_ReturnsNull()
    {
        string[] libraries = [SteamRoot];

        Assert.Null(SteamLibrary.ReadManifest(libraries, "999999"));
    }

    [Fact]
    public void ReadManifest_BrokenManifest_IsSkipped()
    {
        string second = Path.Combine(folder.Root, "Lib2");
        TestFolder.Write(
            Path.Combine(SteamRoot, "steamapps", "appmanifest_105600.acf"),
            "\"AppState\"\n{\n\t\"name\"");
        TestFolder.Write(
            Path.Combine(second, "steamapps", "appmanifest_105600.acf"),
            Manifest("105600", "Terraria", "Terraria"));
        string[] libraries = [SteamRoot, second];

        var manifest = SteamLibrary.ReadManifest(libraries, "105600");

        Assert.NotNull(manifest);
        Assert.Equal(second, manifest.Value.Library);
    }

    [Fact]
    public void FindImages_NewLayout_FindsIconAndCoverInGameFolder()
    {
        string folder = Path.Combine(SteamRoot, "appcache", "librarycache", "870780");
        string icon = Path.Combine(folder, "858961e95fbf869f136e1770d586e0caefd4cfac.jpg");
        string cover = Path.Combine(folder, "library_600x900.jpg");
        TestFolder.Write(icon, "");
        TestFolder.Write(cover, "");
        TestFolder.Write(Path.Combine(folder, "header.jpg"), "");
        TestFolder.Write(Path.Combine(folder, "logo.png"), "");

        var images = SteamLibrary.FindImages(SteamRoot, "870780");

        Assert.Equal(icon, images.Icon);
        Assert.Equal(cover, images.Cover);
    }

    [Fact]
    public void FindImages_OldLayout_FindsFilesInLibraryCache()
    {
        string cache = Path.Combine(SteamRoot, "appcache", "librarycache");
        TestFolder.Write(Path.Combine(cache, "870780_icon.jpg"), "");
        TestFolder.Write(Path.Combine(cache, "870780_library_600x900.jpg"), "");

        var images = SteamLibrary.FindImages(SteamRoot, "870780");

        Assert.Equal(Path.Combine(cache, "870780_icon.jpg"), images.Icon);
        Assert.Equal(Path.Combine(cache, "870780_library_600x900.jpg"), images.Cover);
    }

    [Fact]
    public void FindImages_NothingInCache_ReturnsEmptyPaths()
    {
        Assert.Equal(("", ""), SteamLibrary.FindImages(SteamRoot, "870780"));
    }

    [Fact]
    public void GetSupport_FilesInRemote_IsSupported()
    {
        TestFolder.Write(folder.SaveFile, "partita");
        TestFolder.Write(folder.CacheFile, "cache");

        Assert.Equal(SupportState.Supported, SteamLibrary.GetSupport(folder.Game));
    }

    [Fact]
    public void GetSupport_OnlyRemoteCache_IsSavesElsewhere()
    {
        TestFolder.Write(folder.CacheFile, "cache");

        Assert.Equal(SupportState.SavesElsewhere, SteamLibrary.GetSupport(folder.Game));
    }

    [Fact]
    public void GetSupport_EmptyFolder_IsNoSaves()
    {
        Directory.CreateDirectory(folder.Game);

        Assert.Equal(SupportState.NoSaves, SteamLibrary.GetSupport(folder.Game));
    }

    [Fact]
    public void GetSupport_MissingFolder_IsNoSaves()
    {
        Assert.Equal(SupportState.NoSaves, SteamLibrary.GetSupport(folder.Game));
    }

    [Fact]
    public void GetSupport_NestedRemoteCache_IsSupported()
    {
        TestFolder.Write(Path.Combine(folder.Game, "remote", "remotecache.vdf"), "cache");

        Assert.Equal(SupportState.Supported, SteamLibrary.GetSupport(folder.Game));
    }

    [Fact]
    public void FindGames_ListsGamesWithNamesAndSupport()
    {
        string second = Path.Combine(folder.Root, "Lib2");
        string account = Path.Combine(SteamRoot, "userdata", "111");
        TestFolder.Write(
            Path.Combine(SteamRoot, "steamapps", "libraryfolders.vdf"),
            LibraryFoldersFile(SteamRoot, second));
        TestFolder.Write(Path.Combine(SteamRoot, "steamapps", "appmanifest_105600.acf"), Manifest("105600", "Terraria", "Terraria"));
        TestFolder.Write(Path.Combine(second, "steamapps", "appmanifest_814380.acf"), Manifest("814380", "Sekiro", "Sekiro"));
        TestFolder.Write(Path.Combine(account, "105600", "remote", "a.sav"), "x");
        TestFolder.Write(Path.Combine(account, "814380", "remotecache.vdf"), "cache");
        TestFolder.Write(Path.Combine(account, "7", "remote", "sharedconfig.vdf"), "x");
        TestFolder.Write(Path.Combine(account, "config", "localconfig.vdf"), "x");
        TestFolder.Write(Path.Combine(account, "241100", "x.vdf"), "x");

        var games = SteamLibrary.FindGames(SteamRoot, account);

        Assert.Equal(2, games.Count);
        Assert.Equal("Terraria", games[0].Name);
        Assert.Equal(SupportState.Supported, games[0].Support);
        Assert.Equal("Terraria", games[0].InstallDir);
        Assert.Equal(SteamRoot, games[0].LibraryPath);
        Assert.Equal("Sekiro", games[1].Name);
        Assert.Equal(SupportState.SavesElsewhere, games[1].Support);
        Assert.Equal(second, games[1].LibraryPath);
    }

    [Fact]
    public void FindGames_UsesKnownNameWithoutManifest()
    {
        string account = Path.Combine(SteamRoot, "userdata", "111");
        TestFolder.Write(Path.Combine(account, "870780", "remote", "x"), "x");

        var games = SteamLibrary.FindGames(SteamRoot, account);

        Assert.Single(games);
        Assert.Equal("Control", games[0].Name);
        Assert.False(games[0].IsInstalled);
    }

    [Fact]
    public void FindGames_UnknownGameWithoutManifest()
    {
        string account = Path.Combine(SteamRoot, "userdata", "111");
        TestFolder.Write(Path.Combine(account, "123450", "remote", "x"), "x");

        var games = SteamLibrary.FindGames(SteamRoot, account);

        Assert.Single(games);
        Assert.Equal("App 123450", games[0].Name);
    }

    [Fact]
    public void FindGames_SkipsProtonAndRuntimes()
    {
        string account = Path.Combine(SteamRoot, "userdata", "111");
        TestFolder.Write(Path.Combine(SteamRoot, "steamapps", "appmanifest_1391110.acf"), Manifest("1391110", "Steam Linux Runtime 2.0 (soldier)", "x"));
        TestFolder.Write(Path.Combine(account, "1391110", "remote", "x"), "x");

        Assert.Empty(SteamLibrary.FindGames(SteamRoot, account));
    }

    [Fact]
    public void FindGames_InstalledGameWithoutUserdataIsListedAsNoSaves()
    {
        string account = Path.Combine(SteamRoot, "userdata", "111");
        Directory.CreateDirectory(account);
        TestFolder.Write(Path.Combine(SteamRoot, "steamapps", "appmanifest_1085660.acf"), Manifest("1085660", "Destiny 2", "Destiny 2"));

        var games = SteamLibrary.FindGames(SteamRoot, account);

        GameInfo game = Assert.Single(games);
        Assert.Equal("Destiny 2", game.Name);
        Assert.Equal(SupportState.NoSaves, game.Support);
        Assert.Equal(SaveSource.None, game.Source);
        Assert.Equal("", game.SaveFolder);
        Assert.True(game.IsInstalled);
    }

    [Fact]
    public void FindGames_GameWithUserdataAndManifestIsListedOnce()
    {
        string account = Path.Combine(SteamRoot, "userdata", "111");
        TestFolder.Write(Path.Combine(SteamRoot, "steamapps", "appmanifest_105600.acf"), Manifest("105600", "Terraria", "Terraria"));
        TestFolder.Write(Path.Combine(account, "105600", "remote", "a.sav"), "x");

        var games = SteamLibrary.FindGames(SteamRoot, account);

        GameInfo game = Assert.Single(games);
        Assert.Equal(SupportState.Supported, game.Support);
        Assert.Equal(SaveSource.Userdata, game.Source);
        Assert.Equal(Path.Combine(account, "105600"), game.SaveFolder);
    }

    [Fact]
    public void FindGames_SkipsInstalledProtonAndRuntimesWithoutUserdata()
    {
        string account = Path.Combine(SteamRoot, "userdata", "111");
        Directory.CreateDirectory(account);
        TestFolder.Write(Path.Combine(SteamRoot, "steamapps", "appmanifest_1493710.acf"), Manifest("1493710", "Proton 9.0", "Proton 9.0"));
        TestFolder.Write(Path.Combine(SteamRoot, "steamapps", "appmanifest_1628350.acf"), Manifest("1628350", "Steam Linux Runtime 3.0 (sniper)", "x"));

        Assert.Empty(SteamLibrary.FindGames(SteamRoot, account));
    }
}
