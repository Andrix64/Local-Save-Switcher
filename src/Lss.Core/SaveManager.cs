using System.Globalization;
using System.Text.RegularExpressions;

namespace Lss;

/// <summary>
/// Copia dei salvataggi in una cartella con data: un backup o un savestate.
/// </summary>
/// <param name="Path">Cartella della copia.</param>
/// <param name="Date">Data di creazione, letta dal nome della cartella.</param>
/// <param name="Name">Nome del savestate (anche vuoto) o motivo del backup.</param>
/// <param name="Owner">Solo backup: partita a cui appartiene il contenuto, o "" se non si sa.</param>
/// <param name="Kind">Solo backup: operazione che l'ha creato (vedi BackupKind), o "".</param>
/// <param name="Detail">Solo backup: dettaglio dell'operazione (partita o punto di arrivo).</param>
public sealed record Snapshot(string Path, DateTime Date, string Name,
    string Owner = "", string Kind = "", string Detail = "");

/// <summary>
/// Stato di Steam Cloud per Control.
/// </summary>
public enum CloudState
{
    /// <summary>Disattivato nelle proprietà del gioco: Steam non tocca i salvataggi.</summary>
    Disabled,

    /// <summary>Attivo (o non disattivato): Steam può risincronizzare i salvataggi.</summary>
    Enabled,

    /// <summary>Impostazione non leggibile.</summary>
    Unknown
}

/// <summary>
/// Logica dei profili di salvataggio, senza input/output di console.
/// </summary>
public sealed partial class SaveManager
{
    private const string ProfilePrefix = "Profilo_";
    private const string DateFormat = "dd/MM/yyyy HH:mm:ss";
    private const string BackupDateFormat = "yyyy-MM-dd_HH-mm-ss-fff";
    private const string SavestateDateFormat = "yyyy-MM-dd_HH-mm-ss";

    // Prefissi dei nomi dei backup, letti da BackupReasonText.
    private const string ActivateBackupPrefix = "Prima_di_";
    private const string SaveBackupPrefix = "Prima_salvataggio_";
    private const string RestoreBackupName = "Prima_ripristino";
    private const string DeletedBackupPrefix = "Eliminato_";

    // Nome intermedio di una rinomina solo di maiuscole.
    private const string RenameSuffix = ".rinomina";

    public string SavePath { get; }
    public GameInfo Game { get; }
    public string ProfilesPath { get; }
    public string BackupPath { get; }
    public string SavestatePath { get; }
    public AppState State { get; }

    /// <summary>
    /// Nome dell'account Steam in uso, o "" se non è noto.
    /// </summary>
    public string AccountName { get; }

    /// <summary>
    /// Account del gioco: la cartella sopra SavePath.
    /// </summary>
    public GameAccount Account { get; }

    /// <summary>
    /// Nome del launcher che gestisce il gioco (marchio: Steam, Epic...).
    /// </summary>
    public string LauncherName => provider.DisplayName;

    /// <summary>
    /// Launcher che gestisce il gioco (Steam).
    /// </summary>
    private readonly IGameProvider provider;

    /// <summary>
    /// I posti dei salvataggi del gioco: si cambiano e si salvano tutti insieme.
    /// SavePath è il posto 1.
    /// </summary>
    private readonly SaveSet saves;

    // Journal del cambio di salvataggi, nella cartella dati del gioco.
    private readonly string switchJournal;

    /// <summary>
    /// true se all'apertura è stato rimesso a posto un cambio di salvataggi
    /// interrotto (per esempio dallo spegnimento del computer).
    /// </summary>
    public bool SwitchInterruptedRestored { get; private set; }

    /// <param name="savePath">Cartella dei salvataggi del gioco (…\870780).</param>
    /// <param name="basePath">Cartella dei dati del gioco per un account (…\LSS\IDAccount\AppID).</param>
    /// <param name="game">Gioco gestito; se manca è Control con savePath.</param>
    /// <param name="provider">Launcher del gioco; se manca è Steam.</param>
    /// <param name="account">Account del gioco; se manca si ricava da savePath (solo Control, in userdata).</param>
    internal SaveManager(string savePath, string basePath, string accountName = "", GameInfo? game = null,
        IGameProvider? provider = null, GameAccount? account = null)
    {
        Game = game ?? GameInfo.Control(savePath);
        this.provider = provider ?? new SteamProvider(Platform.Current);

        // Una cartella dei salvataggi fuori da userdata non sta sotto l'account: va preso quello dato.
        string accountFolder = Path.GetDirectoryName(savePath) ?? "";
        Account = account ?? new GameAccount(this.provider.Id, Path.GetFileName(accountFolder), accountName, accountFolder, null);
        SavePath = savePath;
        saves = new SaveSet(Game.Spots.Count > 0 ? Game.Spots : GameInfo.SpotsOf(savePath, SaveSource.Userdata));
        switchJournal = Path.Combine(basePath, "cambio.ini");
        AccountName = accountName;
        ProfilesPath = Path.Combine(basePath, "Profili");
        BackupPath = Path.Combine(basePath, "Backup");
        SavestatePath = Path.Combine(basePath, "Savestate");
        InfoPath = Path.Combine(basePath, "Info");
        State = new AppState(Path.Combine(basePath, "stato.ini"));

        Directory.CreateDirectory(ProfilesPath);
        Directory.CreateDirectory(BackupPath);

        // Profili e savestate lasciati a metà da un'operazione interrotta.
        RecoverInterrupted(ProfilesPath);
        RecoverInterrupted(SavestatePath);

        State.Load();
    }

    // INIZIALIZZAZIONE

    /// <param name="chooseAccount">
    /// Chiamata solo se ci sono più account con i salvataggi di Control: riceve
    /// gli account e l'ID dell'ultimo usato (o null) e restituisce quello scelto,
    /// o null se l'utente annulla. In quel caso il messaggio del risultato è vuoto.
    /// </param>
    public static Result<SaveManager> Initialize(
        Func<IReadOnlyList<GameAccount>, string?, GameAccount?> chooseAccount)
    {
        var steamProvider = new SteamProvider(Platform.Current);
        string steam = steamProvider.FindRoot() ?? "";

        if (steam == "")
            return Result<SaveManager>.Fail(Strings.SteamNotFound);

        string userData = SteamProvider.UserDataFolder(steam);

        if (!Directory.Exists(userData))
            return Result<SaveManager>.Fail(Strings.UserDataNotFound);

        // Prima di cercare gli account: una cartella del gioco rimasta come
        // 870780.vecchio farebbe sparire l'account.
        RecoverGameFolders(userData, GameInfo.ControlAppId);

        IReadOnlyList<GameAccount> accounts = SteamProvider.FindAccounts(userData, GameInfo.ControlAppId);

        if (accounts.Count == 0)
        {
            return Result<SaveManager>.Fail(
                Loc.F(Strings.SavesNotFound, "Control"));
        }

        string appData = AppData.Root;
        var settings = new AppSettings(Path.Combine(appData, "lss.ini"));
        settings.Load();

        GameAccount? account = accounts[0];

        if (accounts.Count > 1)
        {
            account = chooseAccount(accounts, settings.LastAccount == "" ? null : settings.LastAccount);

            if (account == null)
                return Result<SaveManager>.Fail("");

            settings.LastAccount = account.Key;
            settings.Save();
        }

        try
        {
            string savePath = Path.Combine(account.Folder, GameInfo.ControlAppId);

            // Dalle librerie di Steam, se Control è installato: con la cartella
            // di installazione il gioco aperto si riconosce anche dai processi.
            GameInfo game = steamProvider.FindGames(account)
                .FirstOrDefault(g => g.Id == GameInfo.ControlAppId) ?? GameInfo.Control(savePath);

            return Result<SaveManager>.Ok(new SaveManager(
                savePath,
                Path.Combine(appData, account.Provider, account.Id, GameInfo.ControlAppId),
                account.Name,
                game,
                new SteamProvider(Platform.Current),
                account));
        }
        catch (Exception ex)
        {
            return Result<SaveManager>.Fail(Loc.F(Strings.InitError, ex.Message));
        }
    }

    /// <summary>
    /// Manager per un gioco di un account: dati in dataRoot/IDAccount/AppID.
    /// </summary>
    public static Result<SaveManager> Open(string dataRoot, GameAccount account, GameInfo game, IGameProvider? provider = null)
    {
        if (game.Support != SupportState.Supported)
            return Result<SaveManager>.Fail(Loc.F(Strings.GameNotSupported, game.Name));

        try
        {
            if (game.Source != SaveSource.Userdata)
            {
                RecoverSaveFolder(game.SaveFolder);

                // Il controllo si ripete ad ogni apertura: la cartella può essere cambiata da quando è stata trovata.
                IGameProvider launcher = provider ?? new SteamProvider(Platform.Current);
                SafetyProblem problem = SaveFolderSafety.Check(
                    game.SaveFolder,
                    SaveLookup.ContextFor(launcher, account, game, Platform.Current.GetKnownFolders()),
                    launcher.LauncherFolders(account),
                    dataRoot);

                if (problem != SafetyProblem.None)
                {
                    return Result<SaveManager>.Fail(Loc.F(Strings.SaveFolderNoLongerSafe, game.Name,
                        SaveFolderSafety.Describe(problem)));
                }
            }

            var manager = new SaveManager(
                game.SaveFolder,
                Path.Combine(dataRoot, account.Provider, account.Id, game.Id),
                account.Name,
                game,
                provider,
                account);

            manager.RecoverInterruptedSwitch();

            return Result<SaveManager>.Ok(manager);
        }
        catch (Exception ex)
        {
            return Result<SaveManager>.Fail(Loc.F(Strings.InitError, ex.Message));
        }
    }

    /// <summary>
    /// Rimette a posto un cambio di salvataggi interrotto, se cambio.ini c'è,
    /// e segna che è successo (l'interfaccia lo dice una volta).
    /// </summary>
    private void RecoverInterruptedSwitch()
    {
        SwitchInterruptedRestored = saves.RecoverInterrupted(switchJournal, null);
    }

    /// <summary>
    /// Rimette a posto una cartella dei salvataggi lasciata a metà da una
    /// sostituzione interrotta. Un errore si registra e basta.
    /// </summary>
    private static void RecoverSaveFolder(string saveFolder)
    {
        try
        {
            if (FileOps.RecoverInterruptedReplace(saveFolder))
                Log.Write("Recuperata una sostituzione interrotta: " + saveFolder);
        }
        catch (Exception ex)
        {
            Log.Write("Recupero di " + saveFolder, ex);
        }
    }

    /// <summary>
    /// Rimette a posto la cartella del gioco di ogni account dopo una
    /// sostituzione interrotta (vedi FileOps.RecoverInterruptedReplace).
    /// </summary>
    internal static void RecoverGameFolders(string userData, string appId)
    {
        foreach (string dir in Directory.GetDirectories(userData))
        {
            string game = Path.Combine(dir, appId);

            if (!Directory.Exists(game + FileOps.OldSuffix) && !Directory.Exists(game + FileOps.TempSuffix))
                continue;

            try
            {
                if (FileOps.RecoverInterruptedReplace(game))
                    Log.Write("Recuperata una sostituzione interrotta: " + game);
            }
            catch (Exception ex)
            {
                Log.Write("Recupero di " + game, ex);
            }
        }
    }

    /// <summary>
    /// Rimette a posto le cartelle dei giochi di ogni account lasciate a metà
    /// da una sostituzione interrotta, per qualsiasi gioco (non solo Control).
    /// </summary>
    internal static void RecoverAllGameFolders(string userData)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string account in Directory.GetDirectories(userData).Where(a => IsNumeric(Path.GetFileName(a))))
        {
            foreach (string sub in Directory.GetDirectories(account))
            {
                string name = Path.GetFileName(sub);

                foreach (string suffix in new[] { FileOps.OldSuffix, FileOps.TempSuffix })
                {
                    if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
                        IsNumeric(name[..^suffix.Length]))
                    {
                        targets.Add(Path.Combine(account, name[..^suffix.Length]));
                    }
                }
            }
        }

        foreach (string target in targets)
        {
            try
            {
                if (FileOps.RecoverInterruptedReplace(target))
                    Log.Write("Recuperata una sostituzione interrotta: " + target);
            }
            catch (Exception ex)
            {
                Log.Write("Recupero di " + target, ex);
            }
        }
    }

    /// <summary>
    /// Rimette a posto le cartelle di root (profili o savestate) lasciate a
    /// metà da una sostituzione o da una rinomina interrotta.
    /// </summary>
    private static void RecoverInterrupted(string root)
    {
        if (!Directory.Exists(root))
            return;

        foreach (string dir in Directory.GetDirectories(root))
        {
            try
            {
                if (dir.EndsWith(RenameSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    // Rinomina solo di maiuscole interrotta tra i due spostamenti.
                    string target = dir[..^RenameSuffix.Length];

                    if (!Directory.Exists(target))
                    {
                        Directory.Move(dir, target);
                        Log.Write("Recuperata una rinomina interrotta: " + target);
                    }
                }
                else if (dir.EndsWith(FileOps.OldSuffix, StringComparison.OrdinalIgnoreCase) ||
                         dir.EndsWith(FileOps.TempSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    string target = dir[..dir.LastIndexOf('.')];

                    if (FileOps.RecoverInterruptedReplace(target))
                        Log.Write("Recuperata una sostituzione interrotta: " + target);
                }
            }
            catch (Exception ex)
            {
                Log.Write("Recupero di " + dir, ex);
            }
        }
    }

    /// <summary>
    /// true se il nome è fatto solo di cifre, come le cartelle degli account e dei giochi.
    /// </summary>
    internal static bool IsNumeric(string name) => name.Length > 0 && name.All(char.IsAsciiDigit);

    public void ReloadState()
    {
        State.Load();
    }

    /// <summary>
    /// Data salvata in stato.ini mostrata nel formato della lingua di Windows.
    /// In stato.ini è sempre gg/MM/aaaa; un valore illeggibile viene mostrato com'è.
    /// </summary>
    public static string FormatStoredDate(string stored)
    {
        return DateTime.TryParseExact(stored, DateFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out DateTime date)
            ? date.ToString("G", CultureInfo.CurrentCulture)
            : stored;
    }

    // STEAM CLOUD

    /// <summary>
    /// Stato di Steam Cloud per Control, letto dall'impostazione del gioco in
    /// userdata\IDAccount\7\remote\sharedconfig.vdf. Steam lo tiene attivo se
    /// non è disattivato esplicitamente, quindi senza impostazione vale Enabled.
    /// L'impostazione generale "Abilita Steam Cloud" non viene letta.
    /// </summary>
    public CloudState GetCloudState() => provider.GetCloudState(Account, Game);

    // PROFILI

    /// <summary>
    /// Nomi delle cartelle dei profili validi (es. "Profilo_Mario").
    /// </summary>
    public IReadOnlyList<string> GetProfiles()
    {
        return Directory.GetDirectories(ProfilesPath)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(name =>
                name.StartsWith(ProfilePrefix) &&
                IsValidName(name[ProfilePrefix.Length..]))
            .OrderBy(name => name)
            .ToList();
    }

    /// <summary>
    /// Cartelle in Profili che non sono profili validi e quindi non compaiono
    /// nell'elenco (nome senza "Profilo_", con spazi, accenti...).
    /// </summary>
    public IReadOnlyList<string> GetIgnoredProfileFolders()
    {
        IReadOnlyList<string> profiles = GetProfiles();

        return Directory.GetDirectories(ProfilesPath)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(name => !profiles.Contains(name))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string ProfileFolderName(string name)
    {
        return ProfilePrefix + name;
    }

    /// <summary>
    /// Nome da mostrare all'utente: "Profilo_Mario" diventa "Mario".
    /// </summary>
    public static string ProfileDisplayName(string profileName)
    {
        return profileName.StartsWith(ProfilePrefix)
            ? profileName[ProfilePrefix.Length..]
            : profileName;
    }

    public bool ProfileExists(string profileName)
    {
        // Senza distinzione di maiuscole su ogni sistema: su Linux "Profilo_mario"
        // e "Profilo_Mario" sono cartelle diverse, ma per LSS sono la stessa partita.
        return Directory.GetDirectories(ProfilesPath)
            .Select(Path.GetFileName)
            .Any(name => string.Equals(name, profileName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Data del file più recente nel profilo, o null se il profilo è vuoto.
    /// </summary>
    public DateTime? GetProfileLastModified(string profileName)
    {
        DateTime? latest = null;

        try
        {
            foreach (string file in Directory.EnumerateFiles(
                         Path.Combine(ProfilesPath, profileName), "*", SearchOption.AllDirectories))
            {
                DateTime time = File.GetLastWriteTime(file);

                if (latest == null || time > latest)
                    latest = time;
            }
        }
        catch
        {
            // Profilo non leggibile: nessuna data.
        }

        return latest;
    }

    public bool HasActiveProfile =>
        State.ActiveProfile != "" && ProfileExists(State.ActiveProfile);

    /// <summary>
    /// true se nella cartella del gioco non c'è nessun file di salvataggio.
    /// </summary>
    public bool GameSavesAreEmpty()
    {
        return saves.GameIsEmpty();
    }

    /// <summary>
    /// true se i salvataggi del gioco sono diversi dal profilo attivo, cioè se
    /// cambiando profilo i progressi finirebbero solo nel backup.
    /// </summary>
    public bool HasUnsavedProgress()
    {
        if (!HasActiveProfile || GameSavesAreEmpty())
            return false;

        try
        {
            return !saves.GameEquals(Path.Combine(ProfilesPath, State.ActiveProfile));
        }
        catch
        {
            // Nel dubbio meglio chiedere all'utente.
            return true;
        }
    }

    /// <summary>
    /// Crea un profilo. Non lo attiva: per farlo chiamare ActivateProfile.
    /// </summary>
    public Result CreateProfile(string name, bool copyCurrentSaves)
    {
        string operation = "Crea profilo " + ProfileFolderName(name) +
                           (copyCurrentSaves ? " (copia dei salvataggi)" : " (vuoto)");

        return Finish(operation, CreateProfileCore(name, copyCurrentSaves));
    }

    private Result CreateProfileCore(string name, bool copyCurrentSaves)
    {
        if (!IsValidName(name))
            return Result.Fail(Strings.InvalidName);

        string profileName = ProfileFolderName(name);
        string profile = Path.Combine(ProfilesPath, profileName);

        if (ProfileExists(profileName))
            return Result.Fail(Strings.ProfileExists);

        if (!copyCurrentSaves)
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(profile, "remote"));
                return Result.Ok(Strings.EmptyProfileCreated);
            }
            catch (Exception ex)
            {
                TryDeleteFolder(profile);
                return Result.Fail(Loc.F(Strings.ErrorWithMessage, ex.Message));
            }
        }

        // Copiando mentre il gioco scrive si otterrebbe un profilo incoerente.
        if (IsGameRunning())
            return Result.Fail(Loc.F(Strings.CloseGameFirst, Game.Name));

        string backup = NewBackupPath("Prima_creazione");

        try
        {
            saves.CopyGameTo(backup);
            // Senza partita nel gioco, i salvataggi copiati diventano proprio questa partita.
            WriteBackupInfo(backup, HasActiveProfile ? State.ActiveProfile : profileName, BackupKind.Create, profileName);
        }
        catch (Exception ex)
        {
            return Result.Fail(Loc.F(Strings.BackupNotDone, ex.Message));
        }

        try
        {
            saves.CopyGameTo(profile);
        }
        catch (Exception ex)
        {
            // Un profilo copiato a metà comparirebbe nella lista e bloccherebbe il nome.
            TryDeleteFolder(profile);
            return Result.Fail(Loc.F(Strings.CopyFailed, ex.Message), backup);
        }

        return Result.Ok(Strings.ProfileCreated, backup);
    }

    /// <summary>
    /// Rinomina un profilo e la cartella dei suoi savestate. Se è il profilo
    /// attivo, aggiorna anche lo stato.
    /// </summary>
    public Result RenameProfile(string profileName, string newName)
    {
        return Log.Write($"Rinomina {profileName} in {ProfileFolderName(newName)}",
            RenameProfileCore(profileName, newName));
    }

    private Result RenameProfileCore(string profileName, string newName)
    {
        if (!IsValidName(newName))
            return Result.Fail(Strings.InvalidName);

        string newProfileName = ProfileFolderName(newName);

        if (newProfileName == profileName)
            return Result.Ok(Strings.NameUnchanged);

        string source = Path.Combine(ProfilesPath, profileName);
        string destination = Path.Combine(ProfilesPath, newProfileName);

        if (!Directory.Exists(source))
            return Result.Fail(Strings.ProfileNotFound);

        // Su Windows "Mario" e "mario" sono la stessa cartella.
        bool caseOnly = string.Equals(profileName, newProfileName, StringComparison.OrdinalIgnoreCase);

        if (!caseOnly && ProfileExists(newProfileName))
            return Result.Fail(Strings.ProfileExists);

        try
        {
            MoveFolder(source, destination, caseOnly);
        }
        catch (Exception ex)
        {
            return Result.Fail(Loc.F(Strings.ProfileNotRenamed, ex.Message));
        }

        string savestates = Path.Combine(SavestatePath, profileName);
        string savestateError = "";

        if (Directory.Exists(savestates))
        {
            try
            {
                MoveFolder(savestates, Path.Combine(SavestatePath, newProfileName), caseOnly);
            }
            catch (Exception ex)
            {
                savestateError = Loc.F(Strings.SavestatesNotMoved, ex.Message, savestates);
            }
        }

        MoveInfo(profileName, newProfileName, caseOnly);

        if (State.ActiveProfile == profileName)
        {
            State.ActiveProfile = newProfileName;

            try
            {
                State.Save();
            }
            catch (Exception ex)
            {
                return Result.Fail(
                    Loc.F(Strings.RenamedStateNotSaved, ex.Message) + savestateError);
            }
        }

        return savestateError == ""
            ? Result.Ok(Strings.ProfileRenamed)
            : Result.Fail(Strings.ProfileRenamed + savestateError);
    }

    private static void MoveFolder(string source, string destination, bool caseOnly)
    {
        if (!caseOnly)
        {
            Directory.Move(source, destination);
            return;
        }

        // Un cambio solo di maiuscole passa per un nome intermedio.
        string temp = destination + RenameSuffix;
        Directory.Move(source, temp);
        Directory.Move(temp, destination);
    }

    /// <summary>
    /// Elimina un profilo non attivo spostandolo nei backup, da cui si può
    /// ancora ripristinare. I suoi savestate vengono eliminati.
    /// </summary>
    public Result DeleteProfile(string profileName)
    {
        return Finish("Elimina " + profileName, DeleteProfileCore(profileName));
    }

    private Result DeleteProfileCore(string profileName)
    {
        if (profileName == State.ActiveProfile)
            return Result.Fail(Strings.CannotDeleteActive);

        string profile = Path.Combine(ProfilesPath, profileName);

        if (!Directory.Exists(profile))
            return Result.Fail(Strings.ProfileNotFound);

        string backup = NewBackupPath(DeletedBackupPrefix + profileName);

        try
        {
            Directory.Move(profile, backup);
        }
        catch (Exception ex)
        {
            return Result.Fail(Loc.F(Strings.ProfileNotDeleted, ex.Message));
        }

        // Nota e tempo di gioco seguono la partita nel backup, per il recupero.
        RecordDeletedInfo(profileName, backup);

        string savestates = Path.Combine(SavestatePath, profileName);

        try
        {
            if (Directory.Exists(savestates))
                Directory.Delete(savestates, true);
        }
        catch (Exception ex)
        {
            return Result.Fail(
                Loc.F(Strings.ProfileDeletedSavestatesNot, ex.Message, savestates),
                backup);
        }

        return Result.Ok(Strings.ProfileDeleted, backup);
    }

    /// <summary>
    /// Quanti savestate ha un profilo.
    /// </summary>
    public int CountSavestates(string profileName)
    {
        string folder = Path.Combine(SavestatePath, profileName);

        try
        {
            return Directory.Exists(folder) ? Directory.GetDirectories(folder).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static void TryDeleteFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
            // Se non si riesce a eliminarla, resta la cartella parziale.
        }
    }

    /// <summary>
    /// Sostituisce i salvataggi del gioco con quelli del profilo,
    /// dopo averne fatto un backup.
    /// </summary>
    public Result ActivateProfile(string profileName)
    {
        return Finish("Attiva " + profileName, ActivateProfileCore(profileName));
    }

    private Result ActivateProfileCore(string profileName)
    {
        if (IsGameRunning())
            return Result.Fail(Loc.F(Strings.CloseGameFirst, Game.Name));

        string profile = Path.Combine(ProfilesPath, profileName);

        if (!Directory.Exists(profile))
            return Result.Fail(Strings.ProfileNotFound);

        // Opzioni condivise: la partita prende le opzioni che il gioco ha adesso.
        if (State.SharePreferences)
            CopyPreferencesTo(profile);

        Result replaced = ReplaceGameSaves(profile, ActivateBackupPrefix + profileName,
            State.ActiveProfile, BackupKind.Activate, profileName);

        if (!replaced.Success)
            return replaced;

        string backup = replaced.BackupPath!;

        State.ActiveProfile = profileName;
        State.ActiveSince = DateTime.Now.ToString(DateFormat, CultureInfo.InvariantCulture);
        State.LastSaved = "";

        try
        {
            State.Save();
        }
        catch (Exception ex)
        {
            return Result.Fail(
                Loc.F(Strings.ActivatedStateNotSaved, ex.Message),
                backup);
        }

        return Result.Ok(Strings.ProfileActivated, backup);
    }

    // SALVATAGGIO DEI PROGRESSI

    /// <summary>
    /// Copia i salvataggi del gioco nel profilo attivo, dopo averne fatto un backup.
    /// </summary>
    /// <param name="allowEmptySaves">
    /// Se false, l'operazione viene rifiutata quando la cartella del gioco non
    /// contiene salvataggi, per non svuotare il profilo per errore.
    /// </param>
    /// <param name="onGameExit">true se è il salvataggio automatico alla chiusura del gioco.</param>
    public Result SaveProgress(bool allowEmptySaves = false, bool onGameExit = false)
    {
        return Finish((onGameExit ? "Salva alla chiusura in " : "Salva progressi in ") + State.ActiveProfile,
            SaveProgressCore(allowEmptySaves, onGameExit));
    }

    private Result SaveProgressCore(bool allowEmptySaves, bool onGameExit)
    {
        if (!HasActiveProfile)
            return Result.Fail(Strings.NoValidActiveProfile);

        if (IsGameRunning())
            return Result.Fail(Loc.F(Strings.CloseGameFirst, Game.Name));

        if (!allowEmptySaves && GameSavesAreEmpty())
        {
            return Result.Fail(Strings.GameSavesEmptyWouldClear);
        }

        string profile = Path.Combine(ProfilesPath, State.ActiveProfile);
        // Il nome dice di quale profilo è il backup.
        string backup = NewBackupPath(SaveBackupPrefix + State.ActiveProfile);

        try
        {
            FileOps.CopyFolderVerified(profile, backup);
            WriteBackupInfo(backup, State.ActiveProfile,
                onGameExit ? BackupKind.SaveOnExit : BackupKind.Save, "");
        }
        catch (Exception ex)
        {
            return Result.Fail(Loc.F(Strings.BackupFailedCancelled, ex.Message));
        }

        try
        {
            saves.ReplaceSnapshotFromGame(profile);
        }
        catch (Exception ex)
        {
            try
            {
                FileOps.ReplaceFolder(backup, profile, carryExtraSpots: true);

                return Result.Fail(
                    Loc.F(Strings.SaveFailedProfileRestored, ex.Message),
                    backup);
            }
            catch (Exception restoreEx)
            {
                return Result.Fail(
                    Loc.F(Strings.SaveFailedRestoreFailed, ex.Message, restoreEx.Message),
                    backup);
            }
        }

        State.LastSaved = DateTime.Now.ToString(DateFormat, CultureInfo.InvariantCulture);

        try
        {
            State.Save();
        }
        catch (Exception ex)
        {
            return Result.Fail(
                Loc.F(Strings.ProgressSavedStateNotSaved, ex.Message),
                backup);
        }

        return Result.Ok(Strings.ProgressSaved, backup);
    }

    /// <summary>
    /// Fa il backup dei salvataggi del gioco e li sostituisce con source.
    /// Se la sostituzione fallisce, ripristina il backup.
    /// </summary>
    /// <param name="owner">Partita a cui appartengono i salvataggi del gioco di adesso.</param>
    /// <param name="kind">Operazione, per la descrizione del backup (BackupKind).</param>
    /// <param name="detail">Dettaglio dell'operazione, per la descrizione.</param>
    private Result ReplaceGameSaves(string source, string backupName, string owner, string kind, string detail)
    {
        string backup = NewBackupPath(backupName);

        try
        {
            saves.CopyGameTo(backup);
            WriteBackupInfo(backup, owner, kind, detail);
        }
        catch (Exception ex)
        {
            return Result.Fail(Loc.F(Strings.BackupFailedCancelled, ex.Message));
        }

        try
        {
            saves.ReplaceGameFrom(source, backup, switchJournal);
        }
        catch (Exception ex)
        {
            try
            {
                saves.ReplaceGameFrom(backup, backup, switchJournal);

                return Result.Fail(
                    Loc.F(Strings.ReplaceFailedRestored, ex.Message),
                    backup);
            }
            catch (Exception restoreEx)
            {
                return Result.Fatal(
                    Loc.F(Strings.ReplaceFailedFatal, ex.Message, restoreEx.Message, backup),
                    backup);
            }
        }

        return Result.Ok("", backup);
    }

    /// <summary>
    /// Chiude un'operazione: elimina i backup in eccesso e scrive il registro.
    /// </summary>
    private Result Finish(string operation, Result result)
    {
        PruneBackups();
        return Log.Write(operation, result);
    }

    // SAVESTATE

    /// <summary>
    /// Cartella dei savestate del profilo attivo, o "" se non c'è un profilo attivo.
    /// </summary>
    public string ActiveSavestatePath => HasActiveProfile
        ? Path.Combine(SavestatePath, State.ActiveProfile)
        : "";

    /// <summary>
    /// Savestate del profilo attivo, dal più recente.
    /// </summary>
    public IReadOnlyList<Snapshot> GetSavestates()
    {
        string folder = ActiveSavestatePath;

        if (folder == "" || !Directory.Exists(folder))
            return [];

        var list = new List<Snapshot>();

        foreach (string dir in Directory.GetDirectories(folder))
        {
            string name = Path.GetFileName(dir);

            if (name.Length < SavestateDateFormat.Length ||
                !DateTime.TryParseExact(name[..SavestateDateFormat.Length], SavestateDateFormat,
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                continue;

            string label = name.Length > SavestateDateFormat.Length + 1
                ? name[(SavestateDateFormat.Length + 1)..]
                : "";

            list.Add(new Snapshot(dir, date, label));
        }

        return list.OrderByDescending(s => s.Date).ToList();
    }

    /// <summary>
    /// Nome facoltativo di un savestate: lettere, cifre, spazi, _ e -.
    /// </summary>
    public static bool IsValidSavestateName(string name)
    {
        if (name == "")
            return true;

        if (name.Length > 50 || name != name.Trim())
            return false;

        return name.All(c => char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-');
    }

    /// <summary>
    /// Copia i salvataggi attuali del gioco in un nuovo savestate del profilo
    /// attivo, senza toccare né il gioco né il profilo.
    /// </summary>
    public Result CreateSavestate(string name)
    {
        return Finish($"Crea savestate \"{name}\" di {State.ActiveProfile}",
            CreateSavestateCore(name.Trim()));
    }

    private Result CreateSavestateCore(string name)
    {
        if (!HasActiveProfile)
            return Result.Fail(Strings.NoValidActiveProfile);

        if (!IsValidSavestateName(name))
            return Result.Fail(Strings.InvalidName);

        if (IsGameRunning())
            return Result.Fail(Loc.F(Strings.CloseGameFirst, Game.Name));

        if (GameSavesAreEmpty())
            return Result.Fail(Strings.GameSavesEmpty);

        string folder = DateTime.Now.ToString(SavestateDateFormat) + (name == "" ? "" : "_" + name);
        string path = Path.Combine(ActiveSavestatePath, folder);

        if (Directory.Exists(path))
            return Result.Fail(Strings.SavestateExists);

        try
        {
            saves.CopyGameTo(path);
        }
        catch (Exception ex)
        {
            TryDeleteFolder(path);
            return Result.Fail(Loc.F(Strings.SavestateNotCreated, ex.Message));
        }

        return Result.Ok(name == "" ? Strings.SavestateCreated : Loc.F(Strings.SavestateCreatedNamed, name));
    }

    /// <summary>
    /// Rimette un savestate nella cartella del gioco, dopo un backup.
    /// Il profilo attivo non cambia.
    /// </summary>
    public Result RestoreSavestate(Snapshot savestate)
    {
        return Finish("Ripristina savestate " + savestate.Path,
            RestoreToGame(savestate, SavestatePath, Strings.SavestateRestored));
    }

    public Result DeleteSavestate(Snapshot savestate)
    {
        return Log.Write("Elimina savestate " + savestate.Path, DeleteSavestateCore(savestate));
    }

    private Result DeleteSavestateCore(Snapshot savestate)
    {
        if (!IsInside(savestate.Path, SavestatePath))
            return Result.Fail(Strings.SavestateInvalid);

        try
        {
            Directory.Delete(savestate.Path, true);
        }
        catch (Exception ex)
        {
            return Result.Fail(Loc.F(Strings.SavestateNotDeleted, ex.Message));
        }

        return Result.Ok(Strings.SavestateDeleted);
    }

    // BACKUP

    private string NewBackupPath(string name)
    {
        return Path.Combine(BackupPath, name + "_" + DateTime.Now.ToString(BackupDateFormat));
    }

    /// <summary>
    /// Backup nella cartella Backup, dal più recente. Name è il motivo
    /// (es. "Prima_di_Profilo_Mario"), vedi BackupReasonText.
    /// </summary>
    public IReadOnlyList<Snapshot> GetBackups()
    {
        var list = new List<Snapshot>();

        foreach (string dir in Directory.GetDirectories(BackupPath))
        {
            string name = Path.GetFileName(dir);
            int dateStart = name.Length - BackupDateFormat.Length;

            if (dateStart < 2 || name[dateStart - 1] != '_' ||
                !DateTime.TryParseExact(name[dateStart..], BackupDateFormat,
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                continue;

            list.Add(WithBackupInfo(new Snapshot(dir, date, name[..(dateStart - 1)])));
        }

        return list.OrderByDescending(b => b.Date).ToList();
    }

    /// <summary>
    /// Descrizione leggibile del motivo di un backup.
    /// </summary>
    public static string BackupReasonText(string reason)
    {
        if (reason.StartsWith(ActivateBackupPrefix))
            return Loc.F(Strings.ReasonBeforeActivating, ProfileDisplayName(reason[ActivateBackupPrefix.Length..]));

        if (reason.StartsWith(DeletedBackupPrefix))
            return Loc.F(Strings.ReasonProfileDeleted, ProfileDisplayName(reason[DeletedBackupPrefix.Length..]));

        if (reason.StartsWith(SaveBackupPrefix))
            return Loc.F(Strings.ReasonBeforeSaving, ProfileDisplayName(reason[SaveBackupPrefix.Length..]));

        return reason switch
        {
            "Prima_salvataggio" => Strings.ReasonBeforeSavingOld,
            "Prima_creazione" => Strings.ReasonBeforeCreating,
            RestoreBackupName => Strings.ReasonBeforeRestoring,
            _ => reason.Replace('_', ' ')
        };
    }

    /// <summary>
    /// Rimette un backup nella cartella del gioco, dopo un nuovo backup.
    /// Il profilo attivo non cambia.
    /// </summary>
    public Result RestoreBackup(Snapshot backup)
    {
        return Finish("Ripristina backup " + backup.Path,
            RestoreToGame(backup, BackupPath, Strings.BackupRestored));
    }

    private Result RestoreToGame(Snapshot snapshot, string root, string message)
    {
        if (IsGameRunning())
            return Result.Fail(Loc.F(Strings.CloseGameFirst, Game.Name));

        if (!IsInside(snapshot.Path, root) || !Directory.Exists(snapshot.Path))
            return Result.Fail(Loc.F(Strings.FolderNotFound, snapshot.Path));

        // La pulizia dei backup avviene solo in Finish, a ripristino concluso:
        // il backup da ripristinare non può sparire durante la copia.
        // Opzioni condivise: restano quelle che il gioco ha adesso.
        Dictionary<string, byte[]>? preferences = State.SharePreferences ? ReadPreferences() : null;

        // Per un savestate il dettaglio è il suo nome, per un backup la sua descrizione.
        string detail = root == SavestatePath ? snapshot.Name : BackupDescription(snapshot);

        Result replaced = ReplaceGameSaves(snapshot.Path, RestoreBackupName,
            State.ActiveProfile, BackupKind.Restore, detail);

        if (replaced.Success && preferences != null)
            WritePreferences(preferences);

        return replaced.Success ? Result.Ok(message, replaced.BackupPath) : replaced;
    }

    /// <summary>
    /// Imposta quanti backup conservare ed elimina subito quelli in eccesso.
    /// </summary>
    public Result SetBackupsToKeep(int count)
    {
        State.BackupsToKeep = Math.Max(0, count);

        try
        {
            State.Save();
        }
        catch (Exception ex)
        {
            return Result.Fail(Loc.F(Strings.SettingNotSaved, ex.Message));
        }

        int deleted = PruneBackups();

        return Result.Ok(deleted == 0
            ? Strings.SettingSaved
            : Loc.F(Strings.SettingSavedPruned, deleted));
    }

    /// <summary>
    /// Elimina i backup più vecchi oltre il numero da conservare.
    /// Restituisce quanti ne ha eliminati.
    /// </summary>
    private int PruneBackups()
    {
        if (State.BackupsToKeep <= 0)
            return 0;

        int deleted = 0;

        try
        {
            foreach (Snapshot backup in GetBackups().Skip(State.BackupsToKeep))
            {
                try
                {
                    Directory.Delete(backup.Path, true);
                    DeleteBackupInfo(backup.Path);
                    deleted++;
                }
                catch (Exception ex)
                {
                    Log.Write("Backup non eliminato " + backup.Path, ex);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Write("Pulizia dei backup", ex);
        }

        if (deleted > 0)
            Log.Write($"Pulizia: eliminati {deleted} backup vecchi");

        return deleted;
    }

    private static bool IsInside(string path, string root)
    {
        string full = Path.GetFullPath(path);
        string parent = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        return full.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
    }

    // CONTROLLI

    public bool IsGameRunning() => provider.IsGameRunning(Game);

    public static bool IsSteamRunning() => new SteamProvider(Platform.Current).IsLauncherRunning();

    /// <summary>
    /// Nome di una partita: lettere (anche accentate), cifre, spazi, _ e -,
    /// senza spazi all'inizio o alla fine, al massimo 50 caratteri.
    /// </summary>
    public static bool IsValidName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 50 || name != name.Trim())
            return false;

        return name.All(c => char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-');
    }
}
