namespace Lss.Tests;

/// <summary>
/// SaveLocator con un prefisso Proton finto: le regole cercano cartelle vere
/// dentro la cartella temporanea.
/// </summary>
public sealed class SaveLocatorTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string Home => Path.Combine(folder.Root, "home");
    private string Prefix => Path.Combine(folder.Root, "pfx");
    private string Steamuser => Path.Combine(Prefix, "drive_c", "users", "steamuser");
    private string LssData => Path.Combine(folder.Root, "lssdata");

    private SaveContext Context() => new(
        new KnownFolders(
            Home, "", "", "", Path.Combine(Home, "Documents"), "", "", "", "", "", "andrix"),
        Prefix,
        true,
        Path.Combine(folder.Root, "lib", "steamapps", "common", "Game"),
        "123",
        IgnoreCase: false);

    private static void Touch(string path) => TestFolder.Write(path, "x");

    private static SaveRule Rule(string root, string path, string pattern = "*", bool recursive = true,
        params string[] platforms) => new(root, path, pattern, recursive, platforms);

    private static GameInfo Game(string saveFolder = "", SupportState support = SupportState.NoSaves,
        SaveSource source = SaveSource.None) =>
        new("steam", "123450", "Test", "", "", "", GameInfo.SpotsOf(saveFolder, source), support, "", "", source);

    /// <summary>
    /// Posti salvati di un gioco (posti.ini in una cartella dati vuota): con una cartella scelta, il posto 1.
    /// </summary>
    private GameSpots Stored(string chosen, SaveSource chosenSource)
    {
        var stored = new GameSpots(Path.Combine(folder.Root, "gamedata"));
        stored.Load();

        if (chosen != "")
            stored.SetPrimary(chosen, chosenSource);

        return stored;
    }

    private SaveLocation Run(GameInfo game, IReadOnlyList<SaveRule> rules, string chosen = "",
        SaveSource chosenSource = SaveSource.Chosen) =>
        SaveLocator.Locate(game, rules, [], Context(), [], LssData, Stored(chosen, chosenSource));

    [Fact]
    public void ExcludedLauncherSpotIsNotFoundAgain()
    {
        string saves = Path.Combine(Steamuser, "AppData", "Local", "Game", "saves");
        string settings = Path.Combine(Steamuser, "AppData", "Local", "Game");
        Touch(Path.Combine(saves, "a.sav"));
        Touch(Path.Combine(settings, "Graphics.xml"));
        SaveRule[] rules = [Rule("WinAppDataLocal", "Game/saves"), Rule("WinAppDataLocal", "Game", "Graphics.xml", false)];

        SaveLocation before = Run(Game(), rules);
        SaveSpot extra = Assert.Single(before.Spots, spot => spot.Id != 1);

        var stored = new GameSpots(Path.Combine(folder.Root, "gamedata"));
        stored.Load();
        stored.Exclude(SaveLocator.KeyOf(extra));
        Assert.True(stored.Save());

        var reloaded = new GameSpots(Path.Combine(folder.Root, "gamedata"));
        reloaded.Load();
        SaveLocation after = SaveLocator.Locate(Game(), rules, [], Context(), [], LssData, reloaded);

        Assert.Single(after.Spots);
    }

    [Fact]
    public void OneFolderIsSupported()
    {
        string saves = Path.Combine(Steamuser, "AppData", "Local", "Game", "saves");
        Touch(Path.Combine(saves, "a.sav"));

        SaveLocation found = Run(Game(), [Rule("WinAppDataLocal", "Game/saves")]);

        Assert.Equal(saves, found.Folder);
        Assert.Equal(SaveSource.Launcher, found.Source);
        Assert.Equal(SupportState.Supported, found.Support);
    }

    [Fact]
    public void FileInsideTheFolderIsCoveredByTheFolder()
    {
        string saves = Path.Combine(Steamuser, "AppData", "Local", "Game", "saves");
        Touch(Path.Combine(saves, "a.sav"));

        SaveLocation found = Run(Game(), [
            Rule("WinAppDataLocal", "Game/saves"),
            Rule("WinAppDataLocal", "Game/saves", "*.sav", recursive: false)
        ]);

        Assert.Equal(saves, found.Folder);
        Assert.Equal(SupportState.Supported, found.Support);
    }

    [Fact]
    public void TwoFoldersAreTwoSpots()
    {
        string a = Path.Combine(Steamuser, "AppData", "Local", "Game", "a");
        string b = Path.Combine(Steamuser, "AppData", "Local", "Game", "b");
        Touch(Path.Combine(a, "x.sav"));
        Touch(Path.Combine(b, "y.sav"));

        SaveLocation found = Run(Game(), [
            Rule("WinAppDataLocal", "Game/a"),
            Rule("WinAppDataLocal", "Game/b")
        ]);

        Assert.Equal(SupportState.Supported, found.Support);
        Assert.Equal(a, found.Folder);
        Assert.Equal([1, 2], found.Spots.Select(spot => spot.Id).ToArray());
        Assert.Equal(b, found.Spots[1].Folder);
    }

    [Fact]
    public void OnlyFilesAreAPatternSpot()
    {
        string local = Path.Combine(Steamuser, "AppData", "Local", "Game");
        Touch(Path.Combine(local, "a.sav"));

        SaveLocation found = Run(Game(), [Rule("WinAppDataLocal", "Game", "*.sav", recursive: false)]);

        Assert.Equal(SupportState.Supported, found.Support);
        SaveSpot spot = Assert.Single(found.Spots);
        Assert.Equal(local, spot.Folder);
        Assert.Equal("*.sav", spot.Pattern);
        Assert.False(spot.Recursive);
    }

    [Fact]
    public void TooWideMaskWithoutRecursionIsUnsure()
    {
        Touch(Path.Combine(Steamuser, "AppData", "Local", "Game", "a.sav"));

        SaveLocation found = Run(Game(), [Rule("WinAppDataLocal", "Game", "*", recursive: false)]);

        Assert.Equal(SupportState.Unsure, found.Support);
        Assert.Empty(found.Spots);
    }

    [Fact]
    public void SteamUserdataGameIgnoresAWideMask()
    {
        string userdata = Path.Combine(folder.Root, "userdata", "111", "123450");
        Touch(Path.Combine(userdata, "remote", "a.sav"));
        Touch(Path.Combine(Steamuser, "Documents", "Game", "b.sav"));

        SaveLocation found = Run(Game(userdata, SupportState.Supported, SaveSource.Userdata),
            [Rule("WinMyDocuments", "Game", "*", recursive: false)]);

        Assert.Equal(SupportState.Supported, found.Support);
        Assert.Equal(userdata, found.Folder);
        Assert.Single(found.Spots);
    }

    [Fact]
    public void ChosenSpotsAreKeptWithTheirIds()
    {
        string mine = Path.Combine(folder.Root, "mine");
        string extra = Path.Combine(folder.Root, "extra");
        Touch(Path.Combine(mine, "a.sav"));
        Touch(Path.Combine(extra, "b.sav"));

        var stored = new GameSpots(Path.Combine(folder.Root, "gamedata"));
        stored.Load();
        stored.SetPrimary(mine, SaveSource.Chosen);
        stored.Add(extra, "", extra, SaveSource.Chosen);
        stored.Save();

        SaveLocation found = SaveLocator.Locate(Game(), [], [], Context(), [], LssData, stored);

        Assert.Equal(SupportState.Supported, found.Support);
        Assert.Equal([1, 2], found.Spots.Select(spot => spot.Id).ToArray());
        Assert.Equal(extra, found.Spots[1].Folder);
    }

    [Fact]
    public void ChosenSpotInsideAFoundFolderIsDroppedInFavourOfTheFoundOne()
    {
        string saves = Path.Combine(Steamuser, "AppData", "Local", "Game", "saves");
        Touch(Path.Combine(saves, "a.sav"));

        var stored = new GameSpots(Path.Combine(folder.Root, "gamedata"));
        stored.Load();
        stored.Add(Path.Combine(saves, "sub"), "", Path.Combine(saves, "sub"), SaveSource.Chosen);
        Touch(Path.Combine(saves, "sub", "b.sav"));

        SaveLocation found = SaveLocator.Locate(Game(), [Rule("WinAppDataLocal", "Game/saves")], [], Context(), [],
            LssData, stored);

        Assert.Equal(saves, found.Folder);
        Assert.Single(found.Spots);
    }

    [Fact]
    public void MoreThanEightSpotsIsUnsure()
    {
        var rules = new List<SaveRule>();

        for (int i = 0; i < 9; i++)
        {
            string dir = Path.Combine(Steamuser, "AppData", "Local", "Game", "p" + i);
            Touch(Path.Combine(dir, "a.sav"));
            rules.Add(Rule("WinAppDataLocal", "Game/p" + i));
        }

        SaveLocation found = Run(Game(), rules);

        Assert.Equal(SupportState.Unsure, found.Support);
        Assert.Empty(found.Spots);
    }

    [Fact]
    public void RulesOnlyForOtherSystemsAreNotFound()
    {
        Touch(Path.Combine(Steamuser, "AppData", "Local", "Game", "a.sav"));

        SaveLocation found = Run(Game(), [Rule("WinAppDataLocal", "Game", platforms: "MacOS")]);

        Assert.Equal(SupportState.NotFound, found.Support);
        Assert.Equal(SaveSource.None, found.Source);
    }

    [Fact]
    public void ChosenFolderWinsOverTheRules()
    {
        string chosen = Path.Combine(folder.Root, "mine");
        Touch(Path.Combine(chosen, "a.sav"));
        Touch(Path.Combine(Steamuser, "AppData", "Local", "Game", "saves", "b.sav"));

        SaveLocation found = Run(Game(), [Rule("WinAppDataLocal", "Game/saves")], chosen, SaveSource.Chosen);

        Assert.Equal(chosen, found.Folder);
        Assert.Equal(SaveSource.Chosen, found.Source);
        Assert.Equal(SupportState.Supported, found.Support);
    }

    [Fact]
    public void UnsafeChosenFolderIsUnsafeAndRulesAreNotTried()
    {
        string local = Path.Combine(Steamuser, "AppData", "Local");
        Touch(Path.Combine(local, "Game", "saves", "a.sav"));

        SaveLocation found = Run(Game(), [Rule("WinAppDataLocal", "Game/saves")], local, SaveSource.Learned);

        Assert.Equal(SupportState.Unsafe, found.Support);
        Assert.Equal(SaveSource.Learned, found.Source);
        Assert.Equal(nameof(SafetyProblem.ProtectedFolder), found.Reason);
    }

    [Fact]
    public void UnsafeLauncherFolderIsUnsafe()
    {
        string local = Path.Combine(Steamuser, "AppData", "Local");
        Touch(Path.Combine(local, "x", "a.sav"));

        SaveLocation found = Run(Game(), [Rule("WinAppDataLocal", "")]);

        Assert.Equal(SupportState.Unsafe, found.Support);
        Assert.Equal(SaveSource.Launcher, found.Source);
    }

    [Fact]
    public void SteamUserdataGameIsUnchanged()
    {
        string userdata = Path.Combine(folder.Root, "userdata", "111", "123450");
        Touch(Path.Combine(userdata, "remote", "a.sav"));

        SaveLocation found = Run(Game(userdata, SupportState.Supported, SaveSource.Userdata), []);

        Assert.Equal(userdata, found.Folder);
        Assert.Equal(SaveSource.Userdata, found.Source);
        Assert.Equal(SupportState.Supported, found.Support);
    }

    [Fact]
    public void SteamUserdataGameWithSavesInDocumentsHasAnExtraSpot()
    {
        string userdata = Path.Combine(folder.Root, "userdata", "111", "123450");
        Touch(Path.Combine(userdata, "remote", "a.sav"));
        Touch(Path.Combine(Steamuser, "Documents", "Game", "b.sav"));

        SaveLocation found = Run(Game(userdata, SupportState.Supported, SaveSource.Userdata),
            [Rule("WinMyDocuments", "Game")]);

        Assert.Equal(userdata, found.Folder);
        Assert.Equal(SupportState.Supported, found.Support);
        Assert.Equal(2, found.Spots.Count);
        Assert.Equal(1, found.Spots[0].Id);
        Assert.Equal(Path.Combine(Steamuser, "Documents", "Game"), found.Spots[1].Folder);
        Assert.NotEqual(1, found.Spots[1].Id);
    }

    [Fact]
    public void GameWithoutRulesKeepsItsSupport()
    {
        string userdata = Path.Combine(folder.Root, "userdata", "111", "123450");
        Touch(Path.Combine(userdata, "remotecache.vdf"));

        SaveLocation found = Run(Game(userdata, SupportState.SavesElsewhere, SaveSource.Userdata), []);

        Assert.Equal(SupportState.SavesElsewhere, found.Support);
        Assert.Equal(userdata, found.Folder);
    }

    [Fact]
    public void OneUnsafeSpotMakesTheWholeGameUnsafe()
    {
        Touch(Path.Combine(Steamuser, "AppData", "Local", "Game", "a", "x.sav"));
        Touch(Path.Combine(Steamuser, "AppData", "Local", "x", "y.sav"));

        SaveLocation found = Run(Game(), [
            Rule("WinAppDataLocal", "Game/a"),
            Rule("WinAppDataLocal", "")
        ]);

        Assert.Equal(SupportState.Unsafe, found.Support);
        Assert.Equal(SaveSource.Launcher, found.Source);
    }

    // Un gioco che nella 1.0 funzionava con la userdata non deve diventare Unsafe.
    [Fact]
    public void UnsafeRuleFolderOfAUserdataGameIsLeftOut()
    {
        string userdata = Path.Combine(folder.Root, "userdata", "111", "123450");
        Touch(Path.Combine(userdata, "remote", "a.sav"));
        Touch(Path.Combine(Steamuser, "Documents", "Game", "b.sav"));
        Touch(Path.Combine(Steamuser, "AppData", "Local", "x", "y.sav"));

        SaveLocation found = Run(Game(userdata, SupportState.Supported, SaveSource.Userdata), [
            Rule("WinMyDocuments", "Game"),
            Rule("WinAppDataLocal", "")
        ]);

        Assert.Equal(SupportState.Supported, found.Support);
        Assert.Equal(SaveSource.Userdata, found.Source);
        Assert.Equal(new[] { userdata, Path.Combine(Steamuser, "Documents", "Game") },
            found.Spots.Select(spot => spot.Folder));
    }

    [Fact]
    public void UnsafeChosenExtraFolderOfAUserdataGameIsStillUnsafe()
    {
        string userdata = Path.Combine(folder.Root, "userdata", "111", "123450");
        Touch(Path.Combine(userdata, "remote", "a.sav"));
        Touch(Path.Combine(Steamuser, "AppData", "Local", "x", "y.sav"));
        string local = Path.Combine(Steamuser, "AppData", "Local");
        var stored = new GameSpots(Path.Combine(folder.Root, "gamedata"));
        stored.Load();
        stored.Add(local, "", local, SaveSource.Chosen);

        SaveLocation found = SaveLocator.Locate(Game(userdata, SupportState.Supported, SaveSource.Userdata),
            [], [], Context(), [], LssData, stored);

        Assert.Equal(SupportState.Unsafe, found.Support);
    }
}
