using System.Text.Json;

namespace Lss;

/// <summary>
/// Heroic Games Launcher (Linux, Steam Deck e Windows): i giochi Epic (Legendary),
/// GOG, Amazon (Nile) e quelli aggiunti a mano (sideload) dalla sua cartella di
/// configurazione. Prefissi Wine e Proton da GamesConfig; salvataggi Epic dalla
/// cartella cloud del catalogo, salvataggi GOG dalle posizioni gogSaves.
/// Formati non verificati su un PC con Heroic (vedi PROGRESS.md, fase O).
/// Avvio da LSS non attivo: l'indirizzo heroic:// non è confermato.
/// </summary>
public sealed class HeroicProvider(IPlatform platform) : IGameProvider
{
    // Un gioco di Heroic con quello che serve per gestirlo.
    private sealed record Game(string Runner, string AppName, string Title, string Folder, string Prefix,
        IReadOnlyList<SaveRule> Rules, CloudState Cloud);

    // Letti dall'ultimo FindGames.
    private readonly Dictionary<string, Game> games = [];
    private readonly HashSet<string> launcherFolders = [];

    public string Id => "heroic";

    // Marchio: non si traduce.
    public string DisplayName => "Heroic";

    /// <summary>
    /// Un account per l'installazione di Heroic, se ha almeno un file dei giochi installati.
    /// </summary>
    public IReadOnlyList<GameAccount> FindAccounts()
    {
        string config = ConfigFolder();

        if (config == "" || !InstalledFiles(config).Any(File.Exists))
            return [];

        return [new GameAccount("heroic", "local", "Heroic", config, null)];
    }

    /// <summary>
    /// Giochi installati, con prefisso e regole dei salvataggi di ciascuno.
    /// </summary>
    public IReadOnlyList<GameInfo> FindGames(GameAccount account)
    {
        games.Clear();
        launcherFolders.Clear();

        string config = account.Folder;
        Dictionary<string, string> gogTitles = ReadGogTitles(config);
        var found = new List<GameInfo>();

        foreach ((string runner, string appName, string title, string install) in ReadInstalled(config))
        {
            string id = runner + "_" + appName;

            if (games.ContainsKey(id) || !Directory.Exists(install))
                continue;

            JsonElement settings = ReadGameConfig(config, appName);
            string prefix = PrefixFolder(settings);
            (IReadOnlyList<SaveRule> rules, CloudState cloud) = RulesOf(config, runner, appName, settings);

            string name = title != "" ? title : gogTitles.GetValueOrDefault(appName, "");
            games[id] = new Game(runner, appName, name, install, prefix, rules, cloud);

            string library = Path.GetDirectoryName(install) ?? "";

            if (library != "")
                launcherFolders.Add(library);

            if (Text(settings, "winePrefix") is { Length: > 0 } winePrefix)
                launcherFolders.Add(Path.GetDirectoryName(winePrefix) ?? "");

            found.Add(new GameInfo(
                "heroic",
                id,
                name != "" ? name : Path.GetFileName(install),
                Path.GetFileName(install),
                library,
                install,
                [],
                SupportState.NoSaves,
                Source: SaveSource.None));
        }

        launcherFolders.Add(config);

        return SaveLookup.Apply(found, this, account, platform.GetKnownFolders(), AppData.Root);
    }

    public bool CanDetectRunning(GameInfo game) => game.InstallPath != "";

    public bool IsGameRunning(GameInfo game) => game.InstallPath != "" && RunningProcesses(game).Count > 0;

    public DateTime? GameStartTime(GameInfo game)
    {
        var starts = RunningProcesses(game).Select(process => process.Start).OfType<DateTime>().ToList();

        return starts.Count > 0 ? starts.Min() : null;
    }

    /// <summary>
    /// Non ancora: l'indirizzo heroic://launch/... non è confermato. Il gioco si avvia da Heroic.
    /// </summary>
    public bool CanLaunch(GameInfo game) => false;

    public Result Launch(GameInfo game) =>
        Log.Write("Avvio di " + game.Name, Result.Fail(Loc.F(Strings.LaunchFailedVia, game.Name, DisplayName, Strings.StartFromHeroic)));

    public bool IsLauncherRunning() => platform.IsProcessRunning("heroic");

    public CloudState GetCloudState(GameAccount account, GameInfo game) =>
        games.TryGetValue(game.Id, out Game? found) ? found.Cloud : CloudState.Unknown;

    public IReadOnlyList<SaveRule> SaveRules(GameAccount account, GameInfo game) =>
        games.TryGetValue(game.Id, out Game? found) ? found.Rules : [];

    public IReadOnlyList<RootOverride> RootOverrides(GameAccount account, GameInfo game) => [];

    /// <summary>
    /// Cartella che contiene drive_c del prefisso Proton (pfx) o Wine del gioco, o "".
    /// </summary>
    public string PrefixOf(GameInfo game) => games.TryGetValue(game.Id, out Game? found) ? found.Prefix : "";

    /// <summary>
    /// La cartella di configurazione di Heroic, le cartelle dei prefissi e quelle dei giochi.
    /// </summary>
    public IReadOnlyList<string> LauncherFolders(GameAccount account)
    {
        var folders = launcherFolders.Where(folder => folder != "").ToList();

        return folders;
    }

    private IReadOnlyList<(int Id, DateTime? Start)> RunningProcesses(GameInfo game) =>
        game.InstallPath == "" ? [] : platform.FindProcessesUnder(game.InstallPath);

    /// <summary>
    /// Prima cartella di configurazione che esiste: Linux, Flatpak, Windows.
    /// </summary>
    private string ConfigFolder()
    {
        KnownFolders folders = platform.GetKnownFolders();
        var candidates = new List<string>();

        if (folders.Home != "")
        {
            candidates.Add(Path.Combine(folders.Home, ".config", "heroic"));
            candidates.Add(Path.Combine(folders.Home, ".var", "app", "com.heroicgameslauncher.hgl", "config", "heroic"));
        }

        if (folders.AppData != "")
            candidates.Add(Path.Combine(folders.AppData, "heroic"));

        return candidates.FirstOrDefault(Directory.Exists) ?? "";
    }

    // File dei giochi installati per ogni runner.
    private static IEnumerable<string> InstalledFiles(string config) =>
    [
        Path.Combine(config, "legendaryConfig", "legendary", "installed.json"),
        Path.Combine(config, "gog_store", "installed.json"),
        Path.Combine(config, "nile_config", "nile", "installed.json"),
        Path.Combine(config, "sideload_apps", "library.json"),
    ];

    /// <summary>
    /// Giochi installati (runner, appName, titolo, cartella) dai file di ogni runner.
    /// </summary>
    private static List<(string Runner, string AppName, string Title, string Install)> ReadInstalled(string config)
    {
        var result = new List<(string, string, string, string)>();

        // Legendary (Epic) e Nile (Amazon): oggetto con una voce per gioco.
        foreach ((string runner, string file) in new[]
                 {
                     ("legendary", Path.Combine(config, "legendaryConfig", "legendary", "installed.json")),
                     ("nile", Path.Combine(config, "nile_config", "nile", "installed.json")),
                 })
        {
            foreach (JsonElement entry in Entries(file, "", ObjectValues))
                Add(result, runner, entry);
        }

        // GOG: oggetto con l'elenco "installed".
        foreach (JsonElement entry in Entries(Path.Combine(config, "gog_store", "installed.json"), "installed", ArrayValues))
            Add(result, "gog", entry);

        // Giochi aggiunti a mano in Heroic: elenco nel file library.json.
        foreach (JsonElement entry in Entries(Path.Combine(config, "sideload_apps", "library.json"), "games", ArrayValues))
            Add(result, "sideload", entry);

        return result;
    }

    // Aggiunge un gioco letto da un file di un runner, se ha app_name e cartella.
    private static void Add(List<(string, string, string, string)> result, string runner, JsonElement entry)
    {
        string appName = Text(entry, "app_name") is { Length: > 0 } name ? name : Text(entry, "appName");
        string install = Text(entry, "install_path") is { Length: > 0 } path ? path : InstallPathOf(entry);

        if (appName == "" || install == "")
            return;

        result.Add((runner, appName, Text(entry, "title"), Path.TrimEndingDirectorySeparator(install)));
    }

    // Cartella di installazione di una voce sideload (install.install_path), o "".
    private static string InstallPathOf(JsonElement entry) =>
        entry.TryGetProperty("install", out JsonElement install) && install.ValueKind == JsonValueKind.Object
            ? Text(install, "install_path")
            : "";

    // Titoli dei giochi GOG da store_cache/gog_library.json (app_name → title), se ci sono.
    private static Dictionary<string, string> ReadGogTitles(string config)
    {
        var titles = new Dictionary<string, string>();

        foreach (JsonElement entry in Entries(Path.Combine(config, "store_cache", "gog_library.json"), "games", ArrayValues))
        {
            string appName = Text(entry, "app_name");

            if (appName != "" && Text(entry, "title") is { Length: > 0 } title)
                titles[appName] = title;
        }

        return titles;
    }

    /// <summary>
    /// Impostazioni di un gioco: GamesConfig/appName.json, la voce con il nome del gioco.
    /// Vuoto se il file manca o è rotto.
    /// </summary>
    private static JsonElement ReadGameConfig(string config, string appName)
    {
        string file = Path.Combine(config, "GamesConfig", appName + ".json");

        using JsonDocument? document = Parse(file);

        if (document is null || !document.RootElement.TryGetProperty(appName, out JsonElement settings))
            return default;

        return settings.Clone();
    }

    private static string PrefixFolder(JsonElement settings)
    {
        string wine = Text(settings, "winePrefix");

        if (wine == "")
            return "";

        if (Directory.Exists(Path.Combine(wine, "pfx", "drive_c")))
            return Path.Combine(wine, "pfx");

        return Directory.Exists(Path.Combine(wine, "drive_c")) ? wine : "";
    }

    // Regole dei salvataggi e stato del cloud: Epic dal metadato Legendary, GOG da gogSaves.
    private static (IReadOnlyList<SaveRule> Rules, CloudState Cloud) RulesOf(string config, string runner,
        string appName, JsonElement settings)
    {
        if (runner == "legendary")
        {
            string file = Path.Combine(config, "legendaryConfig", "legendary", "metadata", appName + ".json");
            using JsonDocument? metadata = Parse(file);

            if (metadata is not null
                && metadata.RootElement.TryGetProperty("metadata", out JsonElement meta)
                && meta.TryGetProperty("customAttributes", out JsonElement attributes)
                && attributes.TryGetProperty("CloudSaveFolder", out JsonElement folder)
                && Text(folder, "value") is { Length: > 0 } value
                && EpicCloudFolder.ToRule(value) is { } rule)
            {
                return ([rule], CloudState.Enabled);
            }

            return ([], CloudState.Unknown);
        }

        if (runner == "gog" && settings.ValueKind == JsonValueKind.Object
            && settings.TryGetProperty("gogSaves", out JsonElement saves) && saves.ValueKind == JsonValueKind.Array)
        {
            var rules = new List<SaveRule>();

            foreach (JsonElement save in saves.EnumerateArray())
            {
                string location = Text(save, "location");

                if (location != "")
                    rules.Add(new SaveRule("Absolute", location, "*", true, []));
            }

            return (rules, CloudState.Unknown);
        }

        return ([], CloudState.Unknown);
    }

    // Elementi di una lista: il file è un array, o un oggetto con la lista sotto "key".
    private static IEnumerable<JsonElement> ArrayValues(JsonElement root, string key)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray();

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(key, out JsonElement list)
            && list.ValueKind == JsonValueKind.Array)
            return list.EnumerateArray();

        return [];
    }

    // Voci di un oggetto: ogni valore è un gioco, la chiave è il suo app name.
    private static IEnumerable<JsonElement> ObjectValues(JsonElement root, string key) =>
        root.ValueKind == JsonValueKind.Object ? root.EnumerateObject().Select(property => property.Value) : [];

    private delegate IEnumerable<JsonElement> Values(JsonElement root, string key);

    private static IEnumerable<JsonElement> Entries(string file, string key, Values values)
    {
        using JsonDocument? document = Parse(file);

        if (document is null)
            return [];

        // Clone prima della chiusura del documento.
        return values(document.RootElement, key).Select(element => element.Clone()).ToList();
    }

    /// <summary>
    /// Il file JSON, o null se manca o non si legge (l'errore va nel log).
    /// </summary>
    private static JsonDocument? Parse(string file)
    {
        if (!File.Exists(file))
            return null;

        try
        {
            return JsonDocument.Parse(File.ReadAllText(file));
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Log.Write("File di Heroic non letto: " + file, ex);
            return null;
        }
    }

    // Testo di una proprietà, "" se manca o non è un testo.
    private static string Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
}
