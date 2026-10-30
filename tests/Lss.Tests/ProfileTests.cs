namespace Lss.Tests;

public sealed class ProfileTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string ProfileFile(SaveManager manager, string profile) =>
        Path.Combine(manager.ProfilesPath, profile, "remote", "slot.sav");

    [Fact]
    public void CreateProfile_CopiesCurrentSaves()
    {
        SaveManager manager = folder.CreateManager("A1");

        Result result = manager.CreateProfile("Mario", copyCurrentSaves: true, TestFolder.Owner);

        Assert.True(result.Success, result.Message);
        Assert.Equal("A1", File.ReadAllText(ProfileFile(manager, "Profilo_Mario")));
        Assert.False(File.Exists(Path.Combine(manager.ProfilesPath, "Profilo_Mario", "remotecache.vdf")));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" spazio iniziale")]
    [InlineData("a/b")]
    public void CreateProfile_RejectsInvalidNames(string name)
    {
        SaveManager manager = folder.CreateManager();

        Assert.False(manager.CreateProfile(name, copyCurrentSaves: false, TestFolder.Owner).Success);
    }

    [Fact]
    public void ActivateProfile_ReplacesGameSavesAndKeepsSteamCache()
    {
        SaveManager manager = folder.CreateManager("A1");
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Luigi", false, TestFolder.Owner);

        Result result = manager.ActivateProfile("Profilo_Luigi");

        Assert.True(result.Success, result.Message);
        Assert.Equal("Profilo_Luigi", manager.State.ActiveProfile);
        Assert.False(File.Exists(folder.SaveFile));
        Assert.Equal("cache", File.ReadAllText(folder.CacheFile));
        Assert.Equal("A1", File.ReadAllText(Path.Combine(result.BackupPath!, "remote", "slot.sav")));
    }

    [Fact]
    public void SaveProgress_CopiesGameSavesIntoActiveProfile()
    {
        SaveManager manager = folder.CreateManager("A1");
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.ActivateProfile("Profilo_Mario");
        TestFolder.Write(folder.SaveFile, "A2");

        Assert.True(manager.HasUnsavedProgress());

        Result result = manager.SaveProgress();

        Assert.True(result.Success, result.Message);
        Assert.Equal("A2", File.ReadAllText(ProfileFile(manager, "Profilo_Mario")));
        Assert.False(manager.HasUnsavedProgress());
        Assert.Equal("Prima_salvataggio_Profilo_Mario", manager.GetBackups()[0].Name);
    }

    [Fact]
    public void SaveProgress_RefusesEmptyGameSavesUnlessAllowed()
    {
        SaveManager manager = folder.CreateManager("A1");
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.ActivateProfile("Profilo_Mario");
        File.Delete(folder.SaveFile);

        Assert.False(manager.SaveProgress().Success);
        Assert.True(File.Exists(ProfileFile(manager, "Profilo_Mario")));

        Assert.True(manager.SaveProgress(allowEmptySaves: true).Success);
        Assert.False(File.Exists(ProfileFile(manager, "Profilo_Mario")));
    }

    [Fact]
    public void RenameProfile_UpdatesActiveProfileAndMovesSavestates()
    {
        SaveManager manager = folder.CreateManager();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.ActivateProfile("Profilo_Mario");
        manager.CreateSavestate("Boss");

        Result result = manager.RenameProfile("Profilo_Mario", "Super");

        Assert.True(result.Success, result.Message);
        Assert.Equal("Profilo_Super", manager.State.ActiveProfile);
        Assert.False(manager.ProfileExists("Profilo_Mario"));
        Assert.Single(manager.GetSavestates());

        manager.ReloadState();
        Assert.Equal("Profilo_Super", manager.State.ActiveProfile);
    }

    [Fact]
    public void RenameProfile_RefusesTheActivePlaythroughWhileTheGameRuns()
    {
        var provider = new FakeProvider();
        var manager = new SaveManager(folder.Game, folder.Data, "", null, provider);
        TestFolder.Write(folder.SaveFile, "M");
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Luigi", true, TestFolder.Owner);
        manager.ActivateProfile("Profilo_Mario");
        provider.Running = true;

        Result active = manager.RenameProfile("profilo_mario", "Super");
        Result other = manager.RenameProfile("Profilo_Luigi", "Bros");

        Assert.False(active.Success);
        Assert.True(manager.ProfileExists("Profilo_Mario"));
        Assert.True(other.Success, other.Message);
    }

    [Fact]
    public void DeleteProfile_RefusesTheActiveOneWithADifferentCase()
    {
        SaveManager manager = folder.CreateManager();
        manager.CreateProfile("Anna", true, TestFolder.Owner);
        manager.ActivateProfile("Profilo_Anna");

        Result result = manager.DeleteProfile("profilo_anna");

        Assert.False(result.Success);
        Assert.Equal(Strings.CannotDeleteActive, result.Message);
        Assert.True(manager.ProfileExists("Profilo_Anna"));
    }

    // L'utente ha cancellato la cartella dei salvataggi del gioco per ricominciare: il cambio deve funzionare.
    [Fact]
    public void ActivateProfile_WorksWhenTheGameSaveFolderIsMissing()
    {
        SaveManager manager = folder.CreateManager("A1");
        manager.CreateProfile("Anna", true, TestFolder.Owner);
        Directory.Delete(folder.Game, true);

        Result result = manager.ActivateProfile("Profilo_Anna");

        Assert.True(result.Success, result.Message);
        Assert.Equal("A1", File.ReadAllText(folder.SaveFile));
    }

    [Fact]
    public void RenameProfile_AllowsChangingOnlyTheCase()
    {
        SaveManager manager = folder.CreateManager();
        manager.CreateProfile("Mario", true, TestFolder.Owner);

        Assert.True(manager.RenameProfile("Profilo_Mario", "MARIO").Success);
        Assert.Contains("Profilo_MARIO", manager.GetProfiles());
    }

    [Fact]
    public void RenameProfile_RejectsExistingName()
    {
        SaveManager manager = folder.CreateManager();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Luigi", true, TestFolder.Owner);

        Assert.False(manager.RenameProfile("Profilo_Mario", "Luigi").Success);
        Assert.True(manager.ProfileExists("Profilo_Mario"));
    }

    [Fact]
    public void DeleteProfile_RefusesActiveProfile()
    {
        SaveManager manager = folder.CreateManager();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.ActivateProfile("Profilo_Mario");

        Assert.False(manager.DeleteProfile("Profilo_Mario").Success);
        Assert.True(manager.ProfileExists("Profilo_Mario"));
    }

    [Fact]
    public void DeleteProfile_MovesProfileToBackupsAndDeletesSavestates()
    {
        SaveManager manager = folder.CreateManager("A1");
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Luigi", true, TestFolder.Owner);
        manager.ActivateProfile("Profilo_Mario");
        manager.CreateSavestate("Boss");
        manager.ActivateProfile("Profilo_Luigi");

        Result result = manager.DeleteProfile("Profilo_Mario");

        Assert.True(result.Success, result.Message);
        Assert.False(manager.ProfileExists("Profilo_Mario"));
        Assert.Equal(0, manager.CountSavestates("Profilo_Mario"));

        Snapshot backup = manager.GetBackups()[0];
        Assert.Equal("Eliminato_Profilo_Mario", backup.Name);
        Assert.Equal("A1", File.ReadAllText(Path.Combine(backup.Path, "remote", "slot.sav")));
    }

    [Fact]
    public void NamesDifferingOnlyByCaseAreTheSamePlaythrough()
    {
        SaveManager manager = folder.CreateManager("A1");
        manager.CreateProfile("Mario", false, TestFolder.Owner);

        Result result = manager.CreateProfile("mario", false, TestFolder.Owner);

        Assert.False(result.Success);
        Assert.Equal(Strings.ProfileExists, result.Message);
        Assert.True(manager.ProfileExists("Profilo_mario"));
    }

    [Fact]
    public void ImportedNameGetsTheFreeNameIgnoringCase()
    {
        SaveManager manager = folder.CreateManager("A1");
        manager.CreateProfile("Mario", false, TestFolder.Owner);

        Assert.Equal("mario 2", manager.UniqueName("mario"));

        manager.CreateProfile("mario 2", false, TestFolder.Owner);

        Assert.Equal("mario 3", manager.UniqueName("mario"));
    }

    [Fact]
    public void RenameToOtherCaseKeepsNoteAndSavestates()
    {
        SaveManager manager = folder.CreateManager("A1");
        manager.CreateProfile("Mario", false, TestFolder.Owner);
        manager.SetNote("Profilo_Mario", "Missione");
        TestFolder.Write(Path.Combine(manager.SavestatePath, "Profilo_Mario", "punto1", "slot.sav"), "p");

        Result result = manager.RenameProfile("Profilo_Mario", "MARIO");

        Assert.True(result.Success, result.Message);
        Assert.Equal("Missione", manager.GetInfo("Profilo_MARIO").Note);
        Assert.Equal(1, manager.CountSavestates("Profilo_MARIO"));
    }

    [Fact]
    public void RenameToAnotherPlaythroughsNameIsRefusedIgnoringCase()
    {
        SaveManager manager = folder.CreateManager("A1");
        manager.CreateProfile("Mario", false, TestFolder.Owner);
        manager.CreateProfile("Luigi", false, TestFolder.Owner);

        Result result = manager.RenameProfile("Profilo_Luigi", "MARIO");

        Assert.False(result.Success);
        Assert.Equal(Strings.ProfileExists, result.Message);
    }
}
