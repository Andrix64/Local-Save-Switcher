using System.Globalization;
using System.Text.RegularExpressions;

namespace Lss;

/// <summary>
/// Steam: account e giochi da userdata e dalle librerie, avvio con steam://,
/// processi e stato di Steam Cloud. Tutto ciò che è del sistema passa da
/// IPlatform.
/// </summary>
public sealed class SteamProvider(IPlatform platform) : IGameProvider
{
    public string Id => "steam";

    // Regole lette dall'ultimo FindGames: appinfo.vdf si legge una volta sola.
    private SteamAppInfo? appInfo;

    // ID dei collegamenti (giochi non Steam) letti dall'ultimo FindGames, con il loro eseguibile.
    private readonly Dictionary<string, string> shortcuts = [];

    // Marchio: non si traduce.
    public string DisplayName => "Steam";

    /// <summary>
    /// Cartella principale di Steam, o null se Steam non è installato.
    /// </summary>
    public string? FindRoot() => platform.FindSteamRoots().FirstOrDefault();

    /// <summary>
    /// Cartella userdata di una installazione di Steam.
    /// </summary>
    internal static string UserDataFolder(string steamRoot) => Path.Combine(steamRoot, "userdata");

    /// <summary>
    /// Account con giochi gestibili in userdata; se non ce ne sono, quelli con
    /// almeno una cartella di gioco (vedi FindAccountsWithAnyGame).
    /// </summary>
    public IReadOnlyList<GameAccount> FindAccounts()
    {
        string? steam = FindRoot();

        if (steam == null)
            return [];

        string userData = UserDataFolder(steam);

        if (!Directory.Exists(userData))
            return [];

        IReadOnlyList<GameAccount> accounts = FindAccountsWithGames(userData);

        return accounts.Count > 0 ? accounts : FindAccountsWithAnyGame(userData);
    }

    /// <summary>
    /// Giochi di un account, dalle librerie di Steam.
    /// </summary>
    public IReadOnlyList<GameInfo> FindGames(GameAccount account)
    {
        string? steam = FindRoot();

        if (steam == null)
            return [];

        var games = SteamLibrary.FindGames(steam, account.Folder).ToList();

        appInfo = SteamAppInfo.Load(steam, games.Select(game => game.Id).ToHashSet());

        // Giochi non Steam aggiunti a Steam: stesso gruppo, avvio con il loro ID di collegamento.
        shortcuts.Clear();

        foreach (SteamShortcuts.Shortcut shortcut in SteamShortcuts.Read(account.Folder))
        {
            string id = shortcut.AppId.ToString(CultureInfo.InvariantCulture);

            if (games.Any(game => game.Id == id))
                continue;

            shortcuts[id] = shortcut.Exe;
            games.Add(new GameInfo("steam", id, shortcut.Name, "", "", shortcut.StartDir, [],
                SupportState.NoSaves, IconPath: shortcut.Icon, Source: SaveSource.None));
        }

        return SaveLookup.Apply(games, this, account, platform.GetKnownFolders(), AppData.Root);
    }

    /// <summary>
    /// Prefisso Proton del gioco, nella libreria del gioco o in un'altra.
    /// </summary>
    public string PrefixOf(GameInfo game) =>
        FindRoot() is { } steam ? SteamLibrary.FindProtonPrefix(steam, game) : "";

    /// <summary>
    /// Cartelle di Steam che LSS non deve mai sostituire per intero: la
    /// installazione, le librerie e le loro cartelle di Steam.
    /// </summary>
    public IReadOnlyList<string> LauncherFolders(GameAccount account)
    {
        if (FindRoot() is not { } steamRoot)
            return [];

        var folders = new List<string>
        {
            steamRoot,
            UserDataFolder(steamRoot),
            Path.Combine(steamRoot, "appcache")
        };

        foreach (string library in SteamLibrary.LibraryFolders(steamRoot))
        {
            folders.Add(library);
            folders.Add(Path.Combine(library, "steamapps"));
            folders.Add(Path.Combine(library, "steamapps", "common"));
            folders.Add(Path.Combine(library, "steamapps", "compatdata"));
        }

        return folders;
    }

    /// <summary>
    /// Regole Auto-Cloud del gioco, da appinfo.vdf letto dall'ultimo FindGames.
    /// </summary>
    public IReadOnlyList<SaveRule> SaveRules(GameAccount account, GameInfo game) =>
        appInfo?.RulesOf(game.Id) ?? [];

    /// <summary>
    /// Sostituzioni di radici del gioco, da appinfo.vdf letto dall'ultimo FindGames.
    /// </summary>
    public IReadOnlyList<RootOverride> RootOverrides(GameAccount account, GameInfo game) =>
        appInfo?.OverridesOf(game.Id) ?? [];

    public bool CanDetectRunning(GameInfo game) => true;

    public bool IsGameRunning(GameInfo game) => platform.IsGameRunning(game);

    public DateTime? GameStartTime(GameInfo game) => platform.GameStartTime(game);

    public bool CanLaunch(GameInfo game) => true;

    /// <summary>
    /// L'eseguibile di un collegamento non Steam; "" per i giochi Steam (hanno le loro immagini).
    /// </summary>
    public string ExecutableOf(GameInfo game) => shortcuts.GetValueOrDefault(game.Id, "");

    /// <summary>
    /// Avvia il gioco tramite Steam (che si apre da solo se è chiuso).
    /// </summary>
    public Result Launch(GameInfo game)
    {
        try
        {
            string uri = shortcuts.ContainsKey(game.Id)
                ? SteamShortcuts.LaunchUri(uint.Parse(game.Id, CultureInfo.InvariantCulture))
                : "steam://rungameid/" + game.Id;

            platform.OpenSteamUri(uri);
            Log.Write("Avvio di " + game.Name + " tramite Steam");
            return Result.Ok(Loc.F(Strings.GameLaunching, game.Name));
        }
        catch (Exception ex)
        {
            return Log.Write("Avvio di " + game.Name, Result.Fail(Loc.F(Strings.LaunchFailed, game.Name, ex.Message)));
        }
    }

    public bool IsLauncherRunning() => platform.IsSteamRunning();

    /// <summary>
    /// Stato di Steam Cloud per il gioco. Disabled se è spento in
    /// account\7\remote\sharedconfig.vdf, o se il gioco non usa Steam Cloud
    /// (niente remotecache.vdf nella sua cartella e nessuna regola Auto-Cloud).
    /// </summary>
    public CloudState GetCloudState(GameAccount account, GameInfo game)
    {
        try
        {
            string config = Path.Combine(account.Folder, "7", "remote", "sharedconfig.vdf");

            if (File.Exists(config) &&
                Vdf.Find(Vdf.Parse(File.ReadAllText(config)), "apps", game.Id, "cloudenabled") == "0")
                return CloudState.Disabled;

            bool usesCloud = File.Exists(Path.Combine(account.Folder, game.Id, "remotecache.vdf")) ||
                             SaveRules(account, game).Count > 0;

            return usesCloud ? CloudState.Enabled : CloudState.Disabled;
        }
        catch
        {
            return CloudState.Unknown;
        }
    }

    /// <summary>
    /// Account in userdata che hanno la cartella di Control, dal più usato di
    /// recente. Basta la cartella 870780: dopo aver attivato un profilo vuoto o
    /// creato a mano può mancare la sottocartella remote, e l'account non deve
    /// sparire per questo.
    /// </summary>
    internal static IReadOnlyList<GameAccount> FindAccounts(string userData, string appId)
    {
        var accounts = new List<GameAccount>();

        foreach (string dir in Directory.GetDirectories(userData))
        {
            string game = Path.Combine(dir, appId);

            if (!Directory.Exists(game))
                continue;

            DateTime? lastSaved = null;

            try
            {
                foreach (string file in Directory.EnumerateFiles(game, "*", SearchOption.AllDirectories))
                {
                    // Il file di Steam Cloud non è un salvataggio.
                    if (Path.GetFileName(file).Equals("remotecache.vdf", StringComparison.OrdinalIgnoreCase))
                        continue;

                    DateTime time = File.GetLastWriteTime(file);

                    if (lastSaved == null || time > lastSaved)
                        lastSaved = time;
                }
            }
            catch
            {
                // Cartella non leggibile: nessuna data.
            }

            accounts.Add(new GameAccount("steam", Path.GetFileName(dir), ReadPersonaName(dir), dir, lastSaved));
        }

        return accounts.OrderByDescending(a => a.LastSaved ?? DateTime.MinValue).ToList();
    }

    /// <summary>
    /// Account in userdata che hanno almeno un gioco con salvataggi in
    /// userdata (SupportState.Supported), dal più usato di recente.
    /// </summary>
    internal static IReadOnlyList<GameAccount> FindAccountsWithGames(string userData)
    {
        var accounts = new List<GameAccount>();

        foreach (string dir in Directory.GetDirectories(userData))
        {
            if (!SaveManager.IsNumeric(Path.GetFileName(dir)))
                continue;

            List<string> games = Directory.GetDirectories(dir)
                .Where(game => SaveManager.IsNumeric(Path.GetFileName(game)) &&
                               !SteamLibrary.SystemApps.Contains(Path.GetFileName(game)))
                .Where(game => SteamLibrary.GetSupport(game) == SupportState.Supported)
                .ToList();

            if (games.Count == 0)
                continue;

            DateTime? lastSaved = null;

            try
            {
                foreach (string file in games.SelectMany(SaveFilesOf))
                {
                    DateTime time = File.GetLastWriteTime(file);

                    if (lastSaved == null || time > lastSaved)
                        lastSaved = time;
                }
            }
            catch
            {
                // Cartella non leggibile: nessuna data.
            }

            accounts.Add(new GameAccount("steam", Path.GetFileName(dir), ReadPersonaName(dir), dir, lastSaved));
        }

        return accounts.OrderByDescending(a => a.LastSaved ?? DateTime.MinValue).ToList();
    }

    /// <summary>
    /// Account in userdata con almeno la cartella di un gioco, anche se nessuno
    /// è gestibile (per esempio con i salvataggi nel prefisso di Proton): così
    /// l'app si apre lo stesso e mostra i giochi con il motivo.
    /// </summary>
    internal static IReadOnlyList<GameAccount> FindAccountsWithAnyGame(string userData)
    {
        var accounts = new List<GameAccount>();

        foreach (string dir in Directory.GetDirectories(userData))
        {
            if (!SaveManager.IsNumeric(Path.GetFileName(dir)))
                continue;

            bool hasGame = Directory.GetDirectories(dir)
                .Any(game => SaveManager.IsNumeric(Path.GetFileName(game)) &&
                             !SteamLibrary.SystemApps.Contains(Path.GetFileName(game)));

            if (hasGame)
                accounts.Add(new GameAccount("steam", Path.GetFileName(dir), ReadPersonaName(dir), dir, null));
        }

        return accounts;
    }

    /// <summary>
    /// I file di salvataggio di una cartella di gioco: tutti tranne
    /// remotecache.vdf al primo livello (il file di Steam Cloud).
    /// </summary>
    private static IEnumerable<string> SaveFilesOf(string game)
    {
        string cache = Path.Combine(game, "remotecache.vdf");

        return Directory.EnumerateFiles(game, "*", SearchOption.AllDirectories)
            .Where(file => !file.Equals(cache, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Nome Steam dell'account, letto da config\localconfig.vdf.
    /// </summary>
    private static string ReadPersonaName(string accountFolder)
    {
        try
        {
            string config = Path.Combine(accountFolder, "config", "localconfig.vdf");

            if (!File.Exists(config))
                return "";

            Match match = Regex.Match(File.ReadAllText(config), "\"PersonaName\"\\s+\"([^\"]*)\"");
            return match.Success ? match.Groups[1].Value : "";
        }
        catch
        {
            return "";
        }
    }
}
