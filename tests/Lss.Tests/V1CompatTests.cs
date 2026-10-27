namespace Lss.Tests;

/// <summary>
/// V1Compat: la compatibilità con i dati della 1.0 (cartelle delle beta, cartella.ini,
/// valori senza launcher, copie senza proprietario, zip di formato 1 e 2).
/// </summary>
public sealed class V1CompatTests : IDisposable
{
    private readonly TestFolder folder = new();

    private string Root => folder.Data;

    public void Dispose() => folder.Dispose();

    [Fact]
    public void NumericFoldersMoveUnderSteam()
    {
        TestFolder.Write(Path.Combine(Root, "111", "870780", "Info", "a.txt"), "x");

        V1Compat.MoveBetaLayout(Root);

        Assert.Equal("x", File.ReadAllText(Path.Combine(Root, "steam", "111", "870780", "Info", "a.txt")));
        Assert.False(Directory.Exists(Path.Combine(Root, "111")));
    }

    [Fact]
    public void SecondRunChangesNothing()
    {
        TestFolder.Write(Path.Combine(Root, "111", "870780", "a.txt"), "x");

        V1Compat.MoveBetaLayout(Root);
        V1Compat.MoveBetaLayout(Root);

        Assert.Equal("x", File.ReadAllText(Path.Combine(Root, "steam", "111", "870780", "a.txt")));
        Assert.Equal(["steam"], Directory.GetDirectories(Root).Select(Path.GetFileName));
    }

    [Fact]
    public void ExistingTargetIsNeverOverwritten()
    {
        TestFolder.Write(Path.Combine(Root, "111", "870780", "a.txt"), "vecchio");
        TestFolder.Write(Path.Combine(Root, "steam", "111", "870780", "a.txt"), "nuovo");

        V1Compat.MoveBetaLayout(Root);

        Assert.Equal("nuovo", File.ReadAllText(Path.Combine(Root, "steam", "111", "870780", "a.txt")));
        Assert.Equal("vecchio", File.ReadAllText(Path.Combine(Root, "111", "870780", "a.txt")));
    }

    [Fact]
    public void InterruptedMoveIsFinished()
    {
        // Spostamento fermato dopo la copia: la destinazione è identica e l'originale c'è ancora.
        TestFolder.Write(Path.Combine(Root, "111", "870780", "a.txt"), "x");
        TestFolder.Write(Path.Combine(Root, "steam", "111", "870780", "a.txt"), "x");

        V1Compat.MoveBetaLayout(Root);

        Assert.False(Directory.Exists(Path.Combine(Root, "111")));
        Assert.Equal("x", File.ReadAllText(Path.Combine(Root, "steam", "111", "870780", "a.txt")));
    }

    [Fact]
    public void FilesAndOtherFoldersStayWhereTheyAre()
    {
        TestFolder.Write(Path.Combine(Root, "lss.ini"), "UltimoAccount=111");
        TestFolder.Write(Path.Combine(Root, "log.txt"), "log");
        TestFolder.Write(Path.Combine(Root, "Altre", "x.txt"), "x");

        V1Compat.MoveBetaLayout(Root);

        Assert.Equal("UltimoAccount=111", File.ReadAllText(Path.Combine(Root, "lss.ini")));
        Assert.Equal("log", File.ReadAllText(Path.Combine(Root, "log.txt")));
        Assert.Equal("x", File.ReadAllText(Path.Combine(Root, "Altre", "x.txt")));
    }

    [Fact]
    public void MissingDataFolderDoesNothing()
    {
        Assert.Null(Record.Exception(() => V1Compat.MoveBetaLayout(Path.Combine(Root, "non-esiste"))));
    }

    // Posti: cartella.ini della 1.0

    private string SpotsData => Path.Combine(folder.Root, "game-data");
    private string Posti => Path.Combine(SpotsData, "posti.ini");
    private string Cartella => Path.Combine(SpotsData, "cartella.ini");

    [Fact]
    public void ChosenFolderFromCartellaIniBecomesSpotOneAndTheFileGoes()
    {
        Directory.CreateDirectory(SpotsData);
        string saves = Path.Combine(folder.Root, "saves");
        string ignored = Path.Combine(folder.Root, "ignored");
        string extra = Path.Combine(folder.Root, "extra");
        string when = new DateTime(2026, 10, 25, 10, 0, 0, DateTimeKind.Utc).ToString("O");
        File.WriteAllLines(Cartella,
        [
            "CartellaSalvataggi=" + saves,
            "Origine=imparata",
            "CartelleExtra=" + extra,
            "Ignorate.0=" + ignored,
            "Aggiunta.0=" + when,
        ]);

        var spots = new GameSpots(SpotsData);
        spots.Load();

        SaveSpot primary = Assert.Single(spots.Spots);
        Assert.Equal(1, primary.Id);
        Assert.Equal(saves, primary.Folder);
        Assert.Equal(SaveSource.Learned, primary.Source);
        Assert.Equal([extra], spots.ExtraRoots);
        Assert.False(File.Exists(Cartella));

        string text = File.ReadAllText(Posti);
        Assert.Contains("Ignorate.0=" + ignored, text);
        Assert.Contains("Aggiunta.0=" + when, text);

        Assert.Equal(ignored, Assert.Single(new IgnoredFolders(folder.Root).ForGame(SpotsData)).Folder);
    }

    // Impostazioni e zip

    [Fact]
    public void OldValuesWithoutProviderAreSteam()
    {
        Assert.True(AppSettings.Refers("111", "steam", "111"));
        Assert.True(AppSettings.Refers("steam/111", "steam", "111"));
        Assert.False(AppSettings.Refers("epic/111", "steam", "111"));
        Assert.False(AppSettings.Refers("", "steam", "111"));
        Assert.False(AppSettings.Refers("111", "epic", "111"));
    }

    [Fact]
    public void ZipWithoutProviderIsSteam()
    {
        Assert.Equal("steam", V1Compat.ZipProvider(new Dictionary<string, string> { ["Formato"] = "2" }));
        Assert.Equal("epic", V1Compat.ZipProvider(new Dictionary<string, string> { ["Provider"] = "epic" }));
    }

    // Copie

    [Theory]
    [InlineData("Prima_di_Profilo_Mario", BackupKind.Activate)]
    [InlineData("Prima_salvataggio_Profilo_Mario", BackupKind.Save)]
    [InlineData("Eliminato_Profilo_Mario", BackupKind.Delete)]
    [InlineData("Prima_salvataggio", BackupKind.Save)]
    [InlineData("Prima_creazione", BackupKind.Create)]
    [InlineData("Prima_ripristino", BackupKind.Restore)]
    public void BackupWithoutInfoFileIsDescribedByItsName(string name, string kind)
    {
        var backup = new Snapshot(Path.Combine(Root, name + "_x"), DateTime.Now, name);

        Assert.Equal(kind, V1Compat.BackupFromName(backup).Kind);
    }

    [Fact]
    public void OnlyTheOldSavingReasonHasAnOldText()
    {
        Assert.Equal(Strings.ReasonBeforeSavingOld, V1Compat.OldBackupReasonText("Prima_salvataggio"));
        Assert.Null(V1Compat.OldBackupReasonText("Prima_creazione"));
    }

    [Fact]
    public void BackupsWithoutOwnerLeaveOutDeletedPlaythroughs()
    {
        SaveManager manager = folder.CreateManager();
        TestFolder.Write(folder.SaveFile, "partita");
        Directory.CreateDirectory(Path.Combine(manager.BackupPath, "Prima_salvataggio_2026-01-01_10-00-00-000"));
        Directory.CreateDirectory(Path.Combine(manager.BackupPath, "Eliminato_Profilo_Mario_2026-01-02_10-00-00-000"));

        Snapshot orphan = Assert.Single(V1Compat.BackupsWithoutOwner(manager));
        Assert.Equal("Prima_salvataggio", orphan.Name);
    }
}
