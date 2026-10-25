using System.Globalization;
using System.IO.Compression;

namespace Lss;

/// <summary>
/// Operazione che ha creato un backup: scritta nel file .ini accanto al
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
}

/// <summary>
/// Informazioni di una partita che non stanno nei salvataggi.
/// </summary>
/// <param name="Note">Nota libera dell'utente, anche vuota.</param>
/// <param name="PlayTime">Tempo di gioco misurato dall'app.</param>
/// <param name="LastSession">Fine dell'ultima sessione di gioco misurata, se c'è.</param>
public sealed record PlaythroughInfo(string Note, TimeSpan PlayTime, DateTime? LastSession)
{
    public static readonly PlaythroughInfo Empty = new("", TimeSpan.Zero, null);
}

/// <summary>
/// Partite: informazioni, proprietario dei backup, opzioni condivise,
/// esportazione, importazione, recupero e avvio del gioco.
/// </summary>
public sealed partial class SaveManager
{
    public const int MaxNoteLength = 100;

    // Formato delle date nei file .ini.
    private const string InfoDateFormat = "yyyy-MM-dd HH:mm:ss";

    // File dentro lo zip di una partita esportata.
    private const string ExportInfoFile = "partita.ini";
    private const string ExportSavesFolder = "salvataggi";
    private const string ExportSavestatesFolder = "punti";

    /// <summary>
    /// Cartella con un file .ini per partita (nota, tempo di gioco). Sta fuori
    /// dalla cartella della partita, che viene copiata così com'è nel gioco.
    /// </summary>
    public string InfoPath { get; }

    // INFORMAZIONI DELLA PARTITA

    private string InfoFile(string profileName) => Path.Combine(InfoPath, profileName + ".ini");

    public PlaythroughInfo GetInfo(string profileName)
    {
        return ParseInfo(IniFile.Read(InfoFile(profileName)));
    }

    private static PlaythroughInfo ParseInfo(Dictionary<string, string> values)
    {
        string note = values.GetValueOrDefault("Nota", "");

        TimeSpan playTime = long.TryParse(values.GetValueOrDefault("TempoDiGioco"), out long seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.Zero;

        DateTime? last = DateTime.TryParseExact(values.GetValueOrDefault("UltimaSessione"), InfoDateFormat,
            CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)
            ? date
            : null;

        return new PlaythroughInfo(note, playTime, last);
    }

    private static IEnumerable<KeyValuePair<string, string>> FormatInfo(PlaythroughInfo info)
    {
        yield return new("Nota", info.Note);
        yield return new("TempoDiGioco", ((long)info.PlayTime.TotalSeconds).ToString(CultureInfo.InvariantCulture));
        yield return new("UltimaSessione",
            info.LastSession?.ToString(InfoDateFormat, CultureInfo.InvariantCulture) ?? "");
    }

    public Result SetNote(string profileName, string note)
    {
        note = note.Trim().Replace("\r", "").Replace("\n", " ");

        if (note.Length > MaxNoteLength)
            return Result.Fail(Loc.F(Strings.NoteTooLong, MaxNoteLength));

        try
        {
            IniFile.Write(InfoFile(profileName), FormatInfo(GetInfo(profileName) with { Note = note }));
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

            IniFile.Write(InfoFile(profileName), FormatInfo(info with
            {
                PlayTime = info.PlayTime + (end - start),
                LastSession = end
            }));

            Log.Write($"Sessione di gioco in {profileName}: {(int)(end - start).TotalMinutes} min");
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
            var values = new List<KeyValuePair<string, string>>
            {
                new("Partita", profileName),
                new("Motivo", BackupKind.Delete),
                new("Dettaglio", "")
            };
            values.AddRange(FormatInfo(info));

            IniFile.Write(BackupInfoFile(backup), values);

            if (File.Exists(InfoFile(profileName)))
                File.Delete(InfoFile(profileName));
        }
        catch (Exception ex)
        {
            Log.Write("Informazioni di " + profileName + " non conservate", ex);
        }
    }

    // BACKUP: PROPRIETARIO E DESCRIZIONE

    private static string BackupInfoFile(string backup) => backup + ".ini";

    /// <summary>
    /// Scrive accanto al backup a quale partita appartiene il contenuto e
    /// quale operazione l'ha creato. Non lancia eccezioni: senza il file il
    /// backup resta valido, solo meno descritto.
    /// </summary>
    private static void WriteBackupInfo(string backup, string owner, string kind, string detail)
    {
        try
        {
            IniFile.Write(BackupInfoFile(backup), new KeyValuePair<string, string>[]
            {
                new("Partita", owner),
                new("Motivo", kind),
                new("Dettaglio", detail)
            });
        }
        catch (Exception ex)
        {
            Log.Write("Informazioni del backup " + backup, ex);
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
            // Un .ini rimasto senza backup non dà fastidio.
        }
    }

    /// <summary>
    /// Completa un backup con il proprietario e l'operazione: dal suo file .ini,
    /// o dal nome della cartella per i backup delle versioni precedenti.
    /// </summary>
    private static Snapshot WithBackupInfo(Snapshot backup)
    {
        Dictionary<string, string> values = IniFile.Read(BackupInfoFile(backup.Path));

        if (values.TryGetValue("Motivo", out string? kind))
        {
            return backup with
            {
                Owner = values.GetValueOrDefault("Partita", ""),
                Kind = kind,
                Detail = values.GetValueOrDefault("Dettaglio", "")
            };
        }

        string reason = backup.Name;

        if (reason.StartsWith(ActivateBackupPrefix))
            return backup with { Kind = BackupKind.Activate, Detail = reason[ActivateBackupPrefix.Length..] };

        if (reason.StartsWith(SaveBackupPrefix))
            return backup with { Kind = BackupKind.Save, Owner = reason[SaveBackupPrefix.Length..] };

        if (reason.StartsWith(DeletedBackupPrefix))
            return backup with { Kind = BackupKind.Delete, Owner = reason[DeletedBackupPrefix.Length..] };

        return reason switch
        {
            "Prima_salvataggio" => backup with { Kind = BackupKind.Save },
            "Prima_creazione" => backup with { Kind = BackupKind.Create },
            RestoreBackupName => backup with { Kind = BackupKind.Restore },
            _ => backup
        };
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

    /// <summary>
    /// Backup di cui non si sa a quale partita appartengano (tranne le
    /// partite eliminate): quelli delle versioni precedenti, senza file .ini, e
    /// quelli fatti quando nessuna partita era nel gioco.
    /// </summary>
    public IReadOnlyList<Snapshot> GetBackupsWithoutOwner()
    {
        return GetBackups()
            .Where(b => b.Owner == "" && b.Kind != BackupKind.Delete)
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
    public Result RecoverDeletedProfile(Snapshot deleted)
    {
        return Finish("Recupera " + deleted.Owner, RecoverDeletedProfileCore(deleted));
    }

    private Result RecoverDeletedProfileCore(Snapshot deleted)
    {
        if (deleted.Kind != BackupKind.Delete || !IsInside(deleted.Path, BackupPath) || !Directory.Exists(deleted.Path))
            return Result.Fail(Loc.F(Strings.FolderNotFound, deleted.Path));

        string name = UniqueName(ProfileDisplayName(deleted.Owner));
        string profileName = ProfileFolderName(name);

        PlaythroughInfo info = ParseInfo(IniFile.Read(BackupInfoFile(deleted.Path)));

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
            if (info != PlaythroughInfo.Empty)
                IniFile.Write(InfoFile(profileName), FormatInfo(info));
        }
        catch (Exception ex)
        {
            Log.Write("Informazioni di " + profileName + " non recuperate", ex);
        }

        return Result.Ok(Loc.F(Strings.Recovered, name));
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
    public Result AdoptGameSaves(string name)
    {
        Result created = CreateProfile(name, copyCurrentSaves: true);

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

    // OPZIONI CONDIVISE

    /// <summary>
    /// File da tenere uguali in tutte le partite, relativi a SavePath.
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

            var values = new List<KeyValuePair<string, string>>
            {
                new("Formato", "3"),
                new("Provider", Game.Provider),
                new("AppId", Game.Id),
                new("Gioco", Game.Name),
                new("Nome", ProfileDisplayName(profileName))
            };
            values.AddRange(FormatInfo(GetInfo(profileName)));
            IniFile.Write(Path.Combine(temp, ExportInfoFile), values);

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
    public Result<string> ImportProfile(string zipPath)
    {
        Result<string> result = ImportProfileCore(zipPath);
        Log.Write($"Importa {zipPath}: " + (result.Success ? result.Value : "ERRORE " + result.Message));
        return result;
    }

    private Result<string> ImportProfileCore(string zipPath)
    {
        string temp = Path.Combine(Path.GetTempPath(), "lss_importa_" + Guid.NewGuid().ToString("N"));

        try
        {
            // ExtractToDirectory rifiuta i percorsi che escono dalla cartella.
            ZipFile.ExtractToDirectory(zipPath, temp);

            string saves = Path.Combine(temp, ExportSavesFolder);

            if (!Directory.Exists(saves))
                return Result<string>.Fail(Strings.ImportInvalid);

            Dictionary<string, string> values = IniFile.Read(Path.Combine(temp, ExportInfoFile));

            (string appId, string gameName) = LegacyMigration.ExportedGame(values);

            // Formato 1 e 2 non hanno il launcher: sono di Steam.
            string provider = values.GetValueOrDefault("Provider", "steam");

            if (appId != Game.Id || provider != Game.Provider)
                return Result<string>.Fail(Loc.F(Strings.ImportOtherGame, gameName, Game.Name));

            string name = UniqueName(values.GetValueOrDefault("Nome", "").Trim());
            string profileName = ProfileFolderName(name);

            FileOps.CopyFolderVerified(saves, Path.Combine(ProfilesPath, profileName));

            string savestates = Path.Combine(temp, ExportSavestatesFolder);

            if (Directory.Exists(savestates))
                FileOps.CopyFolderVerified(savestates, Path.Combine(SavestatePath, profileName));

            PlaythroughInfo info = ParseInfo(values);

            if (info != PlaythroughInfo.Empty)
                IniFile.Write(InfoFile(profileName), FormatInfo(info));

            return Result<string>.Ok(profileName);
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
