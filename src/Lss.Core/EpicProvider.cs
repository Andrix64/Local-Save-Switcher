using System.Text.Json;

namespace Lss;

/// <summary>
/// Epic Games Launcher (Windows): giochi installati dai manifest in ProgramData,
/// salvataggi dalla cartella cloud che il catalogo di Epic dà al gioco. Avvio
/// con l'indirizzo com.epicgames.launcher://, processi e stato del cloud.
/// Tutto ciò che è del sistema passa da IPlatform.
/// </summary>
public sealed class EpicProvider(IPlatform platform) : IGameProvider
{
    // Un gioco installato, come lo scrive il launcher in un file .item.
    private sealed record Item(string AppName, string Name, string InstallLocation, string CatalogNamespace,
        string CatalogItemId, string LaunchExecutable);

    // Letti dall'ultimo FindGames, come fa SteamProvider con appinfo.vdf.
    private readonly Dictionary<string, Item> items = [];
    private readonly Dictionary<string, string> cloudFolders = [];
    private readonly HashSet<string> libraries = [];

    public string Id => "epic";

    // Marchio: non si traduce.
    public string DisplayName => "Epic Games";

    /// <summary>
    /// Un account: la cartella dei manifest, se contiene almeno un gioco installato.
    /// </summary>
    public IReadOnlyList<GameAccount> FindAccounts()
    {
        string manifests = ManifestsFolder();

        if (manifests == "" || ReadItems(manifests).Count == 0)
            return [];

        return [new GameAccount("epic", "local", "Epic Games", manifests, null)];
    }

    /// <summary>
    /// Giochi installati, con i salvataggi che il launcher conosce (regola del cloud).
    /// </summary>
    public IReadOnlyList<GameInfo> FindGames(GameAccount account)
    {
        items.Clear();
        cloudFolders.Clear();
        libraries.Clear();

        Dictionary<string, string> catalog = EpicCloudFolder.ReadCatalog(CatalogFile());
        var games = new List<GameInfo>();

        foreach (Item item in ReadItems(account.Folder))
        {
            items[item.AppName] = item;

            if (catalog.TryGetValue(item.CatalogItemId, out string? folder))
                cloudFolders[item.AppName] = folder;

            string library = Path.GetDirectoryName(item.InstallLocation) ?? "";

            if (library != "")
                libraries.Add(library);

            games.Add(new GameInfo(
                "epic",
                item.AppName,
                item.Name,
                Path.GetFileName(item.InstallLocation),
                library,
                item.InstallLocation,
                [],
                SupportState.NoSaves,
                Source: SaveSource.None));
        }

        return SaveLookup.Apply(games, this, account, platform.GetKnownFolders(), AppData.Root);
    }

    public bool CanDetectRunning(GameInfo game) => game.InstallPath != "";

    public bool IsGameRunning(GameInfo game) => game.InstallPath != "" && RunningProcesses(game).Count > 0;

    public DateTime? GameStartTime(GameInfo game)
    {
        var starts = RunningProcesses(game).Select(process => process.Start).OfType<DateTime>().ToList();

        return starts.Count > 0 ? starts.Min() : null;
    }

    public bool CanLaunch(GameInfo game) => items.ContainsKey(game.Id);

    /// <summary>
    /// LaunchExecutable del manifest, nella cartella d'installazione (es. "FEZ.exe").
    /// </summary>
    public string ExecutableOf(GameInfo game) =>
        items.TryGetValue(game.Id, out Item? item) && item.LaunchExecutable != ""
            ? Path.Combine(item.InstallLocation, item.LaunchExecutable)
            : "";

    /// <summary>
    /// Avvia il gioco con l'indirizzo di Epic Games Launcher (che si apre da solo se è chiuso).
    /// </summary>
    public Result Launch(GameInfo game)
    {
        if (!items.TryGetValue(game.Id, out Item? item))
            return Log.Write("Avvio di " + game.Name, Result.Fail(Loc.F(Strings.LaunchFailedVia, game.Name, DisplayName, Strings.EpicItemMissing)));

        try
        {
            platform.OpenUri("com.epicgames.launcher://apps/" + item.CatalogNamespace + "%3A" + item.CatalogItemId
                             + "%3A" + item.AppName + "?action=launch&silent=true");
            Log.Write("Avvio di " + game.Name + " tramite Epic Games Launcher");
            return Result.Ok(Loc.F(Strings.GameLaunching, game.Name));
        }
        catch (Exception ex)
        {
            return Log.Write("Avvio di " + game.Name, Result.Fail(Loc.F(Strings.LaunchFailedVia, game.Name, DisplayName, ex.Message)));
        }
    }

    public bool IsLauncherRunning() => platform.IsProcessRunning("EpicGamesLauncher");

    /// <summary>
    /// Enabled se il catalogo dà al gioco una cartella di salvataggi cloud, altrimenti Unknown.
    /// </summary>
    public CloudState GetCloudState(GameAccount account, GameInfo game) =>
        cloudFolders.ContainsKey(game.Id) ? CloudState.Enabled : CloudState.Unknown;

    /// <summary>
    /// La cartella di salvataggi cloud del gioco, dal catalogo letto dall'ultimo FindGames.
    /// </summary>
    public IReadOnlyList<SaveRule> SaveRules(GameAccount account, GameInfo game) =>
        cloudFolders.TryGetValue(game.Id, out string? folder) && EpicCloudFolder.ToRule(folder) is { } rule
            ? [rule]
            : [];

    public IReadOnlyList<RootOverride> RootOverrides(GameAccount account, GameInfo game) => [];

    public string PrefixOf(GameInfo game) => "";

    /// <summary>
    /// La cartella di Epic in ProgramData e le cartelle delle biblioteche dei giochi.
    /// </summary>
    public IReadOnlyList<string> LauncherFolders(GameAccount account)
    {
        var folders = new List<string>(libraries);
        string programData = platform.GetKnownFolders().ProgramData;

        if (programData != "")
            folders.Add(Path.Combine(programData, "Epic"));

        return folders;
    }

    private IReadOnlyList<(int Id, DateTime? Start)> RunningProcesses(GameInfo game) =>
        game.InstallPath == "" ? [] : platform.FindProcessesUnder(game.InstallPath);

    /// <summary>
    /// Cartella Data di Epic in ProgramData, o "" se ProgramData non si conosce.
    /// </summary>
    private string DataFolder()
    {
        string programData = platform.GetKnownFolders().ProgramData;

        return programData == "" ? "" : Path.Combine(programData, "Epic", "EpicGamesLauncher", "Data");
    }

    private string ManifestsFolder() => DataFolder() is { Length: > 0 } data ? Path.Combine(data, "Manifests") : "";

    private string CatalogFile() => DataFolder() is { Length: > 0 } data
        ? Path.Combine(data, "Catalog", "catcache.bin")
        : "";

    /// <summary>
    /// I giochi installati nei manifest (*.item) di una cartella. Salta quelli
    /// con un'installazione incompleta o senza nome e cartella.
    /// </summary>
    private static List<Item> ReadItems(string folder)
    {
        var result = new List<Item>();

        if (!Directory.Exists(folder))
            return result;

        foreach (string file in Directory.GetFiles(folder, "*.item"))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
                JsonElement root = document.RootElement;

                if (Flag(root, "bIsIncompleteInstall"))
                    continue;

                string appName = Text(root, "AppName");
                string name = Text(root, "DisplayName");
                string location = Path.TrimEndingDirectorySeparator(Text(root, "InstallLocation"));

                if (appName == "" || name == "" || location == "")
                    continue;

                result.Add(new Item(appName, name, location, Text(root, "CatalogNamespace"), Text(root, "CatalogItemId"),
                    Text(root, "LaunchExecutable")));
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                Log.Write("Manifest di Epic non letto: " + file, ex);
            }
        }

        return result;
    }

    // Testo di una proprietà, "" se manca o non è un testo.
    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    // Proprietà vera, false se manca.
    private static bool Flag(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;
}
