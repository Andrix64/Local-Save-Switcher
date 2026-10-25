namespace Lss.Tests;

/// <summary>
/// Cartelle che non sono mai salvataggi (M3b): per tutti i giochi o per uno solo.
/// Prefissi e cartelle finti dentro TestFolder.
/// </summary>
public sealed class IgnoredFoldersTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string Home => Path.Combine(folder.Root, "home");

    private string GameData(string id) => Path.Combine(folder.Data, "steam", "111", id);

    private SaveContext Context(string prefix = "") => new(
        new KnownFolders(Home, "", "", "", "", "", "", "", "", "", "steamuser"),
        prefix,
        prefix != "",
        "",
        "111",
        IgnoreCase: false);

    private string Prefix(string name)
    {
        string prefix = Path.Combine(folder.Root, name);
        Directory.CreateDirectory(Path.Combine(prefix, "drive_c", "users", "steamuser"));
        return prefix;
    }

    private static string LocalAppData(string prefix) =>
        Path.Combine(prefix, "drive_c", "users", "steamuser", "AppData", "Local");

    [Fact]
    public void GlobalEntryIsStoredPortableAndMatchesAnotherPrefix()
    {
        string prefixA = Prefix("pfxA");
        string prefixB = Prefix("pfxB");
        string discordA = Path.Combine(LocalAppData(prefixA), "Discord");
        string discordB = Path.Combine(LocalAppData(prefixB), "Discord");
        Directory.CreateDirectory(discordA);
        Directory.CreateDirectory(discordB);

        string portable = SavePathResolver.ToPortable(discordA, Context(prefixA));
        var ignored = new IgnoredFolders(folder.Data);
        ignored.AddGlobal(portable);

        Assert.Equal("{WinAppDataLocal}/Discord", portable);
        Assert.Contains("Cartella.0={WinAppDataLocal}/Discord",
            File.ReadAllText(Path.Combine(folder.Data, "cartelle-ignorate.ini")));
        Assert.True(ignored.IsIgnored(Path.Combine(discordB, "cache.dat"), GameData("B"), Context(prefixB)));
    }

    [Fact]
    public void GameOnlyEntryDoesNotAffectAnotherGame()
    {
        string dir = Path.Combine(folder.Root, "launcher", "logs");
        var ignored = new IgnoredFolders(folder.Data);

        ignored.AddForGame(GameData("A"), dir);

        Assert.True(ignored.IsIgnored(dir, GameData("A"), Context()));
        Assert.False(ignored.IsIgnored(dir, GameData("B"), Context()));
    }

    [Fact]
    public void SubfolderOfAnIgnoredFolderIsIgnored()
    {
        string dir = Path.Combine(folder.Root, "cache");
        var ignored = new IgnoredFolders(folder.Data);
        ignored.AddForGame(GameData("A"), dir);

        Assert.True(ignored.IsIgnored(Path.Combine(dir, "deep", "file.sav"), GameData("A"), Context()));
        Assert.False(ignored.IsIgnored(dir + "2", GameData("A"), Context()));
    }

    [Fact]
    public void RemovingPutsTheFolderBackInTheSearch()
    {
        string dir = Path.Combine(folder.Root, "cache");
        var ignored = new IgnoredFolders(folder.Data);
        ignored.AddForGame(GameData("A"), dir);
        ignored.AddGlobal("{WinUserProfile}/Other");

        ignored.RemoveForGame(GameData("A"), dir);
        ignored.RemoveGlobal("{WinUserProfile}/Other");

        Assert.False(ignored.IsIgnored(dir, GameData("A"), Context()));
        Assert.Empty(ignored.Global);
        Assert.False(File.Exists(Path.Combine(folder.Data, "cartelle-ignorate.ini")));
    }

    [Fact]
    public void SavingTheSaveFolderKeepsTheIgnoredFolders()
    {
        string dir = Path.Combine(folder.Root, "cache");
        string saves = Path.Combine(folder.Root, "saves");
        var ignored = new IgnoredFolders(folder.Data);
        ignored.AddForGame(GameData("A"), dir);

        new GameSaveSettings(GameData("A")) { Folder = saves, Origin = SaveSource.Learned }.Save();

        Assert.True(ignored.IsIgnored(dir, GameData("A"), Context()));
        var loaded = new GameSaveSettings(GameData("A"));
        loaded.Load();
        Assert.Equal(saves, loaded.Folder);
    }

    [Fact]
    public void FindChangedDoesNotEnterAnIgnoredFolder()
    {
        string root = Path.Combine(folder.Root, "game");
        string discord = Path.Combine(root, "Launcher");
        TestFolder.Write(Path.Combine(discord, "sub", "x.sav"), "x");
        TestFolder.Write(Path.Combine(root, "saves", "y.sav"), "y");
        var since = DateTime.UtcNow.AddMinutes(-5);
        var ignored = new IgnoredFolders(folder.Data);
        ignored.AddForGame(GameData("A"), discord);
        var visited = new List<string>();

        IReadOnlyList<ChangedFile> found = SaveFinder.Scan(
            [new SearchRoot(root, 8, true)], since, ignored.Folders(GameData("A"), Context()),
            TimeSpan.FromMinutes(1), CancellationToken.None, out _, visited);

        Assert.DoesNotContain(visited, path => path.StartsWith(discord, StringComparison.Ordinal));
        Assert.Equal(Path.Combine(root, "saves", "y.sav"), Assert.Single(found).Path);
    }

    [Fact]
    public void RankDropsTheIgnoredCandidate()
    {
        string documents = Path.Combine(Home, "Documents");
        string game = Path.Combine(documents, "My Games", "Hollow Knight");
        TestFolder.Write(Path.Combine(game, "Saves", "user1.dat"), "x");
        var root = new SearchRoot(documents, 6, false);
        var files = new List<ChangedFile>
        {
            new(Path.Combine(game, "Saves", "user1.dat"), 1, DateTime.UtcNow, root),
        };
        var ignored = new IgnoredFolders(folder.Data);
        ignored.AddForGame(GameData("A"), game);

        IReadOnlyList<SaveCandidate> candidates = SaveFinder.Rank(files,
            Context(), "Hollow Knight", [], "", [], ignored.Folders(GameData("A"), Context()));

        Assert.Empty(candidates);
    }

    [Fact]
    public void PathOutsideEveryRootStaysFull()
    {
        string other = Path.Combine(folder.Root, "elsewhere", "Game");

        Assert.Equal(other, SavePathResolver.ToPortable(other, Context()));
        Assert.Null(SavePathResolver.FromPortable("{WinAppDataLocal}/Discord", Context()));
    }
}
