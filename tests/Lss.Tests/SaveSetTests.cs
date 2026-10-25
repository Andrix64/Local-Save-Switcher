namespace Lss.Tests;

/// <summary>
/// SaveSet: le operazioni su tutti i posti di un gioco. Tre posti: due cartelle
/// intere (1 e 2) e un pattern *.sav ricorsivo nella terza (3).
/// </summary>
public sealed class SaveSetTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string Game1 => Path.Combine(folder.Root, "game1");
    private string Game2 => Path.Combine(folder.Root, "game2");
    private string Game3 => Path.Combine(folder.Root, "game3");
    private string Snapshot => Path.Combine(folder.Root, "snapshot");
    private string Backup => Path.Combine(folder.Root, "backup");
    private string Journal => Path.Combine(folder.Root, "cambio.ini");
    private string Extra(int id) => Path.Combine(Snapshot, ".lss-posizioni", id.ToString());

    private SaveSet ThreeSpots() => new(
    [
        new SaveSpot(1, Game1, "", true, Game1, SaveSource.Chosen),
        new SaveSpot(2, Game2, "", true, Game2, SaveSource.Chosen),
        new SaveSpot(3, Game3, "*.sav", true, Game3, SaveSource.Chosen),
    ]);

    private void FillGame()
    {
        TestFolder.Write(Path.Combine(Game1, "slot.sav"), "one");
        TestFolder.Write(Path.Combine(Game2, "settings.ini"), "two");
        TestFolder.Write(Path.Combine(Game3, "slot.sav"), "three");
        TestFolder.Write(Path.Combine(Game3, "sub", "deep.sav"), "deep");
        TestFolder.Write(Path.Combine(Game3, "config.ini"), "not a save");
    }

    [Fact]
    public void CopyGameToPutsEverySpotInItsPlace()
    {
        FillGame();

        ThreeSpots().CopyGameTo(Snapshot);

        Assert.Equal("one", File.ReadAllText(Path.Combine(Snapshot, "slot.sav")));
        Assert.Equal("two", File.ReadAllText(Path.Combine(Extra(2), "settings.ini")));
        Assert.Equal("three", File.ReadAllText(Path.Combine(Extra(3), "slot.sav")));
        Assert.Equal("deep", File.ReadAllText(Path.Combine(Extra(3), "sub", "deep.sav")));
        Assert.False(File.Exists(Path.Combine(Extra(3), "config.ini")));
    }

    [Fact]
    public void GameEqualsDetectsUnsavedProgressInTheSecondSpotOnly()
    {
        FillGame();
        ThreeSpots().CopyGameTo(Snapshot);

        Assert.True(ThreeSpots().GameEquals(Snapshot));

        TestFolder.Write(Path.Combine(Game2, "settings.ini"), "changed");

        Assert.False(ThreeSpots().GameEquals(Snapshot));
    }

    [Fact]
    public void GameEqualsIgnoresAnExtraSpotMissingFromTheSnapshot()
    {
        FillGame();
        ThreeSpots().CopyGameTo(Snapshot);
        Directory.Delete(Extra(2), true);

        Assert.True(ThreeSpots().GameEquals(Snapshot));
    }

    [Fact]
    public void GameIsEmptyOnlyWhenNoSpotHasSaves()
    {
        Directory.CreateDirectory(Game1);
        Directory.CreateDirectory(Game2);
        TestFolder.Write(Path.Combine(Game3, "config.ini"), "not a save");

        Assert.True(ThreeSpots().GameIsEmpty());

        TestFolder.Write(Path.Combine(Game2, "settings.ini"), "two");

        Assert.False(ThreeSpots().GameIsEmpty());
    }

    [Fact]
    public void ReplaceGameFromRestoresEverySpotAndKeepsTheFilesNotMatched()
    {
        FillGame();
        ThreeSpots().CopyGameTo(Snapshot);

        TestFolder.Write(Path.Combine(Game1, "slot.sav"), "changed");
        File.Delete(Path.Combine(Game2, "settings.ini"));
        TestFolder.Write(Path.Combine(Game3, "new.sav"), "new");
        TestFolder.Write(Path.Combine(Game3, "config.ini"), "changed too");

        ThreeSpots().ReplaceGameFrom(Snapshot, backup: Backup, journal: Journal);

        Assert.Equal("one", File.ReadAllText(Path.Combine(Game1, "slot.sav")));
        Assert.Equal("two", File.ReadAllText(Path.Combine(Game2, "settings.ini")));
        Assert.Equal("three", File.ReadAllText(Path.Combine(Game3, "slot.sav")));
        Assert.False(File.Exists(Path.Combine(Game3, "new.sav")));
        Assert.Equal("changed too", File.ReadAllText(Path.Combine(Game3, "config.ini")));
    }

    [Fact]
    public void SnapshotWithoutSpotThreeLeavesSpotThreeAsItIs()
    {
        FillGame();
        ThreeSpots().CopyGameTo(Snapshot);
        Directory.Delete(Extra(3), true);
        TestFolder.Write(Path.Combine(Game3, "slot.sav"), "changed");

        ThreeSpots().ReplaceGameFrom(Snapshot, backup: Backup, journal: Journal);

        Assert.Equal("changed", File.ReadAllText(Path.Combine(Game3, "slot.sav")));
    }

    [Fact]
    public void EmptyExtraFolderEmptiesItsSpot()
    {
        FillGame();
        ThreeSpots().CopyGameTo(Snapshot);
        Directory.Delete(Extra(3), true);
        Directory.CreateDirectory(Extra(3));

        ThreeSpots().ReplaceGameFrom(Snapshot, backup: Backup, journal: Journal);

        Assert.Empty(Directory.GetFiles(Game3, "*.sav", SearchOption.AllDirectories));
        Assert.True(File.Exists(Path.Combine(Game3, "config.ini")));
    }

    [Fact]
    public void ReplaceSnapshotFromGameMakesEverySpotIdentical()
    {
        FillGame();
        ThreeSpots().CopyGameTo(Snapshot);

        TestFolder.Write(Path.Combine(Game2, "settings.ini"), "newer");
        File.Delete(Path.Combine(Game3, "sub", "deep.sav"));

        ThreeSpots().ReplaceSnapshotFromGame(Snapshot);

        Assert.Equal("newer", File.ReadAllText(Path.Combine(Extra(2), "settings.ini")));
        Assert.False(File.Exists(Path.Combine(Extra(3), "sub", "deep.sav")));
        Assert.True(ThreeSpots().GameEquals(Snapshot));
    }

    [Fact]
    public void WithOneWholeFolderSpotItDoesWhatFileOpsDoes()
    {
        FillGame();
        var single = new SaveSet([new SaveSpot(1, Game1, "", true, Game1, SaveSource.Chosen)]);
        string viaFileOps = Path.Combine(folder.Root, "via-fileops");

        single.CopyGameTo(Snapshot);
        FileOps.CopyFolderVerified(Game1, viaFileOps);

        Assert.True(FileOps.FoldersAreEqual(Snapshot, viaFileOps));

        TestFolder.Write(Path.Combine(Game1, "slot.sav"), "changed");
        single.ReplaceGameFrom(Snapshot, backup: Backup, journal: Journal);

        Assert.True(FileOps.FoldersAreEqual(Game1, Snapshot));
        Assert.True(single.GameEquals(Snapshot));
        Assert.False(single.GameIsEmpty());
    }

    [Fact]
    public void FailureOnThirdSpotPutsTheFirstTwoBackFromTheBackup()
    {
        FillGame();
        ThreeSpots().CopyGameTo(Snapshot);
        TestFolder.Write(Path.Combine(Snapshot, "slot.sav"), "target");
        TestFolder.Write(Path.Combine(Extra(2), "settings.ini"), "target");
        TestFolder.Write(Path.Combine(Extra(3), "slot.sav"), "target");
        ThreeSpots().CopyGameTo(Backup);

        // Il terzo posto non si può scrivere: una cartella con il nome della copia.
        Directory.CreateDirectory(Path.Combine(Game3, "slot.sav.nuovo"));

        Assert.ThrowsAny<Exception>(() => ThreeSpots().ReplaceGameFrom(Snapshot, Backup, Journal));

        Assert.Equal("one", File.ReadAllText(Path.Combine(Game1, "slot.sav")));
        Assert.Equal("two", File.ReadAllText(Path.Combine(Game2, "settings.ini")));
        Assert.False(File.Exists(Journal));
    }

    [Fact]
    public void InterruptedSwitchIsUndoneByRecoverInterrupted()
    {
        FillGame();
        ThreeSpots().CopyGameTo(Backup);

        // Cambio a metà: il posto 1 è già cambiato, il posto 2 è tra i due spostamenti.
        TestFolder.Write(Path.Combine(Game1, "slot.sav"), "switched");
        Directory.Move(Game2, Game2 + FileOps.OldSuffix);
        File.WriteAllLines(Journal,
        [
            "Snapshot=" + Snapshot,
            "Backup=" + Backup,
            "Posti=1,2,3",
            "Fatti=1",
        ]);

        bool restored = ThreeSpots().RecoverInterrupted(Journal, Backup);

        Assert.True(restored);
        Assert.Equal("one", File.ReadAllText(Path.Combine(Game1, "slot.sav")));
        Assert.Equal("two", File.ReadAllText(Path.Combine(Game2, "settings.ini")));
        Assert.Equal("three", File.ReadAllText(Path.Combine(Game3, "slot.sav")));
        Assert.False(File.Exists(Journal));
    }

    [Fact]
    public void WithoutAJournalNothingHappens()
    {
        FillGame();
        TestFolder.Write(Path.Combine(Game1, "slot.sav"), "kept");

        Assert.False(ThreeSpots().RecoverInterrupted(Journal, Backup));
        Assert.Equal("kept", File.ReadAllText(Path.Combine(Game1, "slot.sav")));
    }

    [Fact]
    public void SuccessfulSwitchLeavesNoJournal()
    {
        FillGame();
        ThreeSpots().CopyGameTo(Snapshot);
        TestFolder.Write(Path.Combine(Game1, "slot.sav"), "changed");

        ThreeSpots().ReplaceGameFrom(Snapshot, Backup, Journal);

        Assert.Equal("one", File.ReadAllText(Path.Combine(Game1, "slot.sav")));
        Assert.False(File.Exists(Journal));
    }
}
