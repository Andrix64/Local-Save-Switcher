namespace Lss.Tests;

/// <summary>
/// Gioco con la cartella dei salvataggi fuori da userdata (qui in una
/// Documents finta): tutto il giro di partite, il recupero e il controllo.
/// </summary>
public sealed class ExternalSavesTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string Documents => Path.Combine(folder.Root, "home", "Documents");
    private string Saves => Path.Combine(Documents, "Sandfall", "Saves");
    private string Slot => Path.Combine(Saves, "slot.sav");
    private string SteamRoot => Path.Combine(folder.Root, "Steam");
    private string Userdata => Path.Combine(SteamRoot, "userdata", "111");

    private static GameInfo Game(string saveFolder) =>
        new("steam", "123450", "Sandfall", "", "", "", GameInfo.SpotsOf(saveFolder, SaveSource.Launcher), SupportState.Supported, "", "", SaveSource.Launcher);

    private GameAccount Account => new("steam", "111", "Andrix", Userdata, null);

    /// <summary>
    /// Launcher finto che conosce solo le cartelle da proteggere (qui Documents).
    /// </summary>
    private FakeProvider Launcher(params string[] protectedFolders)
    {
        var provider = new FakeProvider { Id = "steam" };
        provider.Launchers.AddRange(protectedFolders);
        return provider;
    }

    private Result<SaveManager> Open(string saveFolder, FakeProvider? provider = null) =>
        SaveManager.Open(folder.Data, Account, Game(saveFolder), provider ?? Launcher(Documents));

    [Fact]
    public void RoundOfPlaythroughsInASaveFolderOutsideUserdata()
    {
        TestFolder.Write(Slot, "L");
        TestFolder.Write(Path.Combine(Documents, "Other", "keep.txt"), "x");

        SaveManager manager = Open(Saves).Value!;
        manager.CreateProfile("Luigi", true, manager.DefaultOwnerId);
        TestFolder.Write(Slot, "M");
        manager.CreateProfile("Mario", true, manager.DefaultOwnerId);

        Assert.True(manager.ActivateProfile("Profilo_Luigi").Success);
        Assert.Equal("L", File.ReadAllText(Slot));

        Assert.True(manager.ActivateProfile("Profilo_Mario").Success);
        Assert.Equal("M", File.ReadAllText(Slot));

        Assert.True(manager.ExportProfile("Profilo_Mario", Path.Combine(folder.Root, "mario.zip")).Success);
        Assert.Equal("x", File.ReadAllText(Path.Combine(Documents, "Other", "keep.txt")));
        Assert.Equal(new[] { "slot.sav" }, Directory.GetFiles(Saves).Select(file => Path.GetFileName(file)));
    }

    [Fact]
    public void AccountIsTheGivenOneNotTheFolderAboveTheSaves()
    {
        TestFolder.Write(Slot, "L");

        SaveManager manager = Open(Saves).Value!;

        Assert.Equal("111", manager.Account.Id);
        Assert.Equal(Userdata, manager.Account.Folder);
    }

    [Fact]
    public void InterruptedReplaceIsRecoveredByOpen()
    {
        // Interrotta tra i due spostamenti: resta solo la cartella vecchia.
        TestFolder.Write(Path.Combine(Saves + FileOps.OldSuffix, "slot.sav"), "vecchio");

        Result<SaveManager> opened = Open(Saves);

        Assert.True(opened.Success);
        Assert.Equal("vecchio", File.ReadAllText(Slot));
        Assert.False(Directory.Exists(Saves + FileOps.OldSuffix));
    }

    [Fact]
    public void FolderThatBecameUnsafeIsRefusedWhenOpened()
    {
        TestFolder.Write(Slot, "L");

        Result<SaveManager> opened = Open(Documents);

        Assert.False(opened.Success);
        Assert.NotEqual("", opened.Message);
    }

    [Fact]
    public void CloudStateIsReadFromTheAccountFolder()
    {
        TestFolder.Write(Slot, "L");
        TestFolder.Write(Path.Combine(Userdata, "7", "remote", "sharedconfig.vdf"),
            "\"apps\"\n{\n\t\"123450\"\n\t{\n\t\t\"cloudenabled\"\t\t\"0\"\n\t}\n}\n");
        var platform = new FakePlatform();
        platform.SteamRoots.Add(SteamRoot);
        var steam = new SteamProvider(platform);

        Result<SaveManager> opened = SaveManager.Open(folder.Data, Account, Game(Saves), steam);

        Assert.Equal(CloudState.Disabled, opened.Value!.GetCloudState());
    }
}
