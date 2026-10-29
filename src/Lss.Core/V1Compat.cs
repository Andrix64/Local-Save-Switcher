using System.Globalization;

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

    // DA .INI A .JSON (R9, R10)

    private const string PreviousSuffix = ".precedente";

    /// <summary>
    /// Dati della 1.0: i file .ini delle impostazioni di tutta l'app diventano .json, nella stessa cartella e con
    /// lo stesso nome (lss, persone, cartelle-ignorate, e gioco di ogni gioco aggiunto a mano). Gira a ogni avvio,
    /// dopo LegacyMigration e prima di leggere le impostazioni.
    /// </summary>
    public static void ConvertAppFiles(string root)
    {
        ConvertIniFile(Path.Combine(root, "lss.ini"), Path.Combine(root, "lss.json"), AppSettingsFromIni);
        ConvertIniFile(Path.Combine(root, "persone.ini"), Path.Combine(root, "persone.json"), PeopleFromIni);
        ConvertIniFile(Path.Combine(root, "cartelle-ignorate.ini"), Path.Combine(root, "cartelle-ignorate.json"), IgnoredFromIni);

        string manual = ManualGames.AccountFolder(root);

        if (!Directory.Exists(manual))
            return;

        foreach (string dir in Directory.GetDirectories(manual))
            ConvertIniFile(Path.Combine(dir, "gioco.ini"), Path.Combine(dir, "gioco.json"), ManualGameFromIni);
    }

    /// <summary>
    /// Converte un .ini in .json. Se il .json c'è già vince lui (il .ini, se c'è ancora, si mette da parte).
    /// Solo il .ini: si legge, si scrive il .json (file temporaneo e spostamento) e solo dopo il .ini diventa
    /// &lt;nome&gt;.ini.precedente, mai eliminato. Un arresto in mezzo lascia usabile il .ini o il .json: il
    /// prossimo avvio finisce il lavoro. Un .ini vuoto o illeggibile non si converte. true se ha convertito.
    /// </summary>
    internal static bool ConvertIniFile<T>(string iniPath, string jsonPath, Func<Dictionary<string, string>, T?> convert)
        where T : class
    {
        if (File.Exists(jsonPath))
        {
            SetAside(iniPath);
            return false;
        }

        if (!File.Exists(iniPath))
            return false;

        try
        {
            Dictionary<string, string> values = IniFile.Read(iniPath);

            if (values.Count == 0 || convert(values) is not { } converted)
            {
                Log.Write("File .ini vuoto o illeggibile, non convertito: " + iniPath);
                return false;
            }

            JsonFile.Write(jsonPath, converted);
            SetAside(iniPath);
            Log.Write("File .ini convertito in JSON: " + iniPath + " → " + jsonPath);

            return true;
        }
        catch (Exception ex)
        {
            Log.Write("Conversione di " + iniPath, ex);
            return false;
        }
    }

    // Il .ini diventa <nome>.ini.precedente (con un numero se ce n'è già uno): non si elimina mai.
    private static void SetAside(string iniPath)
    {
        if (!File.Exists(iniPath))
            return;

        try
        {
            string target = iniPath + PreviousSuffix;

            for (int n = 2; File.Exists(target); n++)
                target = iniPath + PreviousSuffix + "." + n.ToString(CultureInfo.InvariantCulture);

            File.Move(iniPath, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write("File .ini non messo da parte: " + iniPath, ex);
        }
    }

    private static AppSettingsFile AppSettingsFromIni(Dictionary<string, string> values) => new()
    {
        LastAccount = values.GetValueOrDefault("UltimoAccount", ""),
        LastGame = values.GetValueOrDefault("UltimoGioco", ""),
        LookAfterPlaying = values.GetValueOrDefault("CercaDopoPartita", "1") != "0",
        Mode = values.GetValueOrDefault("Modalita", AppSettings.AdvancedMode),
        FullScreen = values.GetValueOrDefault("SchermoIntero", "0") == "1",
    };

    // Le persone nell'ordine di "Ordine"; PIN, colore e ruolo come sono (i controlli li fa People.Load).
    private static PeopleFile PeopleFromIni(Dictionary<string, string> values)
    {
        var file = new PeopleFile();
        string[] order = values.TryGetValue("Ordine", out string? ordine)
            ? ordine.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

        foreach (string id in order)
        {
            if (!values.TryGetValue(id + ".Nome", out string? name) || file.People.Any(person => person.Id == id))
                continue;

            file.People.Add(new PersonEntry
            {
                Id = id,
                Name = name,
                Color = values.GetValueOrDefault(id + ".Colore", ""),
                Role = values.GetValueOrDefault(id + ".Ruolo", "utente"),
                Pin = values.GetValueOrDefault(id + ".Pin", ""),
            });
        }

        return file;
    }

    private static IgnoredFile IgnoredFromIni(Dictionary<string, string> values)
    {
        var file = new IgnoredFile();

        for (int n = 0; values.TryGetValue("Cartella." + n, out string? folder); n++)
        {
            file.Folders.Add(new IgnoredEntry
            {
                Folder = folder,
                Added = values.GetValueOrDefault("Aggiunta." + n, ""),
            });
        }

        return file;
    }

    private static ManualGameFile? ManualGameFromIni(Dictionary<string, string> values) =>
        values.TryGetValue("Nome", out string? name)
            ? new ManualGameFile
            {
                Name = name,
                Exe = values.GetValueOrDefault("Eseguibile", ""),
                Arguments = values.GetValueOrDefault("Argomenti", ""),
                Prefix = values.GetValueOrDefault("Prefisso", ""),
            }
            : null;

    // DA .INI A .JSON: I FILE DI OGNI GIOCO (R10)

    /// <summary>
    /// Dati della 1.0: i file .ini della cartella dati di ogni gioco diventano .json (stato, preferite, posti,
    /// proposte, le informazioni delle partite in Info e delle copie in Backup). Gira a ogni avvio, subito dopo
    /// ConvertAppFiles e prima di aprire un gioco. Le cartelle dei giochi sono dati\launcher\account\gioco.
    /// </summary>
    public static void ConvertGameFiles(string root)
    {
        if (!Directory.Exists(root))
            return;

        foreach (string provider in Directory.GetDirectories(root))
        {
            foreach (string account in Directory.GetDirectories(provider))
            {
                foreach (string game in Directory.GetDirectories(account))
                    ConvertGameFolder(game);
            }
        }
    }

    // Tutti i file .ini di un gioco. Gli errori di un file si registrano e non fermano gli altri.
    private static void ConvertGameFolder(string game)
    {
        ConvertIniFile(Path.Combine(game, "stato.ini"), Path.Combine(game, "stato.json"), AppStateFromIni);
        ConvertIniFile(Path.Combine(game, "preferite.ini"), Path.Combine(game, "preferite.json"), FavouritesFromIni);
        ConvertIniFile(Path.Combine(game, "posti.ini"), Path.Combine(game, "posti.json"), SpotsFromIni);
        // cartella.ini (1.0) conta solo se posti.json non c'è ancora: altrimenti si mette da parte.
        ConvertIniFile(Path.Combine(game, "cartella.ini"), Path.Combine(game, "posti.json"), SpotsFromOldFolderIni);
        ConvertIniFile(Path.Combine(game, "proposte.ini"), Path.Combine(game, "proposte.json"), SuggestionsFromIni);

        foreach (string folder in new[] { Path.Combine(game, "Info"), Path.Combine(game, "Backup") })
        {
            if (!Directory.Exists(folder))
                continue;

            foreach (string ini in Directory.GetFiles(folder, "*.ini"))
                ConvertIniFile(ini, Path.ChangeExtension(ini, ".json"), InfoFromIni);
        }
    }

    // Il valore di stato.ini: "-" vuol dire vuoto.
    private static string StateValue(Dictionary<string, string> values, string key) =>
        values.GetValueOrDefault(key, "") is var value && value == "-" ? "" : value;

    private static AppStateFile AppStateFromIni(Dictionary<string, string> values)
    {
        var file = new AppStateFile
        {
            ActiveProfile = StateValue(values, "ProfiloAttivo"),
            ActiveSince = StateValue(values, "AttivatoIl"),
            LastSaved = StateValue(values, "SalvatoIl"),
            LaunchGame = StateValue(values, "AvviaGioco") != "0",
            SaveOnGameExit = StateValue(values, "SalvaAllaChiusura") != "0",
            SharePreferences = StateValue(values, "OpzioniCondivise") == "1",
            CloudAllowedInSimple = StateValue(values, "CloudConsentito") == "1",
            OwnersAssigned = StateValue(values, "ProprietariOk") == "1",
        };

        if (int.TryParse(StateValue(values, "BackupDaConservare"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int keep) && keep >= 0)
            file.BackupsToKeep = keep;

        if (values.ContainsKey("FileCondivisi"))
        {
            file.SharedFiles = StateValue(values, "FileCondivisi")
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        return file;
    }

    // Persona -> partita preferita, e persona.Ultima -> data dell'ultima partita.
    private static FavouritesFileData FavouritesFromIni(Dictionary<string, string> values)
    {
        const string suffix = ".Ultima";
        var file = new FavouritesFileData();

        foreach ((string key, string value) in values)
        {
            if (key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                file.LastPlayed[key[..^suffix.Length]] = value;
            else
                file.Favourites[key] = value;
        }

        return file;
    }

    // posti.ini: Prossimo, Posto.n = cartella|portabile|maschera|ricorsiva|origine, Launcher.chiave = id,
    // CartelleExtra = a;b, Ignorate.n e Aggiunta.n.
    private static SpotsFile SpotsFromIni(Dictionary<string, string> values)
    {
        var file = new SpotsFile();

        foreach ((string key, string value) in values)
        {
            if (key == "Prossimo" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int next))
            {
                file.Next = next;
            }
            else if (key.StartsWith("Posto.", StringComparison.OrdinalIgnoreCase)
                     && int.TryParse(key["Posto.".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
            {
                string[] parts = value.Split('|');

                if (parts.Length == 5 && parts[0] != "")
                {
                    file.Spots.Add(new SpotEntry
                    {
                        Id = id,
                        Folder = parts[0],
                        Portable = parts[1],
                        Pattern = parts[2],
                        Recursive = parts[3] == "1",
                        Origin = parts[4],
                    });
                }
            }
            else if (key.StartsWith("Launcher.", StringComparison.OrdinalIgnoreCase)
                     && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int launcherId))
            {
                file.Launchers.Add(new LauncherSpotEntry { Key = key["Launcher.".Length..], Id = launcherId });
            }
            else if (key == "CartelleExtra")
            {
                file.ExtraRoots.AddRange(value.Split(';', StringSplitOptions.RemoveEmptyEntries));
            }
        }

        for (int n = 0; values.TryGetValue("Ignorate." + n, out string? folder); n++)
            file.Ignored.Add(new IgnoredEntry { Folder = folder, Added = values.GetValueOrDefault("Aggiunta." + n, "") });

        return file;
    }

    // cartella.ini della 1.0: la cartella e la sua origine sono il posto 1, le cartelle extra restano extra.
    // Un percorso con "|" non entrava in posti.ini: cartella.ini resta com'è (null).
    private static SpotsFile? SpotsFromOldFolderIni(Dictionary<string, string> values)
    {
        string folder = values.GetValueOrDefault("CartellaSalvataggi", "");

        if (folder.Contains('|'))
        {
            Log.Write("cartella.ini con un percorso che contiene '|': non convertito");
            return null;
        }

        // Le cartelle extra e quelle ignorate si leggono come in posti.ini.
        SpotsFile file = SpotsFromIni(values);

        if (folder != "")
        {
            file.Next = 2;
            file.Spots.Add(new SpotEntry { Id = 1, Folder = folder, Portable = folder, Recursive = true, Origin = values.GetValueOrDefault("Origine", "") });
        }

        return file;
    }

    // Proposta.n = cartella|maschera|punteggio|data[|tipo:punti:dettaglio;...], Scartata.n = cartella|maschera.
    private static SuggestionsFile SuggestionsFromIni(Dictionary<string, string> values)
    {
        var file = new SuggestionsFile();

        foreach ((string key, string value) in values)
        {
            string[] parts = value.Split('|');

            if (key.StartsWith("Proposta.", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length is < 4 or > 5
                    || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int score))
                {
                    continue;
                }

                var entry = new SuggestionEntry { Folder = parts[0], Pattern = parts[1], Score = score, Found = parts[3] };

                if (parts.Length == 5)
                {
                    foreach (string item in parts[4].Split(';', StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] fields = item.Split(':', 3);

                        if (fields.Length == 3
                            && int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int points))
                        {
                            entry.Reasons.Add(new ReasonEntry { Kind = fields[0], Points = points, Detail = fields[2] });
                        }
                    }
                }

                file.Suggestions.Add(entry);
            }
            else if (key.StartsWith("Scartata.", StringComparison.OrdinalIgnoreCase) && parts.Length == 2)
            {
                file.Dismissed.Add(new DismissedEntry { Folder = parts[0], Pattern = parts[1] });
            }
        }

        return file;
    }

    // Info\partita.ini e Backup\copia.ini: nota, tempo, ultima sessione, giocatore; la copia ha anche Partita,
    // Motivo e Dettaglio.
    private static InfoFileData InfoFromIni(Dictionary<string, string> values)
    {
        var data = new InfoFileData
        {
            Note = values.GetValueOrDefault("Nota", ""),
            LastSession = values.GetValueOrDefault("UltimaSessione", ""),
            Owner = values.GetValueOrDefault("Giocatore", ""),
        };

        if (long.TryParse(values.GetValueOrDefault("TempoDiGioco"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long seconds) && seconds > 0)
            data.PlayTime = seconds;

        if (values.TryGetValue("Motivo", out string? reason))
        {
            data.Reason = reason;
            data.Playthrough = values.GetValueOrDefault("Partita", "");
            data.Detail = values.GetValueOrDefault("Dettaglio", "");
        }

        return data;
    }

    // ZIP DI ESPORTAZIONE

    /// <summary>
    /// Dati della 1.0: le informazioni di uno zip esportato con file .ini (formati 1-4). Gli zip di formato 1 e 2
    /// non hanno il launcher (sono di Steam), quelli di Control Save Manager non hanno AppId (LegacyMigration).
    /// Senza partita.ini l'elenco è vuoto, come prima.
    /// </summary>
    internal static ExportInfoData ReadExportedInfo(string folder)
    {
        Dictionary<string, string> values = IniFile.Read(Path.Combine(folder, "partita.ini"));
        (string appId, string gameName) = LegacyMigration.ExportedGame(values);

        return new ExportInfoData
        {
            Provider = values.GetValueOrDefault("Provider", "steam"),
            AppId = appId,
            Game = gameName,
            Name = values.GetValueOrDefault("Nome", ""),
            Info = InfoFromIni(values),
            OwnerName = values.GetValueOrDefault("NomeGiocatore", ""),
        };
    }

    /// <summary>
    /// Dati della 1.0: i posti di uno zip esportato (posizioni.ini: id = portabile|maschera|ricorsiva), o null
    /// se lo zip non li ha. Una riga non valida fa rifiutare lo zip (InvalidDataException).
    /// </summary>
    internal static ExportSpotsData? ReadExportedSpots(string folder)
    {
        string path = Path.Combine(folder, "posizioni.ini");

        if (!File.Exists(path))
            return null;

        var data = new ExportSpotsData();

        foreach ((string key, string value) in IniFile.Read(path))
        {
            if (!int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out int id))
                continue;

            string[] parts = value.Split('|');

            if (parts.Length != 3)
                throw new InvalidDataException("posto non valido nello zip: " + key);

            data.Spots.Add(new ExportSpotEntry { Id = id, Portable = parts[0], Pattern = parts[1], Recursive = parts[2] == "1" });
        }

        return data;
    }
}
