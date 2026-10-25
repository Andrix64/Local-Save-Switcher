using System.IO.Compression;

namespace Lss.Tests;

/// <summary>
/// Funzioni della nuova interfaccia: informazioni della partita, cronologia,
/// partite eliminate, opzioni condivise, esportazione e importazione.
/// </summary>
public sealed class PlaythroughTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string Preferences => Path.Combine(folder.Game, "remote", "preferences_data");

    /// <summary>
    /// "Mario" (nel gioco, salvataggi "M") e "Luigi" (salvataggi "L").
    /// </summary>
    private SaveManager TwoPlaythroughs()
    {
        SaveManager manager = folder.CreateManager("L");
        manager.CreateProfile("Luigi", true);
        TestFolder.Write(folder.SaveFile, "M");
        manager.CreateProfile("Mario", true);
        manager.ActivateProfile("Profilo_Mario");
        return manager;
    }

    [Fact]
    public void NoteAndPlayTimeAreSavedAndFollowRename()
    {
        SaveManager manager = TwoPlaythroughs();
        var start = new DateTime(2026, 10, 22, 20, 0, 0);

        Assert.True(manager.SetNote("Profilo_Mario", "  Missione Polaris  ").Success);
        manager.AddPlayTime("Profilo_Mario", start, start.AddMinutes(90));
        manager.AddPlayTime("Profilo_Mario", start.AddHours(3), start.AddHours(3).AddMinutes(30));

        PlaythroughInfo info = manager.GetInfo("Profilo_Mario");
        Assert.Equal("Missione Polaris", info.Note);
        Assert.Equal(TimeSpan.FromMinutes(120), info.PlayTime);
        Assert.Equal(start.AddHours(3).AddMinutes(30), info.LastSession);

        manager.RenameProfile("Profilo_Mario", "Super Mario");
        Assert.Equal("Missione Polaris", manager.GetInfo("Profilo_Super Mario").Note);
        Assert.Equal(PlaythroughInfo.Empty, manager.GetInfo("Profilo_Mario"));
    }

    [Fact]
    public void NoteTooLongIsRefused()
    {
        SaveManager manager = TwoPlaythroughs();

        Assert.False(manager.SetNote("Profilo_Mario", new string('x', SaveManager.MaxNoteLength + 1)).Success);
    }

    [Fact]
    public void BackupsKnowTheirPlaythrough()
    {
        SaveManager manager = TwoPlaythroughs();

        // Mario nel gioco, si passa a Luigi: la copia contiene Mario.
        manager.ActivateProfile("Profilo_Luigi");
        Snapshot activation = manager.GetBackups()[0];
        Assert.Equal("Profilo_Mario", activation.Owner);
        Assert.Equal(BackupKind.Activate, activation.Kind);
        Assert.Equal("Prima di passare a «Luigi»", SaveManager.BackupDescription(activation));
        Assert.Equal("M", File.ReadAllText(Path.Combine(activation.Path, "remote", "slot.sav")));

        TestFolder.Write(folder.SaveFile, "L2");
        manager.SaveProgress(onGameExit: true);
        Snapshot save = manager.GetBackups()[0];
        Assert.Equal("Profilo_Luigi", save.Owner);
        Assert.Equal("Prima del salvataggio alla chiusura del gioco", SaveManager.BackupDescription(save));

        manager.CreateSavestate("Boss");
        manager.RestoreSavestate(manager.GetSavestates()[0]);
        Assert.Equal("Prima di tornare a «Boss»", SaveManager.BackupDescription(manager.GetBackups()[0]));

        Assert.All(manager.GetBackupsOf("Profilo_Luigi"), b => Assert.Equal("Profilo_Luigi", b.Owner));
        Assert.Contains(manager.GetBackupsOf("Profilo_Mario"), b => b.Path == activation.Path);
    }

    [Fact]
    public void PruningAlsoDeletesBackupInfo()
    {
        SaveManager manager = TwoPlaythroughs();
        manager.SetBackupsToKeep(1);

        Assert.Single(manager.GetBackups());
        Assert.Single(Directory.GetFiles(manager.BackupPath, "*.ini"));
    }

    [Fact]
    public void DeletedPlaythroughCanBeRecoveredWithItsInfo()
    {
        SaveManager manager = TwoPlaythroughs();
        manager.SetNote("Profilo_Luigi", "Run veloce");
        manager.AddPlayTime("Profilo_Luigi", DateTime.Now.AddHours(-1), DateTime.Now);

        manager.DeleteProfile("Profilo_Luigi");
        Snapshot deleted = Assert.Single(manager.GetDeletedProfiles());
        Assert.Equal("Partita «Luigi» eliminata", SaveManager.BackupDescription(deleted));

        // Nel frattempo il nome è stato riusato.
        manager.CreateProfile("Luigi", false);

        Result result = manager.RecoverDeletedProfile(deleted);

        Assert.True(result.Success, result.Message);
        Assert.Contains("Profilo_Luigi 2", manager.GetProfiles());
        Assert.Equal("L", File.ReadAllText(Path.Combine(manager.ProfilesPath, "Profilo_Luigi 2", "remote", "slot.sav")));
        Assert.Equal("Run veloce", manager.GetInfo("Profilo_Luigi 2").Note);
        Assert.True(manager.GetInfo("Profilo_Luigi 2").PlayTime > TimeSpan.FromMinutes(59));
        Assert.Empty(manager.GetDeletedProfiles());
    }

    [Fact]
    public void ExportAndImportRoundTrip()
    {
        SaveManager manager = TwoPlaythroughs();
        manager.CreateSavestate("Boss");
        manager.SetNote("Profilo_Mario", "Polaris");
        string zip = Path.Combine(folder.Root, "mario.zip");

        Result exported = manager.ExportProfile("Profilo_Mario", zip);
        Assert.True(exported.Success, exported.Message);

        // Importata nello stesso account: il nome è già usato.
        Result<string> imported = manager.ImportProfile(zip);

        Assert.True(imported.Success, imported.Message);
        Assert.Equal("Profilo_Mario 2", imported.Value);
        Assert.True(FileOps.FoldersAreEqual(
            Path.Combine(manager.ProfilesPath, "Profilo_Mario"),
            Path.Combine(manager.ProfilesPath, "Profilo_Mario 2")));
        Assert.Equal("Polaris", manager.GetInfo("Profilo_Mario 2").Note);
        Assert.Equal(1, manager.CountSavestates("Profilo_Mario 2"));
    }

    [Fact]
    public void ImportRejectsOtherZips()
    {
        SaveManager manager = TwoPlaythroughs();
        string zip = Path.Combine(folder.Root, "altro.zip");

        using (ZipArchive archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            archive.CreateEntry("leggimi.txt");

        Assert.False(manager.ImportProfile(zip).Success);
        Assert.False(manager.ImportProfile(Path.Combine(folder.Root, "non-esiste.zip")).Success);
        Assert.Equal(2, manager.GetProfiles().Count);
    }

    [Fact]
    public void Export_WritesFormat4WithGameAndProvider()
    {
        SaveManager manager = TwoPlaythroughs();
        string zip = Path.Combine(folder.Root, "formato4.zip");

        Assert.True(manager.ExportProfile("Profilo_Mario", zip).Success);

        using ZipArchive archive = ZipFile.OpenRead(zip);
        string info = ReadEntry(archive, "partita.ini");

        Assert.Contains("Formato=4", info);
        Assert.Contains("Provider=steam", info);
        Assert.Contains("AppId=870780", info);
        Assert.Contains("Gioco=Control", info);
    }

    [Fact]
    public void Export_UsesForwardSlashes()
    {
        SaveManager manager = TwoPlaythroughs();
        string zip = Path.Combine(folder.Root, "barre.zip");

        Assert.True(manager.ExportProfile("Profilo_Mario", zip).Success);

        using ZipArchive archive = ZipFile.OpenRead(zip);
        Assert.NotEmpty(archive.Entries);
        Assert.All(archive.Entries, entry => Assert.DoesNotContain("\\", entry.FullName));
    }

    [Fact]
    public void Import_Format2IsSteam()
    {
        string zip = WriteZip("formato2.zip", "Formato=2\nAppId=870780\nGioco=Control\nNome=Mario\n");
        SaveManager control = folder.CreateManager();

        Assert.True(control.ImportProfile(zip).Success);
        Assert.Contains("Profilo_Mario", control.GetProfiles());
    }

    [Fact]
    public void Import_OtherLauncherIsRefused()
    {
        string zip = WriteZip("epic.zip", "Formato=3\nProvider=epic\nAppId=870780\nGioco=Control\nNome=Mario\n");
        var epicControl = new GameInfo("epic", GameInfo.ControlAppId, "Control", "", "", "", GameInfo.SpotsOf(folder.Game, SaveSource.Userdata), SupportState.Supported);
        SaveManager epic = new(folder.Game, Path.Combine(folder.Root, "epic"), "", epicControl);

        // Lo zip è di epic: entra nel gioco di epic...
        Assert.True(epic.ImportProfile(zip).Success);

        // ...e non nel Control di Steam, anche con lo stesso AppID.
        SaveManager steam = folder.CreateManager();
        Result<string> refused = steam.ImportProfile(zip);

        Assert.False(refused.Success);
        Assert.Equal(Loc.F(Strings.ImportOtherGame, "Control", "Control"), refused.Message);
    }

    [Fact]
    public void Import_Format1IsControl()
    {
        string zip = WriteZip("formato1.zip", "Formato=1\nNome=Mario\n");
        SaveManager control = folder.CreateManager();

        Assert.True(control.ImportProfile(zip).Success);
        Assert.Contains("Profilo_Mario", control.GetProfiles());

        SaveManager other = new(folder.Game, Path.Combine(folder.Root, "altro"), "", OtherGame);
        Result<string> refused = other.ImportProfile(zip);

        Assert.False(refused.Success);
        Assert.Equal(Loc.F(Strings.ImportOtherGame, "Control", "Test"), refused.Message);
        Assert.Empty(other.GetProfiles());
    }

    [Fact]
    public void Import_OtherGameRefused()
    {
        TestFolder.Write(folder.SaveFile, "partita");
        SaveManager other = new(folder.Game, Path.Combine(folder.Root, "altro"), "", OtherGame);
        other.CreateProfile("Mario", copyCurrentSaves: false);
        string zip = Path.Combine(folder.Root, "altro_gioco.zip");

        Assert.True(other.ExportProfile("Profilo_Mario", zip).Success);

        SaveManager control = folder.CreateManager();
        Result<string> refused = control.ImportProfile(zip);

        Assert.False(refused.Success);
        Assert.Empty(control.GetProfiles());
    }

    private static GameInfo OtherGame => new("steam", "123450", "Test", "", "", "", [], SupportState.Supported);

    private static string ReadEntry(ZipArchive archive, string name)
    {
        using var reader = new StreamReader(archive.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Zip fatto a mano, con partita.ini e una cartella salvataggi come quelli esportati.
    /// </summary>
    private string WriteZip(string name, string info)
    {
        string source = Path.Combine(folder.Root, name + "_src");
        TestFolder.Write(Path.Combine(source, "salvataggi", "slot.sav"), "partita");
        TestFolder.Write(Path.Combine(source, "partita.ini"), info);

        string zip = Path.Combine(folder.Root, name);
        ZipFile.CreateFromDirectory(source, zip);
        return zip;
    }

    [Fact]
    public void AdoptGameSavesKeepsGameFolderAndMarksItActive()
    {
        SaveManager manager = folder.CreateManager("partita");

        Result result = manager.AdoptGameSaves("Partita principale");

        Assert.True(result.Success, result.Message);
        Assert.Equal("Profilo_Partita principale", manager.State.ActiveProfile);
        Assert.False(manager.HasUnsavedProgress());
        Assert.Equal("partita", File.ReadAllText(folder.SaveFile));
    }

    [Fact]
    public void SharedPreferencesFollowTheGameAcrossSwitchesAndRestores()
    {
        SaveManager manager = TwoPlaythroughs();
        manager.State.SharePreferences = true;
        manager.State.Save();
        TestFolder.Write(Preferences, "opzioni di adesso");

        manager.ActivateProfile("Profilo_Luigi");
        Assert.Equal("opzioni di adesso", File.ReadAllText(Preferences));

        manager.CreateSavestate("Boss");
        TestFolder.Write(Preferences, "opzioni cambiate");
        manager.RestoreSavestate(manager.GetSavestates()[0]);
        Assert.Equal("opzioni cambiate", File.ReadAllText(Preferences));
    }

    [Fact]
    public void WithoutSharingPreferencesSwitchWithThePlaythrough()
    {
        SaveManager manager = TwoPlaythroughs();
        TestFolder.Write(Preferences, "opzioni di Mario");
        manager.SaveProgress();

        manager.ActivateProfile("Profilo_Luigi");

        Assert.False(File.Exists(Preferences));
    }

    [Fact]
    public void SharedFiles_DefaultForControl()
    {
        SaveManager manager = folder.CreateManager();

        Assert.Equal(new[] { "remote/preferences_data" }, manager.SharedFiles);
    }

    [Fact]
    public void SharedFiles_EmptyForOtherGames()
    {
        var game = new GameInfo("steam", "123450", "Test", "", "", "", GameInfo.SpotsOf(folder.Game, SaveSource.Userdata), SupportState.Supported);
        var manager = new SaveManager(folder.Game, folder.Data, "", game);

        Assert.Empty(manager.SharedFiles);
    }

    [Fact]
    public void SharedFiles_FromStateWinOverDefaults()
    {
        SaveManager manager = folder.CreateManager();
        manager.State.SharedFiles = new[] { "remote/a.cfg" };
        manager.State.Save();

        manager.ReloadState();

        Assert.Equal(new[] { "remote/a.cfg" }, manager.SharedFiles);
    }

    [Fact]
    public void SharedFiles_SeveralFilesKeptAcrossActivation()
    {
        var game = new GameInfo("steam", "123450", "Test", "", "", "", GameInfo.SpotsOf(folder.Game, SaveSource.Userdata), SupportState.Supported);
        SaveManager manager = new(folder.Game, folder.Data, "", game);
        manager.State.SharedFiles = new[] { "remote/a.cfg", "b.ini" };
        manager.State.SharePreferences = true;
        manager.State.Save();

        TestFolder.Write(folder.SaveFile, "L");
        manager.CreateProfile("Luigi", true);
        TestFolder.Write(folder.SaveFile, "M");
        manager.CreateProfile("Mario", true);
        manager.ActivateProfile("Profilo_Mario");

        TestFolder.Write(Path.Combine(folder.Game, "remote", "a.cfg"), "a adesso");
        TestFolder.Write(Path.Combine(folder.Game, "b.ini"), "b adesso");

        manager.ActivateProfile("Profilo_Luigi");

        Assert.Equal("a adesso", File.ReadAllText(Path.Combine(folder.Game, "remote", "a.cfg")));
        Assert.Equal("b adesso", File.ReadAllText(Path.Combine(folder.Game, "b.ini")));
    }

    [Fact]
    public void SharedFiles_PathOutsideGameIsIgnored()
    {
        // "../evil.txt" punterebbe fuori dalla cartella del gioco: non va letto né scritto.
        TestFolder.Write(Path.Combine(folder.Root, "evil.txt"), "fuori");
        SaveManager manager = folder.CreateManager();
        manager.State.SharedFiles = new[] { "../evil.txt" };
        manager.State.SharePreferences = true;
        manager.State.Save();
        manager.CreateProfile("Luigi", true);

        manager.ActivateProfile("Profilo_Luigi");

        Assert.Equal("fuori", File.ReadAllText(Path.Combine(folder.Root, "evil.txt")));
        Assert.False(File.Exists(Path.Combine(manager.ProfilesPath, "evil.txt")));
    }

    [Fact]
    public void SettingsArePersisted()
    {
        SaveManager manager = folder.CreateManager();
        manager.State.LaunchGame = false;
        manager.State.SaveOnGameExit = false;
        manager.State.SharePreferences = true;
        manager.State.Save();

        manager.ReloadState();

        Assert.False(manager.State.LaunchGame);
        Assert.False(manager.State.SaveOnGameExit);
        Assert.True(manager.State.SharePreferences);
    }
}
