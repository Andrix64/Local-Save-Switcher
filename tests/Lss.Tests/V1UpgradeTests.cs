namespace Lss.Tests;

/// <summary>
/// Rete di sicurezza di R9–R11: una cartella dei dati come la scrivevano la 1.0 e le build 2.0 prima
/// della R10 (solo file .ini) si converte in JSON e si apre con tutto quello che c'era. Resta finché
/// c'è la compatibilità con la 1.0 (V1Compat).
/// </summary>
public sealed class V1UpgradeTests : IDisposable
{
    private const string Second = "b2c3d4e5";
    private const string When = "2026-10-25T10:00:00.0000000Z";

    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string Root => folder.Data;
    private string GameData => Path.Combine(Root, "steam", "111", "870780");
    private string Extra => Path.Combine(folder.Root, "extra");
    private string Ignored => Path.Combine(folder.Root, "ignorata");
    private string Loose => Path.Combine(folder.Root, "sparsi");

    private void Old(string relative, string text) => TestFolder.Write(Path.Combine(GameData, relative), text);

    // I file come li scriveva la 1.0: persone, due partite con note e tempo, copie con e senza proprietario,
    // una partita eliminata, posti, cartelle ignorate, una proposta e una scartata.
    private void WriteOldData()
    {
        TestFolder.Write(Path.Combine(Root, "lss.ini"), "UltimoAccount=steam/111\nUltimoGioco=steam/870780\nModalita=semplice\nSchermoIntero=1\nCercaDopoPartita=0\n");
        TestFolder.Write(Path.Combine(Root, "persone.ini"),
            $"Ordine={TestFolder.Owner},{Second}\n{TestFolder.Owner}.Nome=Andrea\n{TestFolder.Owner}.Ruolo=admin\n{TestFolder.Owner}.Pin=c2FsdA==:aGFzaA==\n" +
            $"{Second}.Nome=Clara\n{Second}.Ruolo=utente\n");
        TestFolder.Write(Path.Combine(Root, "cartelle-ignorate.ini"), "Cartella.0={Documents}/Rumore\nAggiunta.0=" + When + "\n");

        TestFolder.Write(folder.SaveFile, "slot");
        TestFolder.Write(folder.CacheFile, "cache");
        Directory.CreateDirectory(Path.Combine(GameData, "Profili", "Profilo_Mario"));
        TestFolder.Write(Path.Combine(GameData, "Profili", "Profilo_Mario", "remote", "slot.sav"), "mario");
        TestFolder.Write(Path.Combine(GameData, "Profili", "Profilo_Luigi", "remote", "slot.sav"), "luigi");

        Old("stato.ini",
            "ProfiloAttivo=Profilo_Mario\nAttivatoIl=-\nSalvatoIl=25/10/2026 10:00:00\nBackupDaConservare=7\nAvviaGioco=0\n" +
            "SalvaAllaChiusura=1\nOpzioniCondivise=1\nFileCondivisi=remote/a.cfg|b.ini\nProprietariOk=1\nCloudConsentito=1\n");
        Old("preferite.ini", $"{TestFolder.Owner}=Profilo_Mario\n{TestFolder.Owner}.Ultima=2026-10-25 21:00:00\n{Second}=Profilo_Luigi\n");
        Old("Info/Profilo_Mario.ini", $"Nota=Run veloce\nTempoDiGioco=3600\nUltimaSessione=2026-10-25 21:00:00\nGiocatore={TestFolder.Owner}\n");
        Old("Info/Profilo_Luigi.ini", $"Nota=\nTempoDiGioco=0\nUltimaSessione=\nGiocatore={Second}\n");

        Old("Backup/Prima_salvataggio_Profilo_Mario_2026-10-25_10-00-00-000/remote/slot.sav", "old");
        Old("Backup/Prima_salvataggio_Profilo_Mario_2026-10-25_10-00-00-000.ini",
            "Partita=Profilo_Mario\nMotivo=salvataggio\nDettaglio=\n");
        // Copia della 1.0, senza file .ini: si descrive dal nome (V1Compat).
        Old("Backup/Prima_salvataggio_2026-10-24_09-00-00-000/remote/slot.sav", "older");
        Old("Backup/Eliminato_Profilo_Gone_2026-10-25_11-00-00-000/remote/slot.sav", "gone");
        Old("Backup/Eliminato_Profilo_Gone_2026-10-25_11-00-00-000.ini",
            $"Partita=Profilo_Gone\nMotivo=eliminazione\nDettaglio=\nNota=Da recuperare\nTempoDiGioco=120\nUltimaSessione=2026-10-24 20:00:00\nGiocatore={Second}\n");

        Old("posti.ini",
            $"Prossimo=4\nPosto.1={folder.Game}|{folder.Game}||1|imparata\nPosto.2={Extra}|{{Documents}}/Extra|*.sav|0|scelta\n" +
            $"Launcher.chiave|*|1=3\nCartelleExtra={Extra}\nIgnorate.0={Ignored}\nAggiunta.0={When}\n");
        Old("proposte.ini",
            $"Proposta.0={Loose}||9|{When}|GameName:6:Hollow\nScartata.0={Loose}|*.sav\n");
    }

    private static void Convert(string root)
    {
        V1Compat.ConvertAppFiles(root);
        V1Compat.ConvertGameFiles(root);
    }

    private SaveManager Open()
    {
        var people = new People(Root);
        people.Load();

        return new SaveManager(folder.Game, GameData) { Players = people };
    }

    [Fact]
    public void EveryIniBecomesJsonAndIsKeptAsPrecedente()
    {
        WriteOldData();

        Convert(Root);

        Assert.Empty(Directory.GetFiles(Root, "*.ini", SearchOption.AllDirectories));

        foreach (string json in new[] { "lss.json", "persone.json", "cartelle-ignorate.json", "steam/111/870780/stato.json",
                     "steam/111/870780/preferite.json", "steam/111/870780/posti.json", "steam/111/870780/proposte.json",
                     "steam/111/870780/Info/Profilo_Mario.json", "steam/111/870780/Info/Profilo_Luigi.json" })
        {
            Assert.True(File.Exists(Path.Combine(Root, json)), json);
        }

        Assert.True(File.Exists(Path.Combine(GameData, "stato.ini.precedente")));
        Assert.True(File.Exists(Path.Combine(GameData, "Info", "Profilo_Mario.ini.precedente")));
    }

    [Fact]
    public void EverythingIsStillThereAfterTheConversion()
    {
        WriteOldData();
        Convert(Root);

        SaveManager manager = Open();

        // Impostazioni dell'app e persone.
        var settings = new AppSettings(AppSettings.FileIn(Root));
        settings.Load();
        Assert.True(settings.FullScreen);
        Assert.False(settings.LookAfterPlaying);
        Assert.Equal(["Andrea", "Clara"], manager.Players!.All.Select(person => person.Name));
        Assert.Contains("c2FsdA==:aGFzaA==", File.ReadAllText(Path.Combine(Root, "persone.json")));

        // Stato del gioco.
        Assert.Equal("Profilo_Mario", manager.State.ActiveProfile);
        Assert.Equal("", manager.State.ActiveSince);
        Assert.Equal("25/10/2026 10:00:00", manager.State.LastSaved);
        Assert.Equal(7, manager.State.BackupsToKeep);
        Assert.False(manager.State.LaunchGame);
        Assert.True(manager.State.SharePreferences);
        Assert.Equal(["remote/a.cfg", "b.ini"], manager.State.SharedFiles);
        Assert.True(manager.State.OwnersAssigned);
        Assert.True(manager.State.CloudAllowedInSimple);

        // Partite, preferite e ultima partita.
        PlaythroughInfo mario = manager.GetInfo("Profilo_Mario");
        Assert.Equal("Run veloce", mario.Note);
        Assert.Equal(TimeSpan.FromHours(1), mario.PlayTime);
        Assert.Equal(new DateTime(2026, 10, 25, 21, 0, 0), mario.LastSession);
        Assert.Equal(TestFolder.Owner, mario.Owner);
        Assert.Equal("Profilo_Mario", manager.FavouriteOf(TestFolder.Owner));
        Assert.Equal("Profilo_Luigi", manager.FavouriteOf(Second));
        Assert.Equal(new DateTime(2026, 10, 25, 21, 0, 0), manager.LastPlayedOf(TestFolder.Owner));

        // Copie: con proprietario, della 1.0 senza file, e la partita eliminata con nota e tempo.
        Assert.Single(manager.GetBackupsOf("Profilo_Mario"));
        Assert.Equal(BackupKind.Save, manager.GetBackups().First(b => b.Name == "Prima_salvataggio").Kind);
        Snapshot gone = Assert.Single(manager.GetDeletedProfiles());
        Assert.Equal("Profilo_Gone", gone.Owner);
        Assert.Equal("Da recuperare", manager.DeletedInfoOf(gone).Note);
        Assert.Equal(TimeSpan.FromMinutes(2), manager.DeletedInfoOf(gone).PlayTime);
        Assert.Equal(Second, manager.DeletedOwnerOf(gone));

        // Posti, cartelle extra, id dei launcher e cartelle ignorate (stesso file: posti.json).
        var spots = new GameSpots(GameData);
        spots.Load();
        Assert.Equal([1, 2], spots.Spots.Select(spot => spot.Id));
        Assert.Equal(SaveSource.Learned, spots.Spots[0].Source);
        Assert.Equal("*.sav", spots.Spots[1].Pattern);
        Assert.False(spots.Spots[1].Recursive);
        Assert.Equal([Extra], spots.ExtraRoots);
        Assert.Equal(3, spots.LauncherId("chiave|*|1"));
        Assert.Equal(Ignored, Assert.Single(new IgnoredFolders(Root).ForGame(GameData)).Folder);

        // Proposte.
        SpotSuggestion suggestion = Assert.Single(SessionScan.Pending(GameData));
        Assert.Equal(Loose, suggestion.Folder);
        Assert.Equal(9, suggestion.Score);
        Assert.Equal(ScoreReasonKind.GameName, Assert.Single(suggestion.Reasons).Kind);
        Assert.Equal((Loose, "*.sav"), Assert.Single(SessionScan.Dismissed(GameData)));
    }

    [Fact]
    public void ASecondRunChangesNothing()
    {
        WriteOldData();
        Convert(Root);

        string Snapshot() => string.Join("\n", Directory.GetFiles(Root, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(file => file + "=" + File.ReadAllText(file)));

        string before = Snapshot();
        Convert(Root);

        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public void SavingASpotKeepsTheIgnoredFoldersAndTheOtherWayRound()
    {
        WriteOldData();
        Convert(Root);

        var spots = new GameSpots(GameData);
        spots.Load();
        spots.Add(Path.Combine(folder.Root, "nuovo"), "", "nuovo", SaveSource.Chosen);
        Assert.True(spots.Save());

        Assert.Single(new IgnoredFolders(Root).ForGame(GameData));

        new IgnoredFolders(Root).AddForGame(GameData, Path.Combine(folder.Root, "altra"));

        var again = new GameSpots(GameData);
        again.Load();
        Assert.Equal(3, again.Spots.Count);
        Assert.Equal(2, new IgnoredFolders(Root).ForGame(GameData).Count);
    }

    [Fact]
    public void RenamingAfterTheConversionKeepsTheInfoAndTheCopies()
    {
        WriteOldData();
        Convert(Root);
        SaveManager manager = Open();

        Assert.True(manager.RenameProfile("Profilo_Mario", "Marietto").Success);

        Assert.Equal("Run veloce", manager.GetInfo("Profilo_Marietto").Note);
        Assert.Single(manager.GetBackupsOf("Profilo_Marietto"));
        Assert.Equal("Profilo_Marietto", manager.FavouriteOf(TestFolder.Owner));
    }

    [Fact]
    public void ABrokenIniDoesNotStopTheOthers()
    {
        WriteOldData();
        Old("posti.ini", "");

        Convert(Root);

        Assert.True(File.Exists(Path.Combine(GameData, "stato.json")));
        Assert.True(File.Exists(Path.Combine(GameData, "proposte.json")));
    }
}
