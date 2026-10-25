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

    private SaveLocation Run(GameInfo game, IReadOnlyList<SaveRule> rules, string chosen = "",
        SaveSource chosenSource = SaveSource.Chosen) =>
        SaveLocator.Locate(game, rules, [], Context(), [], LssData, chosen, chosenSource);

    [Fact]
    public void OneFolderIsSupported()
    {
        string saves = Path.Combine(Steamuser, "AppData", "Local", "Game", "saves");
        Touch(Path.Combine(saves, "a.sav"));

        SaveLocation found = Run(Game(), [Rule("WinAppDataLocal", "Game/saves")]);

        Assert.Equal(saves, found.Folder);
        Assert.Equal(SaveSource.Launcher, found.Source);
        Assert.Equal(SupportState.Supported, found.Support);
        Assert.False(found.HasOtherSaves);
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
    public void TwoFoldersAreUnsure()
    {
        Touch(Path.Combine(Steamuser, "AppData", "Local", "Game", "a", "x.sav"));
        Touch(Path.Combine(Steamuser, "AppData", "Local", "Game", "b", "y.sav"));

        SaveLocation found = Run(Game(), [
            Rule("WinAppDataLocal", "Game/a"),
            Rule("WinAppDataLocal", "Game/b")
        ]);

        Assert.Equal(SupportState.Unsure, found.Support);
        Assert.Equal("", found.Folder);
    }

    [Fact]
    public void OnlyFilesAreUnsure()
    {
        Touch(Path.Combine(Steamuser, "AppData", "Local", "Game", "a.sav"));

        SaveLocation found = Run(Game(), [Rule("WinAppDataLocal", "Game", "*.sav", recursive: false)]);

        Assert.Equal(SupportState.Unsure, found.Support);
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
        Assert.False(found.HasOtherSaves);
    }

    [Fact]
    public void SteamUserdataGameWithSavesInDocumentsHasOtherSaves()
    {
        string userdata = Path.Combine(folder.Root, "userdata", "111", "123450");
        Touch(Path.Combine(userdata, "remote", "a.sav"));
        Touch(Path.Combine(Steamuser, "Documents", "Game", "b.sav"));

        SaveLocation found = Run(Game(userdata, SupportState.Supported, SaveSource.Userdata),
            [Rule("WinMyDocuments", "Game")]);

        Assert.Equal(userdata, found.Folder);
        Assert.Equal(SupportState.Supported, found.Support);
        Assert.True(found.HasOtherSaves);
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
}
