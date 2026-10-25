namespace Lss.Tests;

/// <summary>
/// SaveManager con un gioco di tre posti (una cartella, una seconda cartella e
/// un pattern *.sav in una terza): cambio di profilo, ripristino di backup e di
/// savestate spostano tutti i posti, e un cambio interrotto si rimette a posto
/// all'apertura del gioco.
/// </summary>
public sealed class SaveManagerSpotsTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string Game1 => folder.Game;
    private string Game2 => Path.Combine(folder.Root, "second");
    private string Game3 => Path.Combine(folder.Root, "loose");

    private GameInfo Game() => new("steam", "123450", "Test", "", "", "",
        [
            new SaveSpot(1, Game1, "", true, Game1, SaveSource.Chosen),
            new SaveSpot(2, Game2, "", true, Game2, SaveSource.Chosen),
            new SaveSpot(3, Game3, "*.sav", true, Game3, SaveSource.Chosen),
        ],
        SupportState.Supported);

    private SaveSet Set() => new(Game().Spots);

    private SaveManager Manager() => new(Game1, folder.Data, "", Game());

    private string ProfileFolder(string name) => Path.Combine(folder.Data, "Profili", "Profilo_" + name);

    private void WriteGame(string value)
    {
        TestFolder.Write(Path.Combine(Game1, "slot.sav"), value);
        TestFolder.Write(Path.Combine(Game2, "settings.ini"), value);
        TestFolder.Write(Path.Combine(Game3, "slot.sav"), value);
        TestFolder.Write(Path.Combine(Game3, "config.ini"), "keep");
    }

    private void AssertGame(string value)
    {
        Assert.Equal(value, File.ReadAllText(Path.Combine(Game1, "slot.sav")));
        Assert.Equal(value, File.ReadAllText(Path.Combine(Game2, "settings.ini")));
        Assert.Equal(value, File.ReadAllText(Path.Combine(Game3, "slot.sav")));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(Game3, "config.ini")));
    }

    [Fact]
    public void ActivatingAProfileMovesEverySpot()
    {
        WriteGame("anna");
        Set().CopyGameTo(ProfileFolder("Anna"));
        WriteGame("changed");
        TestFolder.Write(Path.Combine(Game3, "extra.sav"), "only now");

        Result result = Manager().ActivateProfile("Profilo_Anna");

        Assert.True(result.Success);
        AssertGame("anna");
        Assert.False(File.Exists(Path.Combine(Game3, "extra.sav")));
    }

    [Fact]
    public void RestoringABackupMovesEverySpotBack()
    {
        WriteGame("before");
        Set().CopyGameTo(ProfileFolder("Anna"));
        WriteGame("changed");
        SaveManager manager = Manager();

        Assert.True(manager.ActivateProfile("Profilo_Anna").Success);
        AssertGame("before");

        Snapshot backup = manager.GetBackups()[0];

        Assert.True(manager.RestoreBackup(backup).Success);
        AssertGame("changed");
    }

    [Fact]
    public void RestoringASavestateMovesEverySpot()
    {
        WriteGame("anna");
        Set().CopyGameTo(ProfileFolder("Anna"));
        SaveManager manager = Manager();
        Assert.True(manager.ActivateProfile("Profilo_Anna").Success);

        string savestate = Path.Combine(folder.Data, "Savestate", "Profilo_Anna", "2026-10-25_10-00-00");
        WriteGame("point");
        Set().CopyGameTo(savestate);
        WriteGame("changed");

        Snapshot point = manager.GetSavestates()[0];

        Assert.True(manager.RestoreSavestate(point).Success);
        AssertGame("point");
    }

    [Fact]
    public void InterruptedActivationIsUndoneWhenTheGameIsOpened()
    {
        string dataRoot = Path.Combine(folder.Root, "lss");
        string basePath = Path.Combine(dataRoot, "steam", "111", "123450");
        string backup = Path.Combine(folder.Root, "backup-before");

        WriteGame("before");
        Set().CopyGameTo(backup);

        // Cambio a metà: il primo posto è già cambiato, il secondo è tra i due spostamenti.
        TestFolder.Write(Path.Combine(Game1, "slot.sav"), "anna");
        Directory.Move(Game2, Game2 + FileOps.OldSuffix);
        Directory.CreateDirectory(basePath);
        File.WriteAllLines(Path.Combine(basePath, "cambio.ini"),
        [
            "Snapshot=" + Path.Combine(folder.Root, "anna"),
            "Backup=" + backup,
            "Posti=1,2,3",
            "Fatti=1",
        ]);

        var account = new GameAccount("steam", "111", "Andrea", Path.Combine(folder.Root, "userdata", "111"), null);
        Result<SaveManager> opened = SaveManager.Open(dataRoot, account, Game());

        Assert.True(opened.Success);
        Assert.True(opened.Value!.SwitchInterruptedRestored);
        AssertGame("before");
        Assert.False(File.Exists(Path.Combine(basePath, "cambio.ini")));
    }

    [Fact]
    public void OpeningAGameWithoutAnInterruptedSwitchSetsNoFlag()
    {
        WriteGame("before");
        var account = new GameAccount("steam", "111", "Andrea", Path.Combine(folder.Root, "userdata", "111"), null);

        Result<SaveManager> opened = SaveManager.Open(Path.Combine(folder.Root, "lss"), account, Game());

        Assert.True(opened.Success);
        Assert.False(opened.Value!.SwitchInterruptedRestored);
        AssertGame("before");
    }

    [Fact]
    public void UnsavedProgressInTheSecondSpotIsDetected()
    {
        WriteGame("first");
        SaveManager manager = Manager();
        Assert.True(manager.CreateProfile("Anna", copyCurrentSaves: true).Success);
        Assert.True(manager.ActivateProfile("Profilo_Anna").Success);
        Assert.False(manager.HasUnsavedProgress());

        TestFolder.Write(Path.Combine(Game2, "settings.ini"), "progress");

        Assert.True(manager.HasUnsavedProgress());
    }

    // Come nella 1.0: la cartella principale vuota non svuota la partita, anche se un altro posto ha file.
    [Fact]
    public void SavingIsRefusedWhenOnlyAnExtraSpotHasFiles()
    {
        WriteGame("first");
        SaveManager manager = Manager();
        Assert.True(manager.CreateProfile("Anna", copyCurrentSaves: true).Success);
        Assert.True(manager.ActivateProfile("Profilo_Anna").Success);

        File.Delete(Path.Combine(Game1, "slot.sav"));

        Result saved = manager.SaveProgress();

        Assert.False(saved.Success);
        Assert.Equal("first", File.ReadAllText(Path.Combine(ProfileFolder("Anna"), "slot.sav")));
    }

    [Fact]
    public void AFullRoundKeepsEverySpot()
    {
        WriteGame("first");
        SaveManager manager = Manager();
        Assert.True(manager.CreateProfile("Anna", copyCurrentSaves: true).Success);
        WriteGame("second");
        Assert.True(manager.CreateProfile("Bea", copyCurrentSaves: true).Success);

        // Cambio: Anna prende i salvataggi di prima; il gioco diventa "first".
        Assert.True(manager.ActivateProfile("Profilo_Anna").Success);
        AssertGame("first");

        // Progressi solo nel secondo posto, poi salvataggio nel profilo.
        TestFolder.Write(Path.Combine(Game2, "settings.ini"), "progress");
        Assert.True(manager.SaveProgress().Success);
        Assert.Equal("progress", File.ReadAllText(Path.Combine(ProfileFolder("Anna"), ".lss-posizioni", "2", "settings.ini")));

        // Savestate, poi il gioco cambia e si torna al savestate.
        Assert.True(manager.CreateSavestate("point").Success);
        WriteGame("changed");
        Snapshot point = manager.GetSavestates()[0];

        Assert.True(manager.RestoreSavestate(point).Success);
        Assert.Equal("progress", File.ReadAllText(Path.Combine(Game2, "settings.ini")));
        Assert.Equal("first", File.ReadAllText(Path.Combine(Game1, "slot.sav")));

        // Il profilo di Bea si elimina con tutti i suoi posti.
        Assert.True(manager.DeleteProfile("Profilo_Bea").Success);
        Assert.False(Directory.Exists(ProfileFolder("Bea")));
    }

    private string NewSpot => Path.Combine(folder.Root, "newspot");

    private SaveSpot NewSpotSpec() => new(0, NewSpot, "", true, NewSpot, SaveSource.Chosen);

    private string Extra(int id) => Path.Combine(".lss-posizioni", id.ToString());

    [Fact]
    public void AddingASpotCopiesItIntoTheActivePlaythroughAndTheOthers()
    {
        WriteGame("first");
        SaveManager manager = Manager();
        Assert.True(manager.CreateProfile("Anna", copyCurrentSaves: true).Success);
        Assert.True(manager.CreateProfile("Bea", copyCurrentSaves: true).Success);
        Assert.True(manager.ActivateProfile("Profilo_Anna").Success);
        TestFolder.Write(Path.Combine(NewSpot, "n.sav"), "new");

        Assert.True(manager.AddSpot(NewSpotSpec(), NewSpotFill.CopyCurrent).Success);

        int id = manager.Game.Spots.Single(spot => spot.Folder == NewSpot).Id;
        Assert.Equal("new", File.ReadAllText(Path.Combine(ProfileFolder("Anna"), Extra(id), "n.sav")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(ProfileFolder("Bea"), Extra(id), "n.sav")));
    }

    [Fact]
    public void AnotherPlaythroughStartsEmptyWhenAsked()
    {
        WriteGame("first");
        SaveManager manager = Manager();
        Assert.True(manager.CreateProfile("Anna", copyCurrentSaves: true).Success);
        Assert.True(manager.CreateProfile("Bea", copyCurrentSaves: true).Success);
        Assert.True(manager.ActivateProfile("Profilo_Anna").Success);
        TestFolder.Write(Path.Combine(NewSpot, "n.sav"), "new");

        Assert.True(manager.AddSpot(NewSpotSpec(), NewSpotFill.Empty).Success);

        int id = manager.Game.Spots.Single(spot => spot.Folder == NewSpot).Id;
        Assert.Equal("new", File.ReadAllText(Path.Combine(ProfileFolder("Anna"), Extra(id), "n.sav")));
        Assert.True(Directory.Exists(Path.Combine(ProfileFolder("Bea"), Extra(id))));
        Assert.Empty(Directory.GetFiles(Path.Combine(ProfileFolder("Bea"), Extra(id)), "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void AddingASpotWhileTheGameRunsIsRefused()
    {
        var provider = new FakeProvider { Running = true };
        var manager = new SaveManager(Game1, folder.Data, "", Game(), provider);
        TestFolder.Write(Path.Combine(NewSpot, "n.sav"), "new");

        Assert.False(manager.AddSpot(NewSpotSpec(), NewSpotFill.CopyCurrent).Success);
        Assert.DoesNotContain(manager.Game.Spots, spot => spot.Folder == NewSpot);
    }

    [Fact]
    public void AFailureInTheMiddleLeavesNoSpotFoldersBehind()
    {
        WriteGame("first");
        SaveManager manager = Manager();
        Assert.True(manager.CreateProfile("Anna", copyCurrentSaves: true).Success);
        Assert.True(manager.CreateProfile("Bea", copyCurrentSaves: true).Success);
        Assert.True(manager.ActivateProfile("Profilo_Anna").Success);
        TestFolder.Write(Path.Combine(NewSpot, "n.sav"), "new");

        // Il nuovo posto avrà id 4 (dopo i tre del gioco): nella seconda partita non si può creare la sua cartella.
        TestFolder.Write(Path.Combine(ProfileFolder("Bea"), Extra(4)), "in the way");

        Assert.False(manager.AddSpot(NewSpotSpec(), NewSpotFill.CopyCurrent).Success);

        Assert.False(Directory.Exists(Path.Combine(ProfileFolder("Anna"), Extra(4))));
        Assert.DoesNotContain(manager.Game.Spots, spot => spot.Folder == NewSpot);
    }

    [Fact]
    public void RemovingASpotKeepsTheCopiesInThePlaythroughs()
    {
        WriteGame("first");
        SaveManager manager = Manager();
        Assert.True(manager.CreateProfile("Anna", copyCurrentSaves: true).Success);
        Assert.True(manager.ActivateProfile("Profilo_Anna").Success);
        TestFolder.Write(Path.Combine(NewSpot, "n.sav"), "new");
        Assert.True(manager.AddSpot(NewSpotSpec(), NewSpotFill.CopyCurrent).Success);
        int id = manager.Game.Spots.Single(spot => spot.Folder == NewSpot).Id;

        Assert.True(manager.RemoveSpot(id).Success);

        Assert.DoesNotContain(manager.Game.Spots, spot => spot.Id == id);
        Assert.Equal("new", File.ReadAllText(Path.Combine(ProfileFolder("Anna"), Extra(id), "n.sav")));
    }

    private string Zip => Path.Combine(folder.Root, "export.zip");

    private void AssertProfile(string profile, string value)
    {
        Assert.Equal(value, File.ReadAllText(Path.Combine(profile, "slot.sav")));
        Assert.Equal(value, File.ReadAllText(Path.Combine(profile, FileOps.ExtraSpotsFolder, "2", "settings.ini")));
        Assert.Equal(value, File.ReadAllText(Path.Combine(profile, FileOps.ExtraSpotsFolder, "3", "slot.sav")));
    }

    [Fact]
    public void ExportAndImportOnTheSamePcKeepEverySpot()
    {
        WriteGame("first");
        SaveManager manager = Manager();
        Assert.True(manager.CreateProfile("Anna", copyCurrentSaves: true).Success);
        Assert.True(manager.ExportProfile("Profilo_Anna", Zip).Success);

        Result<string> imported = manager.ImportProfile(Zip);

        Assert.True(imported.Success);
        AssertProfile(Path.Combine(folder.Data, "Profili", imported.Value!), "first");
    }

    [Fact]
    public void AnotherPcMatchesTheSpotsByTheirPortablePath()
    {
        WriteGame("first");
        SaveManager manager = Manager();
        Assert.True(manager.CreateProfile("Anna", copyCurrentSaves: true).Success);
        Assert.True(manager.ExportProfile("Profilo_Anna", Zip).Success);

        // Un altro computer: gli stessi percorsi portabili, altre cartelle vere.
        string other = Path.Combine(folder.Root, "pc2");
        var pc2Game = new GameInfo("steam", "123450", "Test", "", "", "",
            [
                new SaveSpot(1, Path.Combine(other, "a"), "", true, Game1, SaveSource.Chosen),
                new SaveSpot(2, Path.Combine(other, "b"), "", true, Game2, SaveSource.Chosen),
                new SaveSpot(3, Path.Combine(other, "c"), "*.sav", true, Game3, SaveSource.Chosen),
            ],
            SupportState.Supported);
        var pc2 = new SaveManager(Path.Combine(other, "a"), Path.Combine(folder.Root, "pc2-data"), "", pc2Game);

        Result<string> imported = pc2.ImportProfile(Zip);

        Assert.True(imported.Success);
        Assert.Equal("", imported.Message);
        AssertProfile(Path.Combine(folder.Root, "pc2-data", "Profili", imported.Value!), "first");
    }

    [Fact]
    public void SpotMissingOnTheOtherPcIsSkippedAndListed()
    {
        WriteGame("first");
        SaveManager manager = Manager();
        Assert.True(manager.CreateProfile("Anna", copyCurrentSaves: true).Success);
        Assert.True(manager.ExportProfile("Profilo_Anna", Zip).Success);

        // Questo computer non ha il secondo posto.
        var pc3Game = new GameInfo("steam", "123450", "Test", "", "", "",
            [
                new SaveSpot(1, Path.Combine(folder.Root, "pc3", "a"), "", true, Game1, SaveSource.Chosen),
                new SaveSpot(3, Path.Combine(folder.Root, "pc3", "c"), "*.sav", true, Game3, SaveSource.Chosen),
            ],
            SupportState.Supported);
        var pc3 = new SaveManager(Path.Combine(folder.Root, "pc3", "a"), Path.Combine(folder.Root, "pc3-data"), "", pc3Game);

        Result<string> imported = pc3.ImportProfile(Zip);

        Assert.True(imported.Success);
        Assert.Contains(Game2, imported.Message);
        Assert.False(Directory.Exists(Path.Combine(folder.Root, "pc3-data", "Profili", imported.Value!, FileOps.ExtraSpotsFolder, "2")));
    }
}
