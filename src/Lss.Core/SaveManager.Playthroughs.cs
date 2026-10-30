using System.Globalization;
using System.IO.Compression;
using System.Text.Json.Serialization;

namespace Lss;

/// <summary>
/// Operazione che ha creato un backup: scritta nel file .json accanto al
/// backup e usata per descriverlo nella Cronologia.
/// </summary>
public static class BackupKind
{
    public const string Activate = "attivazione";
    public const string Save = "salvataggio";
    public const string SaveOnExit = "chiusura";
    public const string Restore = "ripristino";
    public const string Create = "creazione";
    public const string Delete = "eliminazione";
    public const string NewSpot = "nuovo posto";
}

/// <summary>
/// Informazioni di una partita che non stanno nei salvataggi.
/// </summary>
/// <param name="Note">Nota libera dell'utente, anche vuota.</param>
/// <param name="PlayTime">Tempo di gioco misurato dall'app.</param>
/// <param name="LastSession">Fine dell'ultima sessione di gioco misurata, se c'è.</param>
/// <param name="Owner">Id della persona a cui appartiene la partita; "" se manca.</param>
public sealed record PlaythroughInfo(string Note, TimeSpan PlayTime, DateTime? LastSession, string Owner = "")
{
    public static readonly PlaythroughInfo Empty = new("", TimeSpan.Zero, null, "");
}

/// <summary>
/// Come sono scritti Info/&lt;partita&gt;.json e Backup/&lt;copia&gt;.json: nomi corti in italiano.
/// Partita, Motivo e Dettaglio ci sono solo nel file di una copia (Backup).
/// </summary>
internal sealed class InfoFileData
{
    [JsonPropertyName("Partita")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Playthrough { get; set; }

    [JsonPropertyName("Motivo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; set; }

    [JsonPropertyName("Dettaglio")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; set; }

    [JsonPropertyName("Nota")]
    public string Note { get; set; } = "";

    // Secondi.
    [JsonPropertyName("TempoDiGioco")]
    public long PlayTime { get; set; }

    // Formato "yyyy-MM-dd HH:mm:ss"; vuoto se non c'è.
    [JsonPropertyName("UltimaSessione")]
    public string LastSession { get; set; } = "";

    [JsonPropertyName("Giocatore")]
    public string Owner { get; set; } = "";
}

/// <summary>Come partita.json, dentro lo zip di una partita esportata, è scritto.</summary>
internal sealed class ExportInfoData
{
    [JsonPropertyName("Formato")]
    public int Format { get; set; }

    [JsonPropertyName("Provider")]
    public string Provider { get; set; } = "";

    [JsonPropertyName("AppId")]
    public string AppId { get; set; } = "";

    [JsonPropertyName("Gioco")]
    public string Game { get; set; } = "";

    [JsonPropertyName("Nome")]
    public string Name { get; set; } = "";

    [JsonPropertyName("Info")]
    public InfoFileData Info { get; set; } = new();

    // Il nome della persona sul PC di partenza: serve solo a leggere il file.
    [JsonPropertyName("NomeGiocatore")]
    public string OwnerName { get; set; } = "";
}

/// <summary>Un posto del gioco nello zip, come è scritto in posizioni.json.</summary>
internal sealed class ExportSpotEntry
{
    [JsonPropertyName("Id")]
    public int Id { get; set; }

    [JsonPropertyName("Portabile")]
    public string Portable { get; set; } = "";

    [JsonPropertyName("Maschera")]
    public string Pattern { get; set; } = "";

    [JsonPropertyName("Ricorsiva")]
    public bool Recursive { get; set; }
}

/// <summary>Come posizioni.json, dentro lo zip, è scritto.</summary>
internal sealed class ExportSpotsData
{
    [JsonPropertyName("Posti")]
    public List<ExportSpotEntry> Spots { get; set; } = [];
}

/// <summary>
/// Partite: informazioni, proprietario dei backup, opzioni condivise,
/// esportazione, importazione, recupero e avvio del gioco.
/// </summary>
public sealed partial class SaveManager
{
    // Formato delle date nei file .json.
    private const string InfoDateFormat = "yyyy-MM-dd HH:mm:ss";

    // File dentro lo zip di una partita esportata.
    private const string ExportInfoFile = "partita.json";
    private const string ExportSavesFolder = "salvataggi";
    private const string ExportSavestatesFolder = "punti";

    // Formato dello zip: 5 = file JSON (partita.json, posizioni.json); 1-4 avevano file .ini (V1Compat).
    private const int ExportFormat = 5;

    // Posti del gioco nello zip.
    private const string ExportSpotsFile = "posizioni.json";

    /// <summary>
    /// Cartella con un file .json per partita (nota, tempo di gioco). Sta fuori
    /// dalla cartella della partita, che viene copiata così com'è nel gioco.
    /// </summary>
    public string InfoPath { get; }

    // INFORMAZIONI DELLA PARTITA

    private string InfoFile(string profileName) => Path.Combine(InfoPath, profileName + ".json");

    public PlaythroughInfo GetInfo(string profileName)
    {
        return ParseInfo(ReadInfoData(InfoFile(profileName)));
    }

    // Il file (vuoto se manca o è rotto).
    private static InfoFileData ReadInfoData(string path) => JsonFile.Read<InfoFileData>(path) ?? new InfoFileData();

    private static PlaythroughInfo ParseInfo(InfoFileData data)
    {
        TimeSpan playTime = data.PlayTime > 0 ? TimeSpan.FromSeconds(data.PlayTime) : TimeSpan.Zero;

        DateTime? last = DateTime.TryParseExact(data.LastSession, InfoDateFormat,
            CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)
            ? date
            : null;

        return new PlaythroughInfo(data.Note, playTime, last, data.Owner);
    }

    private static InfoFileData FormatInfo(PlaythroughInfo info) => new()
    {
        Note = info.Note,
        PlayTime = (long)info.PlayTime.TotalSeconds,
        LastSession = info.LastSession?.ToString(InfoDateFormat, CultureInfo.InvariantCulture) ?? "",
        Owner = info.Owner,
    };

    public Result SetNote(string profileName, string note)
    {
        // Le note possono andare a capo (textarea): si toglie solo il ritorno carrello di Windows.
        note = note.Trim().Replace("\r", "");

        try
        {
            JsonFile.Write(InfoFile(profileName), FormatInfo(GetInfo(profileName) with { Note = note }));
        }
        catch (Exception ex)
        {
            return Log.Write("Nota di " + profileName, Result.Fail(Loc.F(Strings.ErrorWithMessage, ex.Message)));
        }

        return Result.Ok(Strings.NoteSaved);
    }

    /// <summary>
    /// Aggiunge una sessione di gioco al tempo della partita. Non lancia
    /// eccezioni: in caso di errore scrive solo nel registro.
    /// </summary>
    public void AddPlayTime(string profileName, DateTime start, DateTime end)
    {
        if (profileName == "" || end <= start)
            return;

        try
        {
            PlaythroughInfo info = GetInfo(profileName);

            JsonFile.Write(InfoFile(profileName), FormatInfo(info with
            {
                PlayTime = info.PlayTime + (end - start),
                LastSession = end
            }));

            Log.Write($"Sessione di gioco in {profileName}: {(int)(end - start).TotalMinutes} min");
            MarkPlayed(profileName, end);
        }
        catch (Exception ex)
        {
            Log.Write("Tempo di gioco di " + profileName, ex);
        }
    }

    /// <summary>
    /// Sposta le informazioni di una partita rinominata.
    /// </summary>
    private void MoveInfo(string profileName, string newProfileName, bool caseOnly)
    {
        string source = InfoFile(profileName);

        try
        {
            if (!File.Exists(source))
                return;

            string destination = InfoFile(newProfileName);

            if (caseOnly)
            {
                File.Move(source, destination + ".rinomina", true);
                File.Move(destination + ".rinomina", destination, true);
            }
            else
            {
                File.Move(source, destination, true);
            }
        }
        catch (Exception ex)
        {
            Log.Write("Informazioni di " + profileName + " non spostate", ex);
        }
    }

    /// <summary>
    /// Mette nota e tempo di gioco di una partita eliminata nel file del suo
    /// backup, per il recupero, e toglie il file delle informazioni.
    /// </summary>
    private void RecordDeletedInfo(string profileName, string backup)
    {
        PlaythroughInfo info = GetInfo(profileName);

        try
        {
            InfoFileData data = FormatInfo(info);
            data.Playthrough = profileName;
            data.Reason = BackupKind.Delete;
            data.Detail = "";

            JsonFile.Write(BackupInfoFile(backup), data);

            if (File.Exists(InfoFile(profileName)))
                File.Delete(InfoFile(profileName));
        }
        catch (Exception ex)
        {
            Log.Write("Informazioni di " + profileName + " non conservate", ex);
        }
    }

    // PROPRIETARIO DELLE PARTITE

    /// <summary>
    /// Persone di LSS. Con l'elenco, il proprietario deve esistere; senza
    /// (test, casi senza persone), basta un id non vuoto. Lo impostano Open e Initialize.
    /// </summary>
    internal People? Players { get; set; }

    /// <summary>
    /// Id del primo giocatore: proprietario delle partite nuove finché
    /// l'interfaccia non ne sceglie uno. "" se non ci sono persone.
    /// </summary>
    public string DefaultOwnerId => Players?.All.FirstOrDefault()?.Id ?? "";

    private bool OwnerIsKnown(string personId) =>
        personId != "" && (Players == null || Players.Find(personId) != null);

    /// <summary>
    /// Scrive il proprietario nelle informazioni della partita, senza toccare il resto.
    /// </summary>
    private void WriteOwner(string profileName, string personId) =>
        JsonFile.Write(InfoFile(profileName), FormatInfo(GetInfo(profileName) with { Owner = personId }));

    /// <summary>
    /// Dà la partita a una persona. Il proprietario dei salvataggi non cambia.
    /// </summary>
    public Result SetOwner(string profileName, string personId)
    {
        Result result = SetOwnerCore(profileName, personId);

        if (result.Success)
            RepairFavourites();

        return Log.Write($"Proprietario di {profileName}: {personId}", result);
    }

    private Result SetOwnerCore(string profileName, string personId)
    {
        if (!OwnerIsKnown(personId))
            return Result.Fail(Strings.PlayerNotFound);

        if (!ProfileExists(profileName))
            return Result.Fail(Strings.ProfileNotFound);

        try
        {
            WriteOwner(profileName, personId);
        }
        catch (Exception ex)
        {
            return Result.Fail(Loc.F(Strings.ErrorWithMessage, ex.Message));
        }

        return Result.Ok("");
    }

    /// <summary>
    /// Partite di una persona, per nome di cartella.
    /// </summary>
    public IReadOnlyList<string> GetProfilesOf(string personId) =>
        GetProfiles().Where(profile => GetInfo(profile).Owner == personId).ToList();

    // BACKUP: PROPRIETARIO E DESCRIZIONE

    private static string BackupInfoFile(string backup) => backup + ".json";

    /// <summary>
    /// Scrive accanto al backup a quale partita appartiene il contenuto e
    /// quale operazione l'ha creato. Non lancia eccezioni: senza il file il
    /// backup resta valido, solo meno descritto. Ritorna false se non riesce a scrivere.
    /// </summary>
    private static bool WriteBackupInfo(string backup, string owner, string kind, string detail)
    {
        try
        {
            JsonFile.Write(BackupInfoFile(backup), new InfoFileData
            {
                Playthrough = owner,
                Reason = kind,
                Detail = detail,
            });

            return true;
        }
        catch (Exception ex)
        {
            Log.Write("Informazioni del backup " + backup, ex);
            return false;
        }
    }

    private static void DeleteBackupInfo(string backup)
    {
        try
        {
            File.Delete(BackupInfoFile(backup));
        }
        catch
        {
            // Un .json rimasto senza backup non dà fastidio.
        }
    }

    /// <summary>
    /// Completa un backup con il proprietario e l'operazione: dal suo file .json,
    /// o dal nome della cartella per i backup senza (V1Compat).
    /// </summary>
    private static Snapshot WithBackupInfo(Snapshot backup)
    {
        InfoFileData data = ReadInfoData(BackupInfoFile(backup.Path));

        if (data.Reason != null)
        {
            return backup with
            {
                Owner = data.Playthrough ?? "",
                Kind = data.Reason,
                Detail = data.Detail ?? ""
            };
        }

        // V1Compat: backup senza file .json, descritti dal nome della cartella
        return V1Compat.BackupFromName(backup);
    }

    /// <summary>
    /// Descrizione di un backup per la Cronologia ("Prima di passare a «X»").
    /// </summary>
    public static string BackupDescription(Snapshot backup)
    {
        string detail = ProfileDisplayName(backup.Detail);

        return backup.Kind switch
        {
            BackupKind.Activate => Loc.F(Strings.DescActivate, detail),
            BackupKind.Save => Strings.DescSave,
            BackupKind.SaveOnExit => Strings.DescSaveOnExit,
            BackupKind.Restore => detail == ""
                ? Strings.DescRestore
                : Loc.F(Strings.DescRestoreNamed, detail),
            BackupKind.Create => detail == ""
                ? Strings.DescCreate
                : Loc.F(Strings.DescCreateNamed, detail),
            BackupKind.Delete => Loc.F(Strings.DescDeleted, ProfileDisplayName(backup.Owner)),
            _ => BackupReasonText(backup.Name)
        };
    }

    /// <summary>
    /// Copie automatiche della partita, dalla più recente: quelle che
    /// contengono i suoi salvataggi. Le partite eliminate stanno a parte.
    /// </summary>
    public IReadOnlyList<Snapshot> GetBackupsOf(string profileName)
    {
        return GetBackups()
            .Where(b => b.Owner == profileName && b.Kind != BackupKind.Delete)
            .ToList();
    }

    // PARTITE ELIMINATE

    /// <summary>
    /// Partite eliminate che si possono ancora recuperare, dalla più recente.
    /// </summary>
    public IReadOnlyList<Snapshot> GetDeletedProfiles()
    {
        return GetBackups().Where(b => b.Kind == BackupKind.Delete && b.Owner != "").ToList();
    }

    /// <summary>
    /// Rimette tra le partite una partita eliminata, con la sua nota e il suo
    /// tempo di gioco. Se il nome è stato riusato ne prende uno libero.
    /// </summary>
    public Result RecoverDeletedProfile(Snapshot deleted, string ownerId)
    {
        Result result = RecoverDeletedProfileCore(deleted, ownerId);

        if (result.Success)
            RepairFavourites();

        return Finish("Recupera " + deleted.Owner, result);
    }

    private Result RecoverDeletedProfileCore(Snapshot deleted, string ownerId)
    {
        if (!OwnerIsKnown(ownerId))
            return Result.Fail(Strings.PlayerNotFound);

        if (deleted.Kind != BackupKind.Delete || !IsInside(deleted.Path, BackupPath) || !Directory.Exists(deleted.Path))
            return Result.Fail(Loc.F(Strings.FolderNotFound, deleted.Path));

        string name = UniqueName(ProfileDisplayName(deleted.Owner));
        string profileName = ProfileFolderName(name);

        // Il proprietario è quello scelto ora: quello di prima può non esistere più.
        PlaythroughInfo info = ParseInfo(ReadInfoData(BackupInfoFile(deleted.Path))) with { Owner = ownerId };

        try
        {
            Directory.Move(deleted.Path, Path.Combine(ProfilesPath, profileName));
        }
        catch (Exception ex)
        {
            return Result.Fail(Loc.F(Strings.RecoverFailed, ex.Message));
        }

        DeleteBackupInfo(deleted.Path);

        try
        {
            JsonFile.Write(InfoFile(profileName), FormatInfo(info));
        }
        catch (Exception ex)
        {
            Log.Write("Informazioni di " + profileName + " non recuperate", ex);
        }

        return Result.Ok(Loc.F(Strings.Recovered, name));
    }

    /// <summary>
    /// Elimina per sempre una partita eliminata: non si può annullare. Come per il
    /// recupero, il backup deve stare in BackupPath ed essere una partita eliminata.
    /// </summary>
    public Result PurgeDeletedProfile(Snapshot deleted)
    {
        return Finish("Elimina per sempre " + ProfileDisplayName(deleted.Owner), PurgeDeletedProfileCore(deleted));
    }

    private Result PurgeDeletedProfileCore(Snapshot deleted)
    {
        if (deleted.Kind != BackupKind.Delete || !IsInside(deleted.Path, BackupPath) || !Directory.Exists(deleted.Path))
            return Result.Fail(Loc.F(Strings.FolderNotFound, deleted.Path));

        try
        {
            Directory.Delete(deleted.Path, true);
        }
        catch (Exception ex)
        {
            return Result.Fail(Loc.F(Strings.ErrorWithMessage, ex.Message));
        }

        DeleteBackupInfo(deleted.Path);
        return Result.Ok(Strings.Purged);
    }

    /// <summary>
    /// Nota e tempo di gioco di una partita eliminata (dal suo file di backup).
    /// </summary>
    public PlaythroughInfo DeletedInfoOf(Snapshot deleted) =>
        ParseInfo(ReadInfoData(BackupInfoFile(deleted.Path)));

    /// <summary>
    /// Id della persona a cui apparteneva una partita eliminata, o "" se non si sa.
    /// </summary>
    public string DeletedOwnerOf(Snapshot deleted) =>
        ReadInfoData(BackupInfoFile(deleted.Path)).Owner;

    /// <summary>
    /// Proprietario per recuperare una partita eliminata: la persona che la aveva, se esiste
    /// ancora; altrimenti il primo amministratore (come per le partite di prima di LSS 2.0).
    /// </summary>
    public string RecoveryOwnerOf(Snapshot deleted)
    {
        string before = DeletedOwnerOf(deleted);

        if (before != "" && OwnerIsKnown(before))
            return before;

        return Players?.Admins.FirstOrDefault()?.Id ?? "";
    }

    /// <summary>
    /// name, oppure "name 2", "name 3"... se esiste già una partita con quel nome.
    /// </summary>
    public string UniqueName(string name)
    {
        if (!IsValidName(name))
            name = Strings.ImportedDefaultName;

        if (!ProfileExists(ProfileFolderName(name)))
            return name;

        for (int i = 2; ; i++)
        {
            string suffix = " " + i.ToString(CultureInfo.InvariantCulture);
            string candidate = (name.Length + suffix.Length > 50 ? name[..(50 - suffix.Length)].TrimEnd() : name) + suffix;

            if (!ProfileExists(ProfileFolderName(candidate)))
                return candidate;
        }
    }

    // PRIMO AVVIO

    /// <summary>
    /// Salva i salvataggi che il gioco ha adesso come nuova partita e la
    /// segna come quella nel gioco, senza toccare la cartella del gioco.
    /// </summary>
    public Result AdoptGameSaves(string name, string ownerId)
    {
        Result created = CreateProfile(name, copyCurrentSaves: true, ownerId);

        if (!created.Success)
            return created;

        State.ActiveProfile = ProfileFolderName(name);
        State.ActiveSince = DateTime.Now.ToString(DateFormat, CultureInfo.InvariantCulture);
        State.LastSaved = State.ActiveSince;

        try
        {
            State.Save();
        }
        catch (Exception ex)
        {
            return Log.Write("Adotta " + name,
                Result.Fail(Loc.F(Strings.ActivatedStateNotSaved, ex.Message), created.BackupPath));
        }

        return Log.Write("Adotta " + name, Result.Ok(Loc.F(Strings.Adopted, name), created.BackupPath));
    }

    // IMPOSTAZIONI

    /// <summary>
    /// Salva le impostazioni ed elimina subito le copie automatiche in eccesso.
    /// </summary>
    public Result SaveSettings(bool launchGame, bool saveOnGameExit, bool sharePreferences, int backupsToKeep,
        IReadOnlyList<string>? sharedFiles = null)
    {
        State.LaunchGame = launchGame;
        State.SaveOnGameExit = saveOnGameExit;
        State.SharePreferences = sharePreferences;

        if (sharedFiles != null)
            State.SharedFiles = sharedFiles;

        Result result = SetBackupsToKeep(backupsToKeep);

        Log.Write($"Impostazioni: avvio {launchGame}, salvataggio alla chiusura {saveOnGameExit}, " +
                  $"opzioni condivise {sharePreferences}, copie {backupsToKeep}");

        return result;
    }

    /// <summary>
    /// Consenti (o togli) le copie online anche in modalità semplice, per questo gioco.
    /// Non lancia eccezioni: l'esito è nel Result.
    /// </summary>
    public Result SetCloudAllowedInSimple(bool allowed)
    {
        bool before = State.CloudAllowedInSimple;
        State.CloudAllowedInSimple = allowed;

        try
        {
            State.Save();
            return Log.Write($"Modalità semplice con cloud per {Game.Name}: {allowed}", Result.Ok(""));
        }
        catch (Exception ex)
        {
            State.CloudAllowedInSimple = before;
            return Log.Write("Consenso cloud di " + Game.Name, Result.Fail(Loc.F(Strings.ErrorWithMessage, ex.Message)));
        }
    }

    // OPZIONI CONDIVISE

    /// <summary>
    /// File da tenere uguali in tutte le partite, relativi a SavePath. Sono solo
    /// del posto 1: gli altri posti (SaveSet) non hanno file condivisi.
    /// </summary>
    public IReadOnlyList<string> SharedFiles => State.SharedFiles ?? GameDefaults.SharedFiles(Game.Id);

    /// <summary>
    /// Copia le opzioni condivise che il gioco ha adesso nella cartella di una
    /// partita. Non lancia eccezioni: senza opzioni la partita usa le sue.
    /// </summary>
    private void CopyPreferencesTo(string profile)
    {
        foreach (string relative in SharedFiles)
        {
            string? source = SharedFilePath(relative);

            if (source == null || !File.Exists(source))
                continue;

            try
            {
                string destination = Path.Combine(profile, ToLocal(relative));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, true);
            }
            catch (Exception ex)
            {
                Log.Write("Opzioni del gioco non copiate in " + profile, ex);
            }
        }
    }

    /// <summary>
    /// Legge le opzioni condivise che il gioco ha adesso, per percorso relativo.
    /// I file mancanti o illeggibili vengono saltati.
    /// </summary>
    private Dictionary<string, byte[]> ReadPreferences()
    {
        var files = new Dictionary<string, byte[]>();

        foreach (string relative in SharedFiles)
        {
            string? file = SharedFilePath(relative);

            if (file == null || !File.Exists(file))
                continue;

            try
            {
                files[relative] = File.ReadAllBytes(file);
            }
            catch (Exception ex)
            {
                Log.Write("Opzioni del gioco non lette: " + relative, ex);
            }
        }

        return files;
    }

    private void WritePreferences(Dictionary<string, byte[]> files)
    {
        foreach ((string relative, byte[] content) in files)
        {
            string? file = SharedFilePath(relative);

            if (file == null)
                continue;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllBytes(file, content);
            }
            catch (Exception ex)
            {
                Log.Write("Opzioni del gioco non rimesse a posto: " + relative, ex);
            }
        }
    }

    /// <summary>
    /// Percorso di un file condiviso nella cartella del gioco, o null se il
    /// percorso esce da quella cartella (il file viene ignorato).
    /// </summary>
    private string? SharedFilePath(string relative)
    {
        string file = Path.Combine(SavePath, ToLocal(relative));

        if (IsInside(file, SavePath))
            return file;

        Log.Write("File condiviso fuori dalla cartella del gioco: " + relative);
        return null;
    }

    private static string ToLocal(string relative) => relative.Replace('/', Path.DirectorySeparatorChar);

    // ESPORTAZIONE E IMPORTAZIONE

    /// <summary>
    /// Salva una partita in un file zip: salvataggi, punti salvati, nota e
    /// tempo di gioco.
    /// </summary>
    public Result ExportProfile(string profileName, string zipPath)
    {
        return Log.Write($"Esporta {profileName} in {zipPath}", ExportProfileCore(profileName, zipPath));
    }

    private Result ExportProfileCore(string profileName, string zipPath)
    {
        string profile = Path.Combine(ProfilesPath, profileName);

        if (!Directory.Exists(profile))
            return Result.Fail(Strings.ProfileNotFound);

        string temp = Path.Combine(Path.GetTempPath(), "lss_esporta_" + Guid.NewGuid().ToString("N"));
        string tempZip = zipPath + ".tmp";

        try
        {
            FileOps.CopyFolderVerified(profile, Path.Combine(temp, ExportSavesFolder));

            string savestates = Path.Combine(SavestatePath, profileName);

            if (Directory.Exists(savestates))
                FileOps.CopyFolderVerified(savestates, Path.Combine(temp, ExportSavestatesFolder));

            PlaythroughInfo exported = GetInfo(profileName);

            // Info.Giocatore è l'id della persona sul PC di partenza: al momento di importare si sceglie
            // un'altra persona. NomeGiocatore serve solo a leggere il file.
            JsonFile.Write(Path.Combine(temp, ExportInfoFile), new ExportInfoData
            {
                Format = ExportFormat,
                Provider = Game.Provider,
                AppId = Game.Id,
                Game = Game.Name,
                Name = ProfileDisplayName(profileName),
                Info = FormatInfo(exported),
                OwnerName = Players?.Find(exported.Owner)?.Name ?? "",
            });

            // I posti del gioco, per abbinarli all'importazione.
            if (Game.Spots.Count > 0)
            {
                JsonFile.Write(Path.Combine(temp, ExportSpotsFile), new ExportSpotsData
                {
                    Spots = Game.Spots.Select(spot => new ExportSpotEntry
                    {
                        Id = spot.Id,
                        Portable = spot.PortablePath,
                        Pattern = spot.Pattern,
                        Recursive = spot.Recursive,
                    }).ToList(),
                });
            }

            if (File.Exists(tempZip))
                File.Delete(tempZip);

            ZipFile.CreateFromDirectory(temp, tempZip);

            // Lo zip deve contenere tutti i file copiati.
            int files = Directory.EnumerateFiles(temp, "*", SearchOption.AllDirectories).Count();

            using (ZipArchive archive = ZipFile.OpenRead(tempZip))
            {
                if (archive.Entries.Count(e => e.Name != "") != files)
                    throw new IOException("Lo zip non contiene tutti i file.");

                // Lo stesso zip deve funzionare su Windows e su Linux: percorsi con /.
                if (archive.Entries.Any(e => e.FullName.Contains('\\')))
                    throw new IOException("Percorsi con \\ nello zip.");
            }

            File.Move(tempZip, zipPath, overwrite: true);
        }
        catch (Exception ex)
        {
            TryDeleteFile(tempZip);
            return Result.Fail(Loc.F(Strings.ExportFailed, ex.Message));
        }
        finally
        {
            TryDeleteFolder(temp);
        }

        return Result.Ok(Loc.F(Strings.Exported, ProfileDisplayName(profileName), Path.GetFileName(zipPath)));
    }

    /// <summary>
    /// Aggiunge una partita da un file zip creato da ExportProfile. Se il nome
    /// è già usato ne prende uno libero. Il valore è il nome della cartella.
    /// </summary>
    public Result<string> ImportProfile(string zipPath, string ownerId)
    {
        Result<string> result = OwnerIsKnown(ownerId)
            ? ImportProfileCore(zipPath, ownerId)
            : Result<string>.Fail(Strings.PlayerNotFound);

        if (result.Success)
            RepairFavourites();
        Log.Write($"Importa {zipPath}: " + (result.Success ? result.Value : "ERRORE " + result.Message));
        return result;
    }

    private Result<string> ImportProfileCore(string zipPath, string ownerId)
    {
        string temp = Path.Combine(Path.GetTempPath(), "lss_importa_" + Guid.NewGuid().ToString("N"));

        try
        {
            // ExtractToDirectory rifiuta i percorsi che escono dalla cartella.
            ZipFile.ExtractToDirectory(zipPath, temp);

            string saves = Path.Combine(temp, ExportSavesFolder);

            if (!Directory.Exists(saves))
                return Result<string>.Fail(Strings.ImportInvalid);

            // V1Compat: gli zip fatti da 1.0 e dalle build prima della R10 hanno file .ini
            ExportInfoData exportInfo = JsonFile.Read<ExportInfoData>(Path.Combine(temp, ExportInfoFile))
                                        ?? V1Compat.ReadExportedInfo(temp);

            if (exportInfo.AppId != Game.Id || exportInfo.Provider != Game.Provider)
                return Result<string>.Fail(Loc.F(Strings.ImportOtherGame, exportInfo.Game, Game.Name));

            string name = UniqueName(exportInfo.Name.Trim());
            string profileName = ProfileFolderName(name);
            string profile = Path.Combine(ProfilesPath, profileName);
            IReadOnlyList<string> skipped = [];

            // V1Compat: posizioni.ini degli zip della 1.0
            ExportSpotsData? zipSpots = JsonFile.Read<ExportSpotsData>(Path.Combine(temp, ExportSpotsFile))
                                        ?? V1Compat.ReadExportedSpots(temp);

            if (zipSpots != null)
            {
                try
                {
                    skipped = ImportSpots(zipSpots, saves, profile);
                }
                catch
                {
                    // Il nome è nuovo (UniqueName): la cartella a metà si può togliere.
                    TryDeleteFolder(profile);
                    throw;
                }
            }
            else
            {
                FileOps.CopyFolderVerified(saves, profile);
            }

            string savestates = Path.Combine(temp, ExportSavestatesFolder);

            if (Directory.Exists(savestates))
                FileOps.CopyFolderVerified(savestates, Path.Combine(SavestatePath, profileName));

            // Il proprietario è quello scelto qui, non il Giocatore dello zip.
            PlaythroughInfo info = ParseInfo(exportInfo.Info) with { Owner = ownerId };
            JsonFile.Write(InfoFile(profileName), FormatInfo(info));

            if (skipped.Count == 0)
                return Result<string>.Ok(profileName);

            Log.Write("Posti non importati: " + string.Join(", ", skipped));

            return new Result<string>(profileName, Loc.F(Strings.ImportSpotsSkipped, string.Join(", ", skipped)));
        }
        catch (InvalidDataException)
        {
            return Result<string>.Fail(Strings.ImportInvalid);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail(Loc.F(Strings.ImportFailed, ex.Message));
        }
        finally
        {
            TryDeleteFolder(temp);
        }
    }

    /// <summary>
    /// Copia nel profilo i posti dello zip che questo gioco ha, ognuno con il suo id di qui.
    /// Il primo dello zip va al primo di qui; gli altri si abbinano per percorso portabile,
    /// maschera e ricorsione. Ritorna i percorsi dei posti che non si abbinano. Senza il
    /// primo posto l'importazione non si fa: un profilo senza cartella principale, cambiato
    /// nel gioco, svuoterebbe la cartella principale.
    /// </summary>
    private List<string> ImportSpots(ExportSpotsData zipSpots, string saves, string profile)
    {
        string zipExtra = Path.Combine(saves, FileOps.ExtraSpotsFolder);
        var skipped = new List<string>();
        bool primaryCopied = false;

        foreach (ExportSpotEntry entry in zipSpots.Spots)
        {
            int zipId = entry.Id;
            string portable = entry.Portable;
            string pattern = entry.Pattern;
            bool recursive = entry.Recursive;
            string source = zipId == 1 ? saves : Path.Combine(zipExtra, zipId.ToString(CultureInfo.InvariantCulture));

            SaveSpot? mine = zipId == 1
                ? Game.Spots.FirstOrDefault(spot => spot.Id == 1)
                : Game.Spots.FirstOrDefault(spot => spot.Id != 1 && SamePortable(spot.PortablePath, portable)
                                                    && spot.Pattern == pattern && spot.Recursive == recursive);

            if (mine is null || !Directory.Exists(source))
            {
                skipped.Add(portable);
                continue;
            }

            string destination = mine.Id == 1
                ? profile
                : Path.Combine(profile, FileOps.ExtraSpotsFolder, mine.Id.ToString(CultureInfo.InvariantCulture));

            FileOps.CopyWithoutExtraSpotsVerified(source, destination);
            primaryCopied |= mine.Id == 1;
        }

        if (!primaryCopied)
            throw new InvalidDataException("nessun posto principale");

        return skipped;
    }

    private static bool SamePortable(string first, string second) =>
        string.Equals(first, second, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // File temporaneo rimasto: non dà fastidio.
        }
    }

    // AVVIO DEL GIOCO

    /// <summary>
    /// Avvia il gioco tramite Steam (che si apre da solo se è chiuso).
    /// </summary>
    public Result LaunchGame() => provider.Launch(Game);

    /// <summary>
    /// Quando è partito il gioco in esecuzione, se si riesce a leggerlo.
    /// </summary>
    public DateTime? GameStartTime() => provider.GameStartTime(Game);
}
