namespace Lss.Tests;

/// <summary>
/// SaveFolderSafety: cartelle vere in una cartella temporanea, con una casa
/// finta (Documents dentro) e un prefisso Proton finto.
/// </summary>
public sealed class SaveFolderSafetyTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string Home => Path.Combine(folder.Root, "home");
    private string Documents => Path.Combine(Home, "Documents");
    private string Prefix => Path.Combine(folder.Root, "pfx");
    private string Steamuser => Path.Combine(Prefix, "drive_c", "users", "steamuser");
    private string Library => Path.Combine(folder.Root, "lib");
    private string Install => Path.Combine(Library, "steamapps", "common", "Game");
    private string LssData => Path.Combine(folder.Root, "lssdata");

    private SaveContext Context(string prefix = "") => new(
        new KnownFolders(
            Home,
            Path.Combine(Home, "AppData", "Roaming"),
            Path.Combine(Home, "AppData", "Local"),
            Path.Combine(Home, "AppData", "LocalLow"),
            Documents,
            Path.Combine(Home, "Saved Games"),
            "",
            "",
            Path.Combine(Home, ".local", "share"),
            Path.Combine(Home, ".config"),
            "andrix"),
        prefix,
        false,
        Install,
        "123",
        IgnoreCase: false);

    private SafetyProblem Check(string path, string prefix = "", IReadOnlyList<string>? launchers = null) =>
        SaveFolderSafety.Check(path, Context(prefix), launchers ?? [], Path.Combine(folder.Root, "lssdata"));

    private static void Save(string path) => TestFolder.Write(Path.Combine(path, "a.sav"), "x");

    [Fact]
    public void DocumentsItselfIsRefused()
    {
        Save(Path.Combine(Documents, "x"));

        Assert.Equal(SafetyProblem.ProtectedFolder, Check(Documents));
    }

    [Fact]
    public void FolderInsideDocumentsIsAccepted()
    {
        Save(Path.Combine(Documents, "Game"));

        Assert.Equal(SafetyProblem.None, Check(Path.Combine(Documents, "Game")));
    }

    [Fact]
    public void ProfileIsRefused()
    {
        Save(Path.Combine(Documents, "x"));

        Assert.Equal(SafetyProblem.ProtectedFolder, Check(Home));
    }

    [Fact]
    public void DriveRootIsRefused()
    {
        string root = Path.GetPathRoot(folder.Root)!;

        Assert.Equal(SafetyProblem.ProtectedFolder, Check(root));
    }

    [Fact]
    public void ProtonPrefixAndSteamUserAreRefused()
    {
        Save(Path.Combine(Steamuser, "Documents", "Game"));

        Assert.Equal(SafetyProblem.ProtectedFolder, Check(Prefix, prefix: Prefix));
        Assert.Equal(SafetyProblem.ProtectedFolder, Check(Steamuser, prefix: Prefix));
    }

    [Fact]
    public void InstallationAndLauncherFoldersAreRefused()
    {
        Save(Path.Combine(Install, "Game.exe"));
        Save(Path.Combine(Library, "steamapps", "appmanifest_1.acf"));

        Assert.Equal(SafetyProblem.ProtectedFolder, Check(Install));
        Assert.Equal(
            SafetyProblem.ProtectedFolder,
            Check(Library, launchers: [Library]));
    }

    [Fact]
    public void FolderInsideTheInstallationIsAccepted()
    {
        Save(Path.Combine(Install, "Saves"));

        Assert.Equal(SafetyProblem.None, Check(Path.Combine(Install, "Saves")));
    }

    [Fact]
    public void LssDataIsRefused()
    {
        Save(Path.Combine(LssData, "Saves"));

        Assert.Equal(SafetyProblem.InsideLssData, Check(Path.Combine(LssData, "Saves")));
    }

    [Fact]
    public void EmptyAndMissingFoldersAreRefused()
    {
        string empty = Path.Combine(folder.Root, "vuota");
        Directory.CreateDirectory(empty);

        Assert.Equal(SafetyProblem.Empty, Check(empty));
        Assert.Equal(SafetyProblem.Missing, Check(Path.Combine(folder.Root, "non-c-e")));
    }

    [Fact]
    public void TooManyFilesAreRefused()
    {
        string saves = Path.Combine(folder.Root, "saves");
        TestFolder.Write(Path.Combine(saves, "1.sav"), "x");
        TestFolder.Write(Path.Combine(saves, "2.sav"), "x");
        TestFolder.Write(Path.Combine(saves, "sub", "3.sav"), "x");

        SafetyProblem tooMany = SaveFolderSafety.Check(saves, Context(), [], LssData, long.MaxValue, maxFiles: 2);
        SafetyProblem enough = SaveFolderSafety.Check(saves, Context(), [], LssData, long.MaxValue, maxFiles: 3);

        Assert.Equal(SafetyProblem.TooBig, tooMany);
        Assert.Equal(SafetyProblem.None, enough);
    }

    [Fact]
    public void TooManyBytesAreRefused()
    {
        string saves = Path.Combine(folder.Root, "saves");
        TestFolder.Write(Path.Combine(saves, "1.sav"), new string('x', 20));

        Assert.Equal(
            SafetyProblem.TooBig,
            SaveFolderSafety.Check(saves, Context(), [], LssData, maxBytes: 10, maxFiles: 100));
    }

    [Fact]
    public void LinkToDocumentsIsRefusedOnLinux()
    {
        if (!OperatingSystem.IsLinux()) return;

        Save(Path.Combine(Documents, "x"));
        string link = Path.Combine(folder.Root, "collegamento");
        Directory.CreateSymbolicLink(link, Documents);

        Assert.Equal(SafetyProblem.ProtectedFolder, Check(link));
    }
}
