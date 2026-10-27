namespace Lss;

/// <summary>
/// Dati della 1.0: tutto ciò che serve solo a leggere o tollerare dati scritti da
/// LSS 1.0.x o da build precedenti alla fase R sta qui. Chiamato da pochi punti,
/// ognuno con il commento "V1Compat: perché". Per togliere la compatibilità
/// basta eliminare questo file, i suoi test e le righe marcate (vedi
/// "Compatibility with LSS 1.0" in docs/DEVELOPMENT.md).
/// Non ci va: il recupero dopo un arresto (SaveSet), le regole di sicurezza
/// (SaveFolderSafety) e Control Save Manager (LegacyMigration).
/// </summary>
internal static class V1Compat
{
    // CARTELLE DEI DATI

    /// <summary>
    /// Dati della 1.0 e delle beta: le cartelle degli account dal vecchio formato,
    /// dati\IDAccount\AppID, al nuovo, dati\steam\IDAccount\AppID (una cartella per
    /// launcher). Gira a ogni avvio, prima di leggere i dati, e non tocca ciò che è
    /// già a posto. Finora erano tutte di Steam.
    /// </summary>
    public static void MoveBetaLayout(string root)
    {
        if (!Directory.Exists(root))
            return;

        foreach (string dir in Directory.GetDirectories(root)
                     .Where(dir => SaveManager.IsNumeric(Path.GetFileName(dir))))
        {
            try
            {
                MoveAccount(dir, Path.Combine(root, "steam", Path.GetFileName(dir)));
            }
            catch (Exception ex)
            {
                Log.Write("Spostamento di " + dir, ex);
            }
        }
    }

    /// <summary>
    /// Sposta la cartella di un account. Se a destinazione c'è già una copia
    /// identica (uno spostamento interrotto dopo la copia) elimina l'originale;
    /// se la destinazione ha dati diversi non tocca niente.
    /// </summary>
    internal static void MoveAccount(string source, string destination)
    {
        if (Directory.Exists(destination))
        {
            if (FileOps.FoldersAreEqual(source, destination))
            {
                Directory.Delete(source, true);
                Log.Write("Completato uno spostamento interrotto: " + source);
            }
            else
            {
                Log.Write("Non spostato, esiste già: " + destination);
            }

            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        try
        {
            // Stesso disco: un solo spostamento.
            Directory.Move(source, destination);
        }
        catch (IOException)
        {
            // Disco diverso: si copia e si verifica, e solo dopo si elimina l'originale.
            string temp = destination + ".spostamento";

            if (Directory.Exists(temp))
                Directory.Delete(temp, true);

            FileOps.CopyAllVerified(source, temp);
            Directory.Move(temp, destination);
            Directory.Delete(source, true);
        }

        Log.Write("Dati dell'account spostati in " + destination);
    }

    // IMPOSTAZIONI

    /// <summary>
    /// Dati di prima della K5: l'ultimo account o gioco salvato senza il launcher
    /// (solo l'id) è di Steam.
    /// </summary>
    public static bool IsSteamIdWithoutProvider(string stored, string provider, string id) =>
        !stored.Contains('/') && provider == "steam" && stored == id;

    // PARTITE E COPIE

    /// <summary>
    /// Dati della 1.0: le partite fatte prima delle persone non hanno una preferita.
    /// A ogni persona senza preferita dà la partita attiva (o quella salvata per
    /// ultima). Si fa una volta per gioco (ProprietariOk in stato.ini); se fallisce,
    /// la volta dopo riprova. Le partite senza proprietario le ha già date al primo
    /// amministratore SaveManager.AssignExistingPlaythroughs.
    /// </summary>
    public static void AssignFirstFavourites(SaveManager manager)
    {
        if (manager.State.OwnersAssigned || manager.Players == null)
            return;

        foreach (Person person in manager.Players.All.Where(person => manager.FavouriteOf(person.Id) == null))
        {
            List<string> mine = manager.GetProfilesOf(person.Id).ToList();

            if (mine.Count == 0)
                continue;

            string chosen = mine.FirstOrDefault(profile => profile == manager.State.ActiveProfile)
                            ?? mine.MaxBy(profile => manager.GetProfileLastModified(profile) ?? DateTime.MinValue)!;

            manager.SetFavourite(person.Id, chosen);
        }

        manager.State.OwnersAssigned = true;
        manager.State.Save();
        Log.Write("Partite esistenti: persone e preferite assegnate");
    }

    /// <summary>
    /// Dati della 1.0: un backup senza file .ini si descrive dal nome della cartella
    /// (le versioni precedenti non scrivevano il proprietario).
    /// </summary>
    public static Snapshot BackupFromName(Snapshot backup)
    {
        string reason = backup.Name;

        if (reason.StartsWith(SaveManager.ActivateBackupPrefix))
            return backup with { Kind = BackupKind.Activate, Detail = reason[SaveManager.ActivateBackupPrefix.Length..] };

        if (reason.StartsWith(SaveManager.SaveBackupPrefix))
            return backup with { Kind = BackupKind.Save, Owner = reason[SaveManager.SaveBackupPrefix.Length..] };

        if (reason.StartsWith(SaveManager.DeletedBackupPrefix))
            return backup with { Kind = BackupKind.Delete, Owner = reason[SaveManager.DeletedBackupPrefix.Length..] };

        return reason switch
        {
            "Prima_salvataggio" => backup with { Kind = BackupKind.Save },
            "Prima_creazione" => backup with { Kind = BackupKind.Create },
            SaveManager.RestoreBackupName => backup with { Kind = BackupKind.Restore },
            _ => backup
        };
    }

    /// <summary>
    /// Dati della 1.0: il motivo di un backup della 1.0, fatto prima del salvataggio
    /// quando non si scriveva la partita nel nome. null se non è uno di questi.
    /// </summary>
    public static string? OldBackupReasonText(string reason) =>
        reason == "Prima_salvataggio" ? Strings.ReasonBeforeSavingOld : null;

    /// <summary>
    /// Dati della 1.0: i backup di cui non si sa a quale partita appartengano (tranne
    /// le partite eliminate): quelli delle versioni precedenti, senza file .ini, e
    /// quelli fatti quando nessuna partita era nel gioco.
    /// </summary>
    public static IReadOnlyList<Snapshot> BackupsWithoutOwner(SaveManager manager) =>
        manager.GetBackups()
            .Where(b => b.Owner == "" && b.Kind != BackupKind.Delete)
            .ToList();

    // ZIP DI ESPORTAZIONE

    /// <summary>
    /// Dati della 1.0: gli zip di formato 1 e 2 non hanno il launcher (chiave
    /// Provider): sono di Steam.
    /// </summary>
    public static string ZipProvider(Dictionary<string, string> values) =>
        values.GetValueOrDefault("Provider", "steam");

    // POSTI

    private const string OldSpotsFile = "cartella.ini";
    private const string OldFolderKey = "CartellaSalvataggi";
    private const string OldOriginKey = "Origine";
    private const string ExtraKey = "CartelleExtra";

    /// <summary>
    /// Dati della 1.0: converte cartella.ini in posti.ini. La cartella e la sua
    /// origine diventano il posto 1, le cartelle extra e le altre chiavi passano a
    /// posti.ini. Poi cartella.ini si elimina, solo se posti.ini è stato scritto.
    /// Ritorna true se c'era da convertire (anche se la conversione non è riuscita:
    /// allora i posti restano vuoti e cartella.ini com'era).
    /// </summary>
    public static bool ConvertOldSpots(GameSpots spots)
    {
        string oldFile = Path.Combine(spots.GameDataFolder, OldSpotsFile);

        if (File.Exists(spots.FilePath) || !File.Exists(oldFile))
            return false;

        Dictionary<string, string> old = IniFile.Read(oldFile);
        string folder = old.GetValueOrDefault(OldFolderKey) ?? "";

        // Un percorso con "|" non entra in posti.ini: cartella.ini resta com'è.
        if (folder.Contains('|'))
        {
            Log.Write("cartella.ini con un percorso che contiene '|': non convertito: " + oldFile);
            return true;
        }

        if (folder != "")
            spots.SetPrimary(folder, GameSpots.OriginOf(old.GetValueOrDefault(OldOriginKey) ?? ""));

        if (old.TryGetValue(ExtraKey, out string? extra))
        {
            foreach (string root in extra.Split(';', StringSplitOptions.RemoveEmptyEntries))
                spots.AddExtraRoot(root);
        }

        IEnumerable<KeyValuePair<string, string>> others = old
            .Where(pair => pair.Key != OldFolderKey && pair.Key != OldOriginKey && pair.Key != ExtraKey);

        if (!spots.Save(others.ToList()))
            return true;

        try
        {
            File.Delete(oldFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write("cartella.ini non eliminato dopo la conversione: " + oldFile, ex);
        }

        Log.Write("cartella.ini convertito in posti.ini: " + spots.FilePath);

        return true;
    }
}
