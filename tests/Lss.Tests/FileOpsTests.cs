namespace Lss.Tests;

public sealed class FileOpsTests : IDisposable
{
    private readonly TestFolder folder = new();

    private string Source => Path.Combine(folder.Root, "sorgente");
    private string Destination => Path.Combine(folder.Root, "destinazione");

    public void Dispose() => folder.Dispose();

    [Fact]
    public void CopyAllVerified_CopiesExcludedFoldersToo()
    {
        // Lo spostamento tra dischi diversi copia tutto, anche "Backup" e "Profili",
        // che CopyFolder invece salta.
        TestFolder.Write(Path.Combine(Source, "123", "Profili", "Profilo_Mario", "remote", "slot.sav"), "mario");
        TestFolder.Write(Path.Combine(Source, "123", "Backup", "Prima_ripristino_x", "a.sav"), "backup");

        FileOps.CopyAllVerified(Source, Destination);

        Assert.Equal("backup", File.ReadAllText(Path.Combine(Destination, "123", "Backup", "Prima_ripristino_x", "a.sav")));
        Assert.True(File.Exists(Path.Combine(Destination, "123", "Profili", "Profilo_Mario", "remote", "slot.sav")));
        Assert.True(Directory.Exists(Source));
    }

    [Fact]
    public void ReplaceFolder_MakesDestinationEqualAndKeepsSteamCache()
    {
        TestFolder.Write(Path.Combine(Source, "remote", "a.sav"), "nuovo");
        TestFolder.Write(Path.Combine(Source, "remote", "sub", "b.sav"), "nuovo B");
        TestFolder.Write(Path.Combine(Destination, "remote", "a.sav"), "vecchio");
        TestFolder.Write(Path.Combine(Destination, "remote", "extra.sav"), "da cancellare");
        TestFolder.Write(Path.Combine(Destination, "remotecache.vdf"), "cache");

        FileOps.ReplaceFolder(Source, Destination);

        Assert.True(FileOps.FoldersAreEqual(Source, Destination));
        Assert.False(File.Exists(Path.Combine(Destination, "remote", "extra.sav")));
        Assert.Equal("cache", File.ReadAllText(Path.Combine(Destination, "remotecache.vdf")));
        Assert.False(Directory.Exists(Destination + ".nuovo"));
        Assert.False(Directory.Exists(Destination + ".vecchio"));
    }

    [Fact]
    public void ReplaceFolder_FallsBackToDirectCopyWhenDestinationIsInUse()
    {
        TestFolder.Write(Path.Combine(Source, "remote", "a.sav"), "nuovo");
        TestFolder.Write(Path.Combine(Destination, "remotecache.vdf"), "cache");

        // Un file aperto impedisce di spostare la cartella.
        using (new FileStream(Path.Combine(Destination, "remotecache.vdf"),
                   FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            FileOps.ReplaceFolder(Source, Destination);
        }

        Assert.True(FileOps.FoldersAreEqual(Source, Destination));
    }

    [Fact]
    public void ReplaceFolder_RecoversFromInterruptedSwap()
    {
        TestFolder.Write(Path.Combine(Source, "remote", "a.sav"), "nuovo");
        TestFolder.Write(Path.Combine(Destination + ".vecchio", "remotecache.vdf"), "cache");

        FileOps.ReplaceFolder(Source, Destination);

        Assert.True(FileOps.FoldersAreEqual(Source, Destination));
        Assert.True(File.Exists(Path.Combine(Destination, "remotecache.vdf")));
    }

    [Fact]
    public void ReplaceFolder_LeavesDestinationIntactWhenSourceIsMissing()
    {
        TestFolder.Write(Path.Combine(Destination, "remote", "a.sav"), "vecchio");

        Assert.Throws<DirectoryNotFoundException>(() =>
            FileOps.ReplaceFolder(Path.Combine(folder.Root, "manca"), Destination));

        Assert.Equal("vecchio", File.ReadAllText(Path.Combine(Destination, "remote", "a.sav")));
    }

    [Fact]
    public void FoldersAreEqual_DetectsDifferentContent()
    {
        TestFolder.Write(Path.Combine(Source, "a.sav"), "uno");
        TestFolder.Write(Path.Combine(Destination, "a.sav"), "due");

        Assert.False(FileOps.FoldersAreEqual(Source, Destination));
    }

    [Fact]
    public void FoldersAreEqual_IgnoresSteamCache()
    {
        TestFolder.Write(Path.Combine(Source, "a.sav"), "uno");
        TestFolder.Write(Path.Combine(Destination, "a.sav"), "uno");
        TestFolder.Write(Path.Combine(Destination, "remotecache.vdf"), "cache");

        Assert.True(FileOps.FoldersAreEqual(Source, Destination));
    }

    [Fact]
    public void HasNoSaveFiles_IgnoresSteamCache()
    {
        TestFolder.Write(Path.Combine(Source, "remotecache.vdf"), "cache");
        Directory.CreateDirectory(Path.Combine(Source, "remote"));

        Assert.True(FileOps.HasNoSaveFiles(Source));
    }

    [Fact]
    public void CopyKeepsUnixPermissions()
    {
        if (!OperatingSystem.IsLinux()) return;

        // Un file eseguibile (mode 0o755) resta eseguibile nella copia.
        const UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

        string file = Path.Combine(Source, "remote", "avvia.sh");
        TestFolder.Write(file, "#!/bin/sh");
        File.SetUnixFileMode(file, mode);

        FileOps.CopyFolderVerified(Source, Destination);

        Assert.Equal(mode, File.GetUnixFileMode(Path.Combine(Destination, "remote", "avvia.sh")));
    }

    [Fact]
    public void MatchedFiles_TakesOnlyTheMatchingFiles()
    {
        TestFolder.Write(Path.Combine(Source, "slot1.sav"), "a");
        TestFolder.Write(Path.Combine(Source, "config.ini"), "b");
        TestFolder.Write(Path.Combine(Source, "sub", "slot2.sav"), "c");
        TestFolder.Write(Path.Combine(Source, "remotecache.vdf"), "d");

        string[] flat = FileOps.MatchedFiles(Source, "*.sav", recursive: false).Select(Path.GetFileName).ToArray()!;
        string[] deep = FileOps.MatchedFiles(Source, "*.sav", recursive: true)
            .Select(file => Path.GetRelativePath(Source, file)).ToArray();

        Assert.Equal(["slot1.sav"], flat);
        Assert.Equal(["slot1.sav", Path.Combine("sub", "slot2.sav")], deep);
        Assert.Empty(FileOps.MatchedFiles(Source, "", recursive: true));
    }

    [Fact]
    public void MatchedFiles_SeveralMasksAndSteamCacheIsSkipped()
    {
        TestFolder.Write(Path.Combine(Source, "slot0.dat"), "a");
        TestFolder.Write(Path.Combine(Source, "remotecache.vdf"), "b");
        TestFolder.Write(Path.Combine(Source, "x.sav.nuovo"), "c");

        string[] found = FileOps.MatchedFiles(Source, "*.sav;slot?.dat;remotecache.vdf", recursive: false)
            .Select(Path.GetFileName).ToArray()!;

        Assert.Equal(["slot0.dat"], found);
    }

    [Fact]
    public void CopyMatchedVerified_CopiesOnlyTheMatchingFiles()
    {
        TestFolder.Write(Path.Combine(Source, "slot1.sav"), "a");
        TestFolder.Write(Path.Combine(Source, "config.ini"), "b");
        TestFolder.Write(Path.Combine(Source, "sub", "slot2.sav"), "c");

        FileOps.CopyMatchedVerified(Source, "*.sav", recursive: true, Destination);

        Assert.Equal("a", File.ReadAllText(Path.Combine(Destination, "slot1.sav")));
        Assert.Equal("c", File.ReadAllText(Path.Combine(Destination, "sub", "slot2.sav")));
        Assert.False(File.Exists(Path.Combine(Destination, "config.ini")));
    }

    [Fact]
    public void ReplaceMatched_TouchesOnlyTheMatchingFiles()
    {
        TestFolder.Write(Path.Combine(Source, "slot1.sav"), "from snapshot");
        TestFolder.Write(Path.Combine(Destination, "slot1.sav"), "old");
        TestFolder.Write(Path.Combine(Destination, "slot2.sav"), "only in game");
        TestFolder.Write(Path.Combine(Destination, "config.ini"), "kept");

        FileOps.ReplaceMatched(Source, Destination, "*.sav", recursive: false);

        Assert.Equal("from snapshot", File.ReadAllText(Path.Combine(Destination, "slot1.sav")));
        Assert.False(File.Exists(Path.Combine(Destination, "slot2.sav")));
        Assert.Equal("kept", File.ReadAllText(Path.Combine(Destination, "config.ini")));
        Assert.Empty(Directory.GetFiles(Destination, "*.nuovo"));
    }

    [Fact]
    public void MatchedEqual_ComparesNamesAndContent()
    {
        TestFolder.Write(Path.Combine(Source, "slot1.sav"), "a");
        TestFolder.Write(Path.Combine(Destination, "slot1.sav"), "a");
        TestFolder.Write(Path.Combine(Destination, "config.ini"), "different, not matched");

        Assert.True(FileOps.MatchedEqual(Destination, "*.sav", false, Source));

        TestFolder.Write(Path.Combine(Destination, "slot1.sav"), "b");

        Assert.False(FileOps.MatchedEqual(Destination, "*.sav", false, Source));
    }

    [Fact]
    public void RecoverInterruptedMatched_RemovesLeftoverCopies()
    {
        TestFolder.Write(Path.Combine(Destination, "slot1.sav"), "game");
        TestFolder.Write(Path.Combine(Destination, "slot1.sav.nuovo"), "copy");
        TestFolder.Write(Path.Combine(Destination, "config.ini.nuovo"), "not matched");

        Assert.True(FileOps.RecoverInterruptedMatched(Destination, "*.sav", recursive: false));

        Assert.Equal("game", File.ReadAllText(Path.Combine(Destination, "slot1.sav")));
        Assert.False(File.Exists(Path.Combine(Destination, "slot1.sav.nuovo")));
        Assert.True(File.Exists(Path.Combine(Destination, "config.ini.nuovo")));
        Assert.False(FileOps.RecoverInterruptedMatched(Destination, "*.sav", recursive: false));
    }

    [Fact]
    public void ReplaceMatched_FailingCopyLeavesTheGameFilesAsTheyWere()
    {
        TestFolder.Write(Path.Combine(Source, "slot1.sav"), "from snapshot");
        TestFolder.Write(Path.Combine(Destination, "slot1.sav"), "game");
        TestFolder.Write(Path.Combine(Destination, "slot2.sav"), "game only");

        // Una cartella con il nome della copia impedisce di scrivere il .nuovo.
        Directory.CreateDirectory(Path.Combine(Destination, "slot1.sav.nuovo"));

        Assert.ThrowsAny<Exception>(() => FileOps.ReplaceMatched(Source, Destination, "*.sav", recursive: false));

        Assert.Equal("game", File.ReadAllText(Path.Combine(Destination, "slot1.sav")));
        Assert.Equal("game only", File.ReadAllText(Path.Combine(Destination, "slot2.sav")));
    }

    [Fact]
    public void ReplaceFolderFromSnapshotDoesNotCreateExtraSpotsInTheGame()
    {
        TestFolder.Write(Path.Combine(Source, "a.sav"), "snap");
        TestFolder.Write(Path.Combine(Source, FileOps.ExtraSpotsFolder, "2", "b.sav"), "extra");

        FileOps.ReplaceFolder(Source, Destination);

        Assert.Equal("snap", File.ReadAllText(Path.Combine(Destination, "a.sav")));
        Assert.False(Directory.Exists(Path.Combine(Destination, FileOps.ExtraSpotsFolder)));
    }

    [Fact]
    public void ReplaceFolderFromGameKeepsTheSnapshotsExtraSpots()
    {
        TestFolder.Write(Path.Combine(Source, "a.sav"), "game");
        TestFolder.Write(Path.Combine(Destination, "a.sav"), "old");
        TestFolder.Write(Path.Combine(Destination, FileOps.ExtraSpotsFolder, "2", "b.sav"), "kept");

        FileOps.ReplaceFolder(Source, Destination);

        Assert.Equal("game", File.ReadAllText(Path.Combine(Destination, "a.sav")));
        Assert.Equal("kept", File.ReadAllText(Path.Combine(Destination, FileOps.ExtraSpotsFolder, "2", "b.sav")));
    }

    [Fact]
    public void MirrorKeepsTheExtraSpotsOfTheDestination()
    {
        TestFolder.Write(Path.Combine(Source, "a.sav"), "game");
        TestFolder.Write(Path.Combine(Destination, "old.sav"), "old");
        TestFolder.Write(Path.Combine(Destination, FileOps.ExtraSpotsFolder, "2", "b.sav"), "kept");

        FileOps.MirrorFolder(Source, Destination);

        Assert.False(File.Exists(Path.Combine(Destination, "old.sav")));
        Assert.Equal("kept", File.ReadAllText(Path.Combine(Destination, FileOps.ExtraSpotsFolder, "2", "b.sav")));
    }

    [Fact]
    public void CopyFolderVerifiedCarriesTheExtraSpots()
    {
        TestFolder.Write(Path.Combine(Source, "a.sav"), "x");
        TestFolder.Write(Path.Combine(Source, FileOps.ExtraSpotsFolder, "2", "b.sav"), "extra");

        FileOps.CopyFolderVerified(Source, Destination);

        Assert.Equal("extra", File.ReadAllText(Path.Combine(Destination, FileOps.ExtraSpotsFolder, "2", "b.sav")));
    }

    [Fact]
    public void ReplaceFolderWithCarryTakesTheExtraSpotsOfTheSource()
    {
        TestFolder.Write(Path.Combine(Source, "a.sav"), "x");
        TestFolder.Write(Path.Combine(Source, FileOps.ExtraSpotsFolder, "2", "b.sav"), "new");
        TestFolder.Write(Path.Combine(Destination, "a.sav"), "old");
        TestFolder.Write(Path.Combine(Destination, FileOps.ExtraSpotsFolder, "2", "b.sav"), "old");
        TestFolder.Write(Path.Combine(Destination, FileOps.ExtraSpotsFolder, "3", "c.sav"), "gone");

        FileOps.ReplaceFolder(Source, Destination, carryExtraSpots: true);

        Assert.Equal("new", File.ReadAllText(Path.Combine(Destination, FileOps.ExtraSpotsFolder, "2", "b.sav")));
        Assert.False(File.Exists(Path.Combine(Destination, FileOps.ExtraSpotsFolder, "3", "c.sav")));
    }

    [Fact]
    public void EqualityAndEmptinessIgnoreTheExtraSpots()
    {
        TestFolder.Write(Path.Combine(Source, "a.sav"), "same");
        TestFolder.Write(Path.Combine(Destination, "a.sav"), "same");
        TestFolder.Write(Path.Combine(Destination, FileOps.ExtraSpotsFolder, "2", "b.sav"), "x");
        TestFolder.Write(Path.Combine(folder.Root, "only", FileOps.ExtraSpotsFolder, "2", "b.sav"), "x");

        Assert.True(FileOps.FoldersAreEqual(Source, Destination));
        Assert.False(FileOps.HasNoSaveFiles(Destination));
        Assert.True(FileOps.HasNoSaveFiles(Path.Combine(folder.Root, "only")));
    }
}
