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

    private string DataRoot => Path.Combine(folder.Root, "data");
    private string SpotsData => Path.Combine(DataRoot, "steam", "111", "123450");
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

        V1Compat.ConvertGameFiles(DataRoot);

        var spots = new GameSpots(SpotsData);
        spots.Load();

        SaveSpot primary = Assert.Single(spots.Spots);
        Assert.Equal(1, primary.Id);
        Assert.Equal(saves, primary.Folder);
        Assert.Equal(SaveSource.Learned, primary.Source);
        Assert.Equal([extra], spots.ExtraRoots);
        Assert.False(File.Exists(Cartella));
        Assert.True(File.Exists(Cartella + ".precedente"));

        Assert.Equal(ignored, Assert.Single(new IgnoredFolders(folder.Root).ForGame(SpotsData)).Folder);

        // Una seconda volta non cambia niente.
        V1Compat.ConvertGameFiles(DataRoot);
        var again = new GameSpots(SpotsData);
        again.Load();
        Assert.Single(again.Spots);
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
        using var folder = new TestFolder();
        TestFolder.Write(Path.Combine(folder.Root, "partita.ini"), "Formato=2\nAppId=123450\n");
        Assert.Equal("steam", V1Compat.ReadExportedInfo(folder.Root).Provider);

        TestFolder.Write(Path.Combine(folder.Root, "partita.ini"), "Provider=epic\n");
        Assert.Equal("epic", V1Compat.ReadExportedInfo(folder.Root).Provider);
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

    // Da .ini a .json (R9)

    [Fact]
    public void EveryAppFileIsConvertedAndTheIniKept()
    {
        TestFolder.Write(Path.Combine(Root, "lss.ini"),
            "UltimoAccount=steam/111\nUltimoGioco=steam/870780\nCercaDopoPartita=0\nModalita=semplice\nSchermoIntero=1\n");
        TestFolder.Write(Path.Combine(Root, "persone.ini"),
            "Ordine=bbbbbbbb,aaaaaaaa\n" +
            "aaaaaaaa.Nome=Andrea\naaaaaaaa.Colore=#C2410C\naaaaaaaa.Ruolo=admin\naaaaaaaa.Pin=c2FsdA==:aGFzaA==\n" +
            "bbbbbbbb.Nome=Clara\nbbbbbbbb.Colore=#1D6FD0\nbbbbbbbb.Ruolo=utente\n");
        TestFolder.Write(Path.Combine(Root, "cartelle-ignorate.ini"),
            "Cartella.0={WinAppDataLocal}/Discord\nAggiunta.0=2026-10-25T10:00:00.0000000\nCartella.1={WinUserProfile}/Altro\n");
        TestFolder.Write(Path.Combine(ManualGames.AccountFolder(Root), "abcd1234", "gioco.ini"),
            "Nome=Il mio gioco\nEseguibile=C:\\Giochi\\gioco.exe\nArgomenti=-w\nPrefisso=\n");

        V1Compat.ConvertAppFiles(Root);

        var settings = new AppSettings(Path.Combine(Root, "lss.json"));
        settings.Load();
        Assert.False(settings.FirstStart);
        Assert.Equal("steam/111", settings.LastAccount);
        Assert.Equal("steam/870780", settings.LastGame);
        Assert.False(settings.LookAfterPlaying);
        Assert.Equal(AppSettings.SimpleMode, settings.Mode);
        Assert.True(settings.FullScreen);

        var people = new People(Root);
        people.Load();
        Assert.Equal(new[] { "Clara", "Andrea" }, people.All.Select(person => person.Name));
        Assert.True(people.Find("aaaaaaaa")!.IsAdmin);

        var ignored = new IgnoredFolders(Root);
        ignored.Load();
        Assert.Equal(["{WinAppDataLocal}/Discord", "{WinUserProfile}/Altro"], ignored.Global.Select(entry => entry.PortablePath));
        Assert.Equal(new DateTime(2026, 10, 25, 10, 0, 0), ignored.Global[0].Added);

        (string id, ManualGame game) = Assert.Single(ManualGames.ReadAll(Root));
        Assert.Equal("abcd1234", id);
        Assert.Equal("Il mio gioco", game.Name);
        Assert.Equal(@"C:\Giochi\gioco.exe", game.Exe);
        Assert.Equal("-w", game.Arguments);

        foreach (string ini in new[] { "lss.ini", "persone.ini", "cartelle-ignorate.ini" })
        {
            Assert.False(File.Exists(Path.Combine(Root, ini)));
            Assert.True(File.Exists(Path.Combine(Root, ini + ".precedente")));
        }

        Assert.True(File.Exists(Path.Combine(ManualGames.AccountFolder(Root), "abcd1234", "gioco.ini.precedente")));
    }

    [Fact]
    public void ThePinOfAUserSurvivesTheConversion()
    {
        TestFolder.Write(Path.Combine(Root, "persone.ini"),
            "Ordine=aaaaaaaa,bbbbbbbb\naaaaaaaa.Nome=Andrea\naaaaaaaa.Ruolo=admin\n" +
            "bbbbbbbb.Nome=Clara\nbbbbbbbb.Ruolo=utente\nbbbbbbbb.Pin=c2FsdHNhbHRzYWx0c2FsdA==:aGFzaGhhc2hoYXNoaGFzaGhhc2hoYXNoaGFzaGhhc2g=\n");

        V1Compat.ConvertAppFiles(Root);

        var people = new People(Root);
        people.Load();
        Assert.True(people.Find("bbbbbbbb")!.HasPin);
    }

    [Fact]
    public void ConvertingTwiceChangesNothing()
    {
        TestFolder.Write(Path.Combine(Root, "lss.ini"), "UltimoAccount=steam/111\n");
        V1Compat.ConvertAppFiles(Root);
        string json = File.ReadAllText(Path.Combine(Root, "lss.json"));
        string[] files = Directory.GetFiles(Root);

        V1Compat.ConvertAppFiles(Root);

        Assert.Equal(json, File.ReadAllText(Path.Combine(Root, "lss.json")));
        Assert.Equal(files, Directory.GetFiles(Root));
    }

    [Fact]
    public void WhenBothExistTheJsonWinsAndTheIniIsSetAside()
    {
        TestFolder.Write(Path.Combine(Root, "lss.ini"), "UltimoAccount=steam/vecchio\n");
        TestFolder.Write(Path.Combine(Root, "lss.json"), "{\"UltimoAccount\":\"steam/nuovo\"}");

        V1Compat.ConvertAppFiles(Root);

        var settings = new AppSettings(Path.Combine(Root, "lss.json"));
        settings.Load();
        Assert.Equal("steam/nuovo", settings.LastAccount);
        Assert.False(File.Exists(Path.Combine(Root, "lss.ini")));
        Assert.Equal("UltimoAccount=steam/vecchio\n", File.ReadAllText(Path.Combine(Root, "lss.ini.precedente")));
    }

    [Fact]
    public void AnInterruptedConversionIsFinishedAtTheNextStart()
    {
        // Il .json è stato scritto ma il .ini non è stato ancora messo da parte.
        TestFolder.Write(Path.Combine(Root, "lss.ini"), "UltimoAccount=steam/111\n");
        TestFolder.Write(Path.Combine(Root, "lss.json"), "{\"UltimoAccount\":\"steam/111\"}");

        V1Compat.ConvertAppFiles(Root);

        Assert.False(File.Exists(Path.Combine(Root, "lss.ini")));
        Assert.True(File.Exists(Path.Combine(Root, "lss.ini.precedente")));
    }

    [Fact]
    public void AnEmptyIniIsNotConvertedAndStaysWhereItIs()
    {
        TestFolder.Write(Path.Combine(Root, "persone.ini"), "questo non è un file di persone");

        V1Compat.ConvertAppFiles(Root);

        Assert.True(File.Exists(Path.Combine(Root, "persone.ini")));
        Assert.False(File.Exists(Path.Combine(Root, "persone.json")));
    }

    [Fact]
    public void AnOldIniPrecedenteIsNeverOverwritten()
    {
        TestFolder.Write(Path.Combine(Root, "lss.ini.precedente"), "prima");
        TestFolder.Write(Path.Combine(Root, "lss.ini"), "UltimoAccount=steam/111\n");

        V1Compat.ConvertAppFiles(Root);

        Assert.Equal("prima", File.ReadAllText(Path.Combine(Root, "lss.ini.precedente")));
        Assert.Equal("UltimoAccount=steam/111\n", File.ReadAllText(Path.Combine(Root, "lss.ini.precedente.2")));
    }

    [Fact]
    public void WithoutAnyFileTheFirstStartIsStillAFirstStart()
    {
        V1Compat.ConvertAppFiles(Root);

        var settings = new AppSettings(Path.Combine(Root, "lss.json"));
        settings.Load();
        Assert.True(settings.FirstStart);
    }

    [Fact]
    public void BrokenJsonGivesDefaultsAndRotto()
    {
        TestFolder.Write(Path.Combine(Root, "lss.json"), "{ rotto");

        var settings = new AppSettings(Path.Combine(Root, "lss.json"));
        settings.Load();

        Assert.Equal("", settings.LastAccount);
        Assert.True(settings.LookAfterPlaying);
        Assert.True(File.Exists(Path.Combine(Root, "lss.json.rotto")));
    }
}
