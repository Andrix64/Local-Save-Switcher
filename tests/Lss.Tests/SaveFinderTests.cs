namespace Lss.Tests;

/// <summary>
/// SaveFinder: le cartelle in cui cercare (M1). Cartelle vere dentro TestFolder.
/// </summary>
public sealed class SaveFinderTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string Home => Path.Combine(folder.Root, "home");

    private static SearchRoot? Find(IReadOnlyList<SearchRoot> roots, string path) =>
        roots.FirstOrDefault(r => string.Equals(r.Folder, Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));

    private static SaveContext Context(KnownFolders folders, string prefix = "", bool perGame = false,
        string install = "") =>
        new(folders, prefix, perGame, install, "111", IgnoreCase: false);

    private static KnownFolders Folders(string home, string documents = "", string appData = "",
        string localAppData = "", string localAppDataLow = "", string savedGames = "", string publicFolder = "",
        string programData = "", string xdgData = "", string xdgConfig = "", string user = "andrix") =>
        new(home, appData, localAppData, localAppDataLow, documents, savedGames, publicFolder, programData,
            xdgData, xdgConfig, user);

    [Fact]
    public void WindowsFoldersAreSearchedWithTheirDepth()
    {
        string documents = Path.Combine(Home, "Documents");
        string roaming = Path.Combine(Home, "AppData", "Roaming");
        string local = Path.Combine(Home, "AppData", "Local");
        string lowLocal = Path.Combine(Home, "AppData", "LocalLow");
        string saved = Path.Combine(Home, "Saved Games");
        string publicFolder = Path.Combine(folder.Root, "Public");
        string publicDocuments = Path.Combine(publicFolder, "Documents");
        string programData = Path.Combine(folder.Root, "ProgramData");

        foreach (string dir in new[] { documents, roaming, local, lowLocal, saved, publicDocuments, programData })
            Directory.CreateDirectory(dir);

        KnownFolders folders = Folders(Home, documents, roaming, local, lowLocal, saved, publicFolder, programData);

        IReadOnlyList<SearchRoot> roots = SaveFinder.Roots(Context(folders), []);

        Assert.Equal(6, Find(roots, documents)?.MaxDepth);
        Assert.Equal(6, Find(roots, roaming)?.MaxDepth);
        Assert.Equal(6, Find(roots, local)?.MaxDepth);
        Assert.Equal(6, Find(roots, lowLocal)?.MaxDepth);
        Assert.Equal(6, Find(roots, saved)?.MaxDepth);
        Assert.Equal(4, Find(roots, publicDocuments)?.MaxDepth);
        Assert.Equal(4, Find(roots, programData)?.MaxDepth);
        Assert.All(roots, r => Assert.False(r.GameOwned));
    }

    [Fact]
    public void GameFoldersAreOwnedByTheGame()
    {
        string install = Path.Combine(folder.Root, "lib", "Game");
        string userdata = Path.Combine(folder.Root, "userdata", "111", "870780");
        Directory.CreateDirectory(install);
        Directory.CreateDirectory(userdata);

        IReadOnlyList<SearchRoot> roots = SaveFinder.Roots(
            Context(Folders(Home), install: install), [], userdata);

        Assert.Equal(new SearchRoot(install, 8, true), Find(roots, install));
        Assert.Equal(new SearchRoot(userdata, 8, true), Find(roots, userdata));
    }

    [Fact]
    public void ExtraFoldersAreNotOwned()
    {
        string extra = Path.Combine(folder.Root, "extra");
        Directory.CreateDirectory(extra);

        IReadOnlyList<SearchRoot> roots = SaveFinder.Roots(Context(Folders(Home)), [extra]);

        Assert.Equal(new SearchRoot(extra, 8, false), Find(roots, extra));
    }

    [Fact]
    public void PrefixUserFolderIsOwnedWhenThePrefixIsPerGame()
    {
        string prefix = Path.Combine(folder.Root, "pfx");
        string user = Path.Combine(prefix, "drive_c", "users", "steamuser");
        string programData = Path.Combine(prefix, "drive_c", "ProgramData");
        string publicUsers = Path.Combine(prefix, "drive_c", "users", "Public");
        Directory.CreateDirectory(user);
        Directory.CreateDirectory(programData);
        Directory.CreateDirectory(publicUsers);

        IReadOnlyList<SearchRoot> roots = SaveFinder.Roots(
            Context(Folders(Home, user: "steamuser"), prefix, perGame: true), []);

        Assert.Equal(new SearchRoot(user, 7, true), Find(roots, user));
        Assert.Equal(new SearchRoot(programData, 5, true), Find(roots, programData));
        Assert.Equal(new SearchRoot(publicUsers, 5, true), Find(roots, publicUsers));
    }

    [Fact]
    public void SharedPrefixIsNotOwned()
    {
        string prefix = Path.Combine(folder.Root, "pfx");
        string user = Path.Combine(prefix, "drive_c", "users", "steamuser");
        Directory.CreateDirectory(user);

        IReadOnlyList<SearchRoot> roots = SaveFinder.Roots(
            Context(Folders(Home, user: "steamuser"), prefix, perGame: false), []);

        Assert.Equal(new SearchRoot(user, 7, false), Find(roots, user));
    }

    [Fact]
    public void LinuxFoldersAndHiddenFoldersInHome()
    {
        if (!OperatingSystem.IsLinux()) return;

        string hidden = Path.Combine(Home, ".mygame");
        string visible = Path.Combine(Home, "Games");
        string xdgData = Path.Combine(Home, ".local", "share");
        string xdgConfig = Path.Combine(Home, ".config");
        string documents = Path.Combine(Home, "Documents");

        foreach (string dir in new[] { hidden, visible, xdgData, xdgConfig, documents })
            Directory.CreateDirectory(dir);

        KnownFolders folders = Folders(Home, documents: documents, xdgData: xdgData, xdgConfig: xdgConfig);

        IReadOnlyList<SearchRoot> roots = SaveFinder.Roots(Context(folders), []);

        Assert.Equal(4, Find(roots, hidden)?.MaxDepth);
        Assert.Null(Find(roots, visible));
        Assert.Equal(6, Find(roots, documents)?.MaxDepth);

        // Dentro a una cartella nascosta di Home: resta la cartella esterna, con la profondità maggiore.
        Assert.Null(Find(roots, xdgData));
        Assert.Equal(5, Find(roots, Path.Combine(Home, ".local"))?.MaxDepth);
        Assert.Equal(5, Find(roots, Path.Combine(Home, ".config"))?.MaxDepth);
    }

    [Fact]
    public void WineWithoutPrefixIsFoundInTheUsualPlaces()
    {
        if (!OperatingSystem.IsLinux()) return;

        string wine = Path.Combine(Home, ".wine", "drive_c", "users");
        string gameWine = Path.Combine(Home, "Games", "Mario", "drive_c", "users");
        Directory.CreateDirectory(wine);
        Directory.CreateDirectory(gameWine);

        IReadOnlyList<SearchRoot> roots = SaveFinder.Roots(Context(Folders(Home)), []);

        // ~/.wine è anche una cartella nascosta di Home: la contiene, con la profondità maggiore.
        Assert.Equal(new SearchRoot(Path.Combine(Home, ".wine"), 6, false), Find(roots, Path.Combine(Home, ".wine")));
        Assert.Equal(new SearchRoot(gameWine, 6, false), Find(roots, gameWine));
    }

    [Fact]
    public void FolderInsideAnotherIsMergedIntoTheOuterOne()
    {
        string documents = Path.Combine(Home, "Documents");
        string inner = Path.Combine(documents, "My Games", "Game");
        Directory.CreateDirectory(inner);

        IReadOnlyList<SearchRoot> roots = SaveFinder.Roots(
            Context(Folders(Home, documents: documents)), [inner]);

        SearchRoot merged = Assert.Single(roots);
        Assert.Equal(new SearchRoot(documents, 8, false), merged);
    }

    [Fact]
    public void GameFolderInsideAnotherFolderStaysOwn()
    {
        string documents = Path.Combine(Home, "Documents");
        string install = Path.Combine(documents, "Game");
        Directory.CreateDirectory(install);

        IReadOnlyList<SearchRoot> roots = SaveFinder.Roots(
            Context(Folders(Home, documents: documents), install: install), []);

        Assert.Equal(2, roots.Count);
        Assert.Equal(new SearchRoot(install, 8, true), roots[0]);
        Assert.Equal(new SearchRoot(documents, 6, false), roots[1]);
    }

    [Fact]
    public void MissingFoldersAreSkipped()
    {
        string missing = Path.Combine(folder.Root, "missing");

        IReadOnlyList<SearchRoot> roots = SaveFinder.Roots(
            Context(Folders(Home, documents: missing), install: missing), [missing]);

        Assert.Empty(roots);
    }

    // ---- M3: candidati e punteggio ----

    private static ChangedFile Created(string path, SearchRoot root)
    {
        TestFolder.Write(path, "x");
        return new ChangedFile(path, 1, DateTime.UtcNow, root);
    }

    private static SaveCandidate? Top(IReadOnlyList<SaveCandidate> candidates) => candidates.FirstOrDefault();

    private static int Points(SaveCandidate candidate, ScoreReasonKind kind) =>
        candidate.Reasons.Where(r => r.Kind == kind).Sum(r => r.Points);

    [Fact]
    public void DocumentsGameIsRaisedToTheGameFolder()
    {
        string documents = Path.Combine(Home, "Documents");
        string game = Path.Combine(documents, "My Games", "Hollow Knight");
        var root = new SearchRoot(documents, 6, false);
        var files = new List<ChangedFile>
        {
            Created(Path.Combine(game, "Saves", "user1.dat"), root),
            Created(Path.Combine(game, "Saves", "user2.dat"), root),
        };

        IReadOnlyList<SaveCandidate> candidates = SaveFinder.Rank(files, Context(Folders(Home, documents: documents)),
            "Hollow Knight", [], "", []);

        SaveCandidate top = Assert.Single(candidates);
        Assert.Equal(game, top.Folder);
        Assert.Equal(6, Points(top, ScoreReasonKind.GameName));
        Assert.Equal(3, Points(top, ScoreReasonKind.SaveLikeFile));
        Assert.Equal(2, Points(top, ScoreReasonKind.ChangedFiles));
        Assert.Equal(3, Points(top, ScoreReasonKind.SaveLikePath));
    }

    [Fact]
    public void ProtonGameFolderWinsOverTheBrowserInTheSamePrefix()
    {
        string prefix = Path.Combine(folder.Root, "pfx");
        string user = Path.Combine(prefix, "drive_c", "users", "steamuser");
        string local = Path.Combine(user, "AppData", "Local");
        var root = new SearchRoot(user, 7, true);
        var files = new List<ChangedFile>
        {
            Created(Path.Combine(local, "Hollow Knight", "user1.dat"), root),
            Created(Path.Combine(local, "BraveSoftware", "Brave-Browser", "Default", "Cookies.dat"), root),
            Created(Path.Combine(local, "BraveSoftware", "Brave-Browser", "Default", "History.dat"), root),
        };

        IReadOnlyList<SaveCandidate> candidates = SaveFinder.Rank(files,
            Context(Folders(Home, user: "steamuser"), prefix, perGame: true), "Hollow Knight", [], "", []);

        Assert.Equal(Path.Combine(local, "Hollow Knight"), candidates[0].Folder);
        Assert.Contains(candidates, c => c.Folder == Path.Combine(local, "BraveSoftware", "Brave-Browser", "Default")
            && c.Score < candidates[0].Score);
    }

    [Fact]
    public void OnlySettingsChangedGivesALowScore()
    {
        string documents = Path.Combine(Home, "Documents");
        var root = new SearchRoot(documents, 6, false);
        var files = new List<ChangedFile>
        {
            Created(Path.Combine(documents, "Hollow Knight", "settings.ini"), root),
        };

        SaveCandidate top = Assert.Single(SaveFinder.Rank(files, Context(Folders(Home, documents: documents)),
            "Hollow Knight", [], "", []));

        Assert.True(top.Score < 6);
        Assert.Equal(-4, Points(top, ScoreReasonKind.OnlySettings));
    }

    [Fact]
    public void SaveFolderInsideTheInstallFolderIsProposed()
    {
        string install = Path.Combine(folder.Root, "lib", "Game");
        string saves = Path.Combine(install, "saves");
        var root = new SearchRoot(install, 8, true);
        var files = new List<ChangedFile> { Created(Path.Combine(saves, "slot1.sav"), root) };

        SaveCandidate top = Assert.Single(SaveFinder.Rank(files, Context(Folders(Home), install: install),
            "Game", [], "", []));

        Assert.Equal(saves, top.Folder);
        Assert.Equal(5, Points(top, ScoreReasonKind.GameOwned));
        Assert.Equal(2, Points(top, ScoreReasonKind.SaveLikePath));
    }

    [Fact]
    public void ProtectedFolderIsDropped()
    {
        string documents = Path.Combine(Home, "Documents");
        var root = new SearchRoot(documents, 6, false);
        var files = new List<ChangedFile> { Created(Path.Combine(documents, "Hollow Knight.sav"), root) };

        Assert.Empty(SaveFinder.Rank(files, Context(Folders(Home, documents: documents)), "Hollow Knight", [], "", []));
    }

    [Fact]
    public void NothingChangedGivesNoCandidates()
    {
        Assert.Empty(SaveFinder.Rank([], Context(Folders(Home)), "Hollow Knight", [], "", []));
    }

    [Fact]
    public void FilesInAKnownSaveFolderAreNotProposedAgain()
    {
        string documents = Path.Combine(Home, "Documents");
        string game = Path.Combine(documents, "My Games", "Hollow Knight");
        var root = new SearchRoot(documents, 6, false);
        var files = new List<ChangedFile> { Created(Path.Combine(game, "Saves", "user1.dat"), root) };

        Assert.Empty(SaveFinder.Rank(files, Context(Folders(Home, documents: documents)), "Hollow Knight", [], "",
            [game]));
    }

    [Fact]
    public void AnotherProgramIsDropped()
    {
        string documents = Path.Combine(Home, "Documents");
        var root = new SearchRoot(documents, 6, false);
        var files = new List<ChangedFile> { Created(Path.Combine(documents, "Random", "x.sav"), root) };

        Assert.Empty(SaveFinder.Rank(files, Context(Folders(Home, documents: documents)), "Hollow Knight", [], "", []));
    }
}
