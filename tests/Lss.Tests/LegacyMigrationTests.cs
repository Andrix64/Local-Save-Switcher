namespace Lss.Tests;

public sealed class LegacyMigrationTests : IDisposable
{
    private readonly TestFolder folder = new();

    private string Old => Path.Combine(folder.Root, "Documenti", "ControlSaves");
    private string New => Path.Combine(folder.Root, "AppData", "Local", "ControlSaveManager");
    private string Lss => Path.Combine(folder.Root, "AppData", "Local", "LSS");

    public void Dispose() => folder.Dispose();

    private void WriteOldData()
    {
        TestFolder.Write(Path.Combine(Old, "123", "Profili", "Profilo_Mario", "remote", "slot.sav"), "mario");
        TestFolder.Write(Path.Combine(Old, "123", "stato.ini"), "ProfiloAttivo=Profilo_Mario");
        TestFolder.Write(Path.Combine(Old, "log.txt"), "registro");
    }

    [Fact]
    public void Move_MovesEverythingAndRemovesOldFolder()
    {
        WriteOldData();

        (LegacyMigration.MoveResult result, string detail) = LegacyMigration.Move(Old, New);

        Assert.Equal(LegacyMigration.MoveResult.Moved, result);
        Assert.Equal("", detail);
        Assert.False(Directory.Exists(Old));
        Assert.Equal("mario", File.ReadAllText(Path.Combine(New, "123", "Profili", "Profilo_Mario", "remote", "slot.sav")));
        Assert.Equal("registro", File.ReadAllText(Path.Combine(New, "log.txt")));
    }

    [Fact]
    public void Move_UsesEmptyDestinationFolder()
    {
        WriteOldData();
        Directory.CreateDirectory(New);

        Assert.Equal(LegacyMigration.MoveResult.Moved, LegacyMigration.Move(Old, New).Result);
        Assert.True(File.Exists(Path.Combine(New, "123", "stato.ini")));
    }

    [Fact]
    public void Move_TouchesNothingWhenBothHaveData()
    {
        WriteOldData();
        TestFolder.Write(Path.Combine(New, "log.txt"), "nuovo");

        Assert.Equal(LegacyMigration.MoveResult.BothExist, LegacyMigration.Move(Old, New).Result);
        Assert.Equal("registro", File.ReadAllText(Path.Combine(Old, "log.txt")));
        Assert.Equal("nuovo", File.ReadAllText(Path.Combine(New, "log.txt")));
    }

    [Fact]
    public void Move_DoesNothingWithoutOldFolder()
    {
        Assert.Equal(LegacyMigration.MoveResult.NothingToMove, LegacyMigration.Move(Old, New).Result);
        Assert.False(Directory.Exists(New));
    }

    [Fact]
    public void Move_LeavesOldFolderIntactWhenItFails()
    {
        if (!OperatingSystem.IsWindows())
        {
            WriteOldData();

            // Su Linux un file aperto non blocca lo spostamento: al posto della
            // cartella che deve contenere quella nuova c'è un file.
            string blocker = Path.Combine(folder.Root, "bloccata");
            File.WriteAllText(blocker, "");
            string target = Path.Combine(blocker, "LSS");

            (LegacyMigration.MoveResult result, string detail) = LegacyMigration.Move(Old, target);

            Assert.Equal(LegacyMigration.MoveResult.Failed, result);
            Assert.NotEqual("", detail);
            Assert.Equal("mario", File.ReadAllText(Path.Combine(Old, "123", "Profili", "Profilo_Mario", "remote", "slot.sav")));
            Assert.False(Directory.Exists(target));
            return;
        }

        WriteOldData();

        // Un file aperto impedisce di spostare la cartella.
        using (new FileStream(Path.Combine(Old, "log.txt"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            (LegacyMigration.MoveResult result, string detail) = LegacyMigration.Move(Old, New);

            Assert.Equal(LegacyMigration.MoveResult.Failed, result);
            Assert.NotEqual("", detail);
        }

        Assert.Equal("mario", File.ReadAllText(Path.Combine(Old, "123", "Profili", "Profilo_Mario", "remote", "slot.sav")));
        Assert.False(Directory.Exists(New));
    }

    [Fact]
    public void Migrate_MovesControlSaveManagerAndRestructures()
    {
        TestFolder.Write(Path.Combine(New, "account.txt"), "111");
        TestFolder.Write(Path.Combine(New, "111", "stato.ini"), "ProfiloAttivo=Profilo_Mario");
        TestFolder.Write(Path.Combine(New, "111", "Profili", "Profilo_Mario", "remote", "a.sav"), "mario");
        TestFolder.Write(Path.Combine(New, "111", "Backup", "x", "a"), "backup");
        TestFolder.Write(Path.Combine(New, "log.txt"), "registro");

        (LegacyMigration.MoveResult result, string _, string root, string source) = LegacyMigration.MigrateToLss(Old, New, Lss);

        Assert.Equal(LegacyMigration.MoveResult.Moved, result);
        Assert.Equal(Lss, root);
        Assert.Equal(New, source);
        Assert.Equal("mario", File.ReadAllText(Path.Combine(Lss, "111", "870780", "Profili", "Profilo_Mario", "remote", "a.sav")));
        Assert.Equal("backup", File.ReadAllText(Path.Combine(Lss, "111", "870780", "Backup", "x", "a")));
        Assert.True(File.Exists(Path.Combine(Lss, "111", "870780", "stato.ini")));
        Assert.Equal("registro", File.ReadAllText(Path.Combine(Lss, "log.txt")));
        var settings = new AppSettings(Path.Combine(Lss, "lss.json"));
        settings.Load();
        Assert.Equal("111", settings.LastAccount);
        Assert.False(File.Exists(Path.Combine(Lss, "account.txt")));
        Assert.False(Directory.Exists(New));
    }

    [Fact]
    public void Migrate_FromDocumentsThroughToLss()
    {
        WriteOldData();

        (LegacyMigration.MoveResult result, string _, string root, string source) = LegacyMigration.MigrateToLss(Old, New, Lss);

        Assert.Equal(LegacyMigration.MoveResult.Moved, result);
        Assert.Equal(Lss, root);
        Assert.Equal(New, source);
        Assert.False(Directory.Exists(Old));
        Assert.False(Directory.Exists(New));
        Assert.Equal("mario", File.ReadAllText(Path.Combine(Lss, "123", "870780", "Profili", "Profilo_Mario", "remote", "slot.sav")));
        Assert.Equal("registro", File.ReadAllText(Path.Combine(Lss, "log.txt")));
    }

    [Fact]
    public void Migrate_BothExist_TouchesNothing()
    {
        TestFolder.Write(Path.Combine(New, "111", "stato.ini"), "vecchio");
        TestFolder.Write(Path.Combine(New, "log.txt"), "vecchio");
        TestFolder.Write(Path.Combine(Lss, "log.txt"), "nuovo");

        (LegacyMigration.MoveResult result, string _, string root, _) = LegacyMigration.MigrateToLss(Old, New, Lss);

        Assert.Equal(LegacyMigration.MoveResult.BothExist, result);
        Assert.Equal(Lss, root);
        Assert.Equal("vecchio", File.ReadAllText(Path.Combine(New, "log.txt")));
        Assert.Equal("vecchio", File.ReadAllText(Path.Combine(New, "111", "stato.ini")));
        Assert.Equal("nuovo", File.ReadAllText(Path.Combine(Lss, "log.txt")));
        Assert.False(Directory.Exists(Path.Combine(Lss, "111")));
    }

    [Fact]
    public void Migrate_StaysInOldRootWhenFirstStepFails()
    {
        if (!OperatingSystem.IsWindows())
        {
            WriteOldData();

            // Su Linux un file aperto non blocca lo spostamento: al posto della
            // cartella che deve contenere la nuova c'è un file.
            TestFolder.Write(Path.GetDirectoryName(New)!, "");

            (LegacyMigration.MoveResult result, string _, string root, string source) = LegacyMigration.MigrateToLss(Old, New, Lss);

            Assert.Equal(LegacyMigration.MoveResult.Failed, result);
            Assert.Equal(Old, root);
            Assert.Equal(Old, source);
            Assert.True(File.Exists(Path.Combine(Old, "123", "870780", "stato.ini")));
            Assert.Equal("mario", File.ReadAllText(Path.Combine(Old, "123", "870780", "Profili", "Profilo_Mario", "remote", "slot.sav")));
            Assert.False(Directory.Exists(Lss));
            return;
        }

        WriteOldData();

        using (new FileStream(Path.Combine(Old, "log.txt"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            (LegacyMigration.MoveResult result, string _, string root, string source) = LegacyMigration.MigrateToLss(Old, New, Lss);

            Assert.Equal(LegacyMigration.MoveResult.Failed, result);
            Assert.Equal(Old, root);
            Assert.Equal(Old, source);
        }

        // L'app resta nella vecchia cartella, già con la struttura per gioco.
        Assert.True(File.Exists(Path.Combine(Old, "123", "870780", "stato.ini")));
        Assert.Equal("mario", File.ReadAllText(Path.Combine(Old, "123", "870780", "Profili", "Profilo_Mario", "remote", "slot.sav")));
        Assert.False(Directory.Exists(New));
        Assert.False(Directory.Exists(Lss));
    }

    [Fact]
    public void Migrate_FromDocumentsButLssHasData_IsBothExist()
    {
        WriteOldData();
        TestFolder.Write(Path.Combine(Lss, "log.txt"), "nuovo");

        (LegacyMigration.MoveResult result, string _, string root, _) = LegacyMigration.MigrateToLss(Old, New, Lss);

        Assert.Equal(LegacyMigration.MoveResult.BothExist, result);
        Assert.Equal(Lss, root);
        Assert.True(File.Exists(Path.Combine(New, "123", "stato.ini")));
        Assert.Equal("nuovo", File.ReadAllText(Path.Combine(Lss, "log.txt")));
    }

    [Fact]
    public void Restructure_IsRepeatable()
    {
        TestFolder.Write(Path.Combine(Lss, "111", "Profili", "Profilo_A", "a"), "a");
        TestFolder.Write(Path.Combine(Lss, "111", "870780", "Profili", "Profilo_B", "b"), "b");

        LegacyMigration.RestructureAccounts(Lss);
        LegacyMigration.RestructureAccounts(Lss);

        // Profili già presente nella cartella del gioco: quello dell'account resta dov'è.
        Assert.Equal("a", File.ReadAllText(Path.Combine(Lss, "111", "Profili", "Profilo_A", "a")));
        Assert.Equal("b", File.ReadAllText(Path.Combine(Lss, "111", "870780", "Profili", "Profilo_B", "b")));
    }

    [Fact]
    public void Restructure_SkipsNonAccountFolders()
    {
        TestFolder.Write(Path.Combine(Lss, "logs", "Backup", "q"), "q");
        TestFolder.Write(Path.Combine(Lss, "abc", "Profili", "Profilo_X", "a"), "a");
        TestFolder.Write(Path.Combine(Lss, "abc", "stato.ini"), "x");

        LegacyMigration.RestructureAccounts(Lss);

        Assert.Equal("q", File.ReadAllText(Path.Combine(Lss, "logs", "Backup", "q")));
        Assert.Equal("a", File.ReadAllText(Path.Combine(Lss, "abc", "Profili", "Profilo_X", "a")));
        Assert.True(File.Exists(Path.Combine(Lss, "abc", "stato.ini")));
        Assert.False(Directory.Exists(Path.Combine(Lss, "abc", "870780")));
    }
}
