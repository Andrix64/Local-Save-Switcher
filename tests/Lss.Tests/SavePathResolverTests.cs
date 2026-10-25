namespace Lss.Tests;

/// <summary>
/// SavePathResolver con una casa e un prefisso finti in una cartella
/// temporanea. Le regole di Windows girano dove il prefisso le rende
/// Windows; quelle di Linux senza prefisso solo su Linux.
/// </summary>
public sealed class SavePathResolverTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string Home => Path.Combine(folder.Root, "home");
    private string Prefix => Path.Combine(folder.Root, "pfx");
    private string Steamuser => Path.Combine(Prefix, "drive_c", "users", "steamuser");

    private KnownFolders Folders(string documents = "") => new(
        Home,
        Path.Combine(Home, "AppData", "Roaming"),
        Path.Combine(Home, "AppData", "Local"),
        Path.Combine(Home, "AppData", "LocalLow"),
        documents,
        Path.Combine(Home, "Saved Games"),
        "",
        "",
        Path.Combine(Home, ".local", "share"),
        Path.Combine(Home, ".config"),
        "andrix");

    private SaveContext PrefixContext(string accountId = "123") =>
        new(Folders(), Prefix, true, "", accountId, IgnoreCase: false);

    private static SaveRule Rule(string root, string path, string pattern = "*", bool recursive = true,
        params string[] platforms) => new(root, path, pattern, recursive, platforms);

    private static void Touch(string path)
    {
        TestFolder.Write(path, "x");
    }

    [Fact]
    public void ProtonPrefixUsesSteamUser()
    {
        Touch(Path.Combine(Steamuser, "AppData", "Local", "Game", "Saves", "slot.sav"));

        IReadOnlyList<string> found = SavePathResolver.Resolve(
            Rule("WinAppDataLocal", "Game/Saves", "*", true, "Windows"), [], PrefixContext());

        Assert.Equal(new[] { Path.Combine(Steamuser, "AppData", "Local", "Game", "Saves") }, found);
    }

    [Fact]
    public void WinePrefixUsesTheLinuxUserName()
    {
        string wine = Path.Combine(Prefix, "drive_c", "users", "andrix", "AppData", "Local", "Game");
        Touch(Path.Combine(wine, "a.sav"));

        IReadOnlyList<string> found = SavePathResolver.Resolve(
            Rule("WinAppDataLocal", "Game", "*.sav", false), [], PrefixContext());

        Assert.Equal(new[] { Path.Combine(wine, "a.sav") }, found);
    }

    [Fact]
    public void MyDocumentsFallsBackToMyDocumentsInThePrefix()
    {
        string saves = Path.Combine(Steamuser, "My Documents", "Game");
        Touch(Path.Combine(saves, "a.sav"));

        IReadOnlyList<string> found = SavePathResolver.Resolve(
            Rule("WinMyDocuments", "Game"), [], PrefixContext());

        Assert.Equal(new[] { saves }, found);
    }

    [Fact]
    public void DocumentsInThePrefixIsUsedBeforeMyDocuments()
    {
        string documents = Path.Combine(Steamuser, "Documents", "Game");
        Touch(Path.Combine(documents, "a.sav"));
        Touch(Path.Combine(Steamuser, "My Documents", "Game", "b.sav"));

        IReadOnlyList<string> found = SavePathResolver.Resolve(
            Rule("WinMyDocuments", "Game"), [], PrefixContext());

        Assert.Equal(new[] { documents }, found);
    }

    [Fact]
    public void SteamIdTokensAreReplaced()
    {
        string saves = Path.Combine(Steamuser, "AppData", "Local", "Game", "76561197960265851");
        string steam3 = Path.Combine(Steamuser, "AppData", "Local", "Game", "123");
        Touch(Path.Combine(saves, "a.sav"));
        Touch(Path.Combine(steam3, "b.sav"));

        IReadOnlyList<string> byId = SavePathResolver.Resolve(
            Rule("WinAppDataLocal", "Game/{64BitSteamID}"), [], PrefixContext("123"));
        IReadOnlyList<string> byNumber = SavePathResolver.Resolve(
            Rule("WinAppDataLocal", "Game/{Steam3AccountID}"), [], PrefixContext("123"));

        Assert.Equal(new[] { saves }, byId);
        Assert.Equal(new[] { steam3 }, byNumber);
    }

    [Fact]
    public void UnknownTokenSkipsTheRule()
    {
        Touch(Path.Combine(Steamuser, "AppData", "Local", "Game", "a.sav"));

        IReadOnlyList<string> found = SavePathResolver.Resolve(
            Rule("WinAppDataLocal", "{Mistero}/Game"), [], PrefixContext());

        Assert.Empty(found);
    }

    [Fact]
    public void StarDotStarIsTheSameAsStar()
    {
        string saves = Path.Combine(Steamuser, "AppData", "Local", "Game");
        Touch(Path.Combine(saves, "a.sav"));
        Touch(Path.Combine(saves, "b.dat"));

        IReadOnlyList<string> star = SavePathResolver.Resolve(
            Rule("WinAppDataLocal", "Game", "*", recursive: true), [], PrefixContext());
        IReadOnlyList<string> starDot = SavePathResolver.Resolve(
            Rule("WinAppDataLocal", "Game", "*.*", recursive: true), [], PrefixContext());

        Assert.Equal(new[] { saves }, star);
        Assert.Equal(star, starDot);
    }

    [Fact]
    public void RecursiveSavFindsFilesInSubfolders()
    {
        string saves = Path.Combine(Steamuser, "AppData", "Local", "Game");
        Touch(Path.Combine(saves, "a.sav"));
        Touch(Path.Combine(saves, "sub", "b.sav"));
        Touch(Path.Combine(saves, "c.dat"));

        IReadOnlyList<string> recursive = SavePathResolver.Resolve(
            Rule("WinAppDataLocal", "Game", "*.sav", recursive: true), [], PrefixContext());

        Assert.Equal(
            new[] { Path.Combine(saves, "a.sav"), Path.Combine(saves, "sub", "b.sav") }.Select(Path.GetFullPath),
            recursive);
    }

    [Fact]
    public void NonRecursiveSavGivesOnlyTopLevelFiles()
    {
        string saves = Path.Combine(Steamuser, "AppData", "Local", "Game");
        Touch(Path.Combine(saves, "a.sav"));
        Touch(Path.Combine(saves, "sub", "b.sav"));

        IReadOnlyList<string> found = SavePathResolver.Resolve(
            Rule("WinAppDataLocal", "Game", "*.sav", recursive: false), [], PrefixContext());

        Assert.Equal(new[] { Path.Combine(saves, "a.sav") }, found);
    }

    [Fact]
    public void StarInSegmentMatchesEveryFolder()
    {
        string first = Path.Combine(Steamuser, "AppData", "Local", "Save1", "x");
        string second = Path.Combine(Steamuser, "AppData", "Local", "Save2", "x");
        Touch(Path.Combine(first, "a.sav"));
        Touch(Path.Combine(second, "a.sav"));

        IReadOnlyList<string> found = SavePathResolver.Resolve(
            Rule("WinAppDataLocal", "Save*/x"), [], PrefixContext());

        Assert.Equal(new[] { first, second }.Select(Path.GetFullPath).OrderBy(p => p, StringComparer.Ordinal), found);
    }

    [Fact]
    public void CaseDoesNotMatterInsideThePrefix()
    {
        string documents = Path.Combine(Steamuser, "Documents", "My Games", "x");
        Touch(Path.Combine(documents, "a.sav"));

        IReadOnlyList<string> found = SavePathResolver.Resolve(
            Rule("WinMyDocuments", "my games/X"), [], PrefixContext() with { IgnoreCase = true });

        Assert.Equal(new[] { documents }, found);
    }

    [Fact]
    public void CaseMattersWithoutIgnoreCaseOnLinux()
    {
        if (!OperatingSystem.IsLinux()) return;

        string documents = Path.Combine(Steamuser, "Documents", "My Games");
        Touch(Path.Combine(documents, "a.sav"));

        IReadOnlyList<string> found = SavePathResolver.Resolve(
            Rule("WinMyDocuments", "my games"), [], PrefixContext());

        Assert.Empty(found);
    }

    // Percorsi come li scrive Steam: "/HotlineMiami" (Hotline Miami), "/" e "'.'".
    [Theory]
    [InlineData("/Game")]
    [InlineData("Game/")]
    [InlineData("'.'/Game")]
    [InlineData("./Game")]
    public void LeadingSlashAndDotAreRelativeToTheRoot(string path)
    {
        string saves = Path.Combine(Steamuser, "AppData", "Local", "Game");
        Touch(Path.Combine(saves, "SaveData.sav"));

        IReadOnlyList<string> found = SavePathResolver.Resolve(
            Rule("WinAppDataLocal", path, "SaveData.sav", false), [], PrefixContext());

        Assert.Equal(new[] { Path.Combine(saves, "SaveData.sav") }, found);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("'.'")]
    public void SlashOrQuotedDotAloneIsTheRoot(string path)
    {
        string root = Path.Combine(Steamuser, "AppData", "Local");
        Touch(Path.Combine(root, "savedata.vfs"));

        IReadOnlyList<string> found = SavePathResolver.Resolve(
            Rule("WinAppDataLocal", path, "savedata.vfs", false), [], PrefixContext());

        Assert.Equal(new[] { Path.Combine(root, "savedata.vfs") }, found);
    }

    [Fact]
    public void ParentFolderIsRejected()
    {
        Touch(Path.Combine(Steamuser, "AppData", "Local", "Game", "a.sav"));

        IReadOnlyList<string> found = SavePathResolver.Resolve(
            Rule("WinAppDataLocal", "../Local/Game"), [], PrefixContext());

        Assert.Empty(found);
    }

    [Fact]
    public void MacRootsGiveNothing()
    {
        Touch(Path.Combine(Home, "Library", "Saves", "a.sav"));

        IReadOnlyList<string> found = SavePathResolver.Resolve(
            Rule("MacAppSupport", "Library/Saves"), [], PrefixContext());

        Assert.Empty(found);
    }

    [Fact]
    public void RulesForAnotherPlatformAreSkipped()
    {
        Touch(Path.Combine(Steamuser, "AppData", "Local", "Game", "a.sav"));

        IReadOnlyList<string> found = SavePathResolver.Resolve(
            Rule("WinAppDataLocal", "Game", platforms: "Linux"), [], PrefixContext());

        Assert.Empty(found);
    }

    [Fact]
    public void XdgDataHomeWithoutPrefix()
    {
        if (!OperatingSystem.IsLinux()) return;

        string saves = Path.Combine(Home, ".local", "share", "Game");
        Touch(Path.Combine(saves, "a.sav"));
        var context = new SaveContext(Folders(), "", false, "", "", IgnoreCase: false);

        IReadOnlyList<string> found = SavePathResolver.Resolve(
            Rule("LinuxXdgDataHome", "Game", platforms: "Linux"), [], context);

        Assert.Equal(new[] { saves }, found);
    }

    [Fact]
    public void OverrideWithPathTransformsOnLinux()
    {
        if (!OperatingSystem.IsLinux()) return;

        string saves = Path.Combine(Home, ".local", "share", "Disco", "SaveGames");
        Touch(Path.Combine(saves, "a.sav"));
        var context = new SaveContext(Folders(), "", false, "", "", IgnoreCase: false);
        var over = new RootOverride("WinAppDataLocalLow", "Linux", "=", "LinuxXdgDataHome", "",
            [("ZAUM Studio/Disco", "Disco")]);

        IReadOnlyList<string> found = SavePathResolver.Resolve(
            Rule("WinAppDataLocalLow", "ZAUM Studio/Disco/SaveGames", platforms: "Linux"), [over], context);

        Assert.Equal(new[] { saves }, found);
    }

    [Fact]
    public void OverrideWithAddPathFromDocumentsToHomeOnLinux()
    {
        if (!OperatingSystem.IsLinux()) return;

        string saves = Path.Combine(Home, "Games", "Game");
        Touch(Path.Combine(saves, "a.sav"));
        var context = new SaveContext(Folders(Path.Combine(Home, "Documents")), "", false, "", "", IgnoreCase: false);
        var over = new RootOverride("WinMyDocuments", "Linux", "=", "LinuxHome", "Games", []);

        IReadOnlyList<string> found = SavePathResolver.Resolve(
            Rule("WinMyDocuments", "Game", platforms: "Linux"), [over], context);

        Assert.Equal(new[] { saves }, found);
    }

    [Fact]
    public void OtherComparisonIsSkippedOnLinux()
    {
        if (!OperatingSystem.IsLinux()) return;

        string documents = Path.Combine(Home, "Documents", "Game");
        Touch(Path.Combine(documents, "a.sav"));
        var context = new SaveContext(Folders(Path.Combine(Home, "Documents")), "", false, "", "", IgnoreCase: false);
        var over = new RootOverride("WinMyDocuments", "Linux", "!=", "LinuxHome", "", []);

        IReadOnlyList<string> found = SavePathResolver.Resolve(
            Rule("WinMyDocuments", "Game", platforms: "Linux"), [over], context);

        Assert.Empty(found);
    }

    [Fact]
    public void WindowsRootsAreEmptyOnNativeLinux()
    {
        if (!OperatingSystem.IsLinux()) return;

        var context = new SaveContext(Folders(Path.Combine(Home, "Documents")), "", false, "", "", IgnoreCase: false);

        Assert.Equal("", SavePathResolver.RootFolder("WinMyDocuments", context));
        Assert.Equal(Home, SavePathResolver.RootFolder("LinuxHome", context));
    }
}
