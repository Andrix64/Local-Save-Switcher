namespace Lss;

/// <summary>
/// GOG su Windows: giochi installati dal registro (HKLM\SOFTWARE\WOW6432Node\GOG.com\Games),
/// anche con i soli installer offline. GOG non dice dove salva: i salvataggi
/// passano sempre dalla ricerca sul disco (fase M). Avvio diretto dell'eseguibile.
/// Tutto ciò che è del sistema passa da IPlatform.
/// </summary>
public sealed class GogProvider(IPlatform platform) : IGameProvider
{
    private const string GamesKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\GOG.com\Games";

    // Eseguibile di ogni gioco letto dall'ultimo FindGames.
    private readonly Dictionary<string, string> executables = [];

    // Cartelle dei giochi letti dall'ultimo FindGames, per LauncherFolders.
    private readonly HashSet<string> libraries = [];

    public string Id => "gog";

    // Marchio: non si traduce.
    public string DisplayName => "GOG";

    /// <summary>
    /// Un account, se nel registro c'è almeno un gioco installato.
    /// </summary>
    public IReadOnlyList<GameAccount> FindAccounts() =>
        ReadGames().Count > 0 ? [new GameAccount("gog", "local", "GOG", "", null)] : [];

    /// <summary>
    /// Giochi installati nel registro. Il controllo di sicurezza e la ricerca dei
    /// salvataggi sono in SaveLookup, come per gli altri launcher.
    /// </summary>
    public IReadOnlyList<GameInfo> FindGames(GameAccount account)
    {
        executables.Clear();
        libraries.Clear();

        var games = new List<GameInfo>();

        foreach (Installed installed in ReadGames())
        {
            executables[installed.Id] = installed.Executable;

            string library = Path.GetDirectoryName(installed.Folder) ?? "";

            if (library != "")
                libraries.Add(library);

            games.Add(new GameInfo(
                "gog",
                installed.Id,
                installed.Name,
                Path.GetFileName(installed.Folder),
                library,
                installed.Folder,
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

    public bool CanLaunch(GameInfo game) => executables.GetValueOrDefault(game.Id, "") != "";

    /// <summary>
    /// Avvia l'eseguibile del gioco nella sua cartella, senza passare da GOG Galaxy.
    /// </summary>
    public Result Launch(GameInfo game)
    {
        string executable = executables.GetValueOrDefault(game.Id, "");

        if (executable == "")
            return Log.Write("Avvio di " + game.Name, Result.Fail(Loc.F(Strings.LaunchFailedVia, game.Name, DisplayName, Strings.GogExeMissing)));

        try
        {
            if (!platform.StartProgram(executable, "", game.InstallPath))
                return Log.Write("Avvio di " + game.Name, Result.Fail(Loc.F(Strings.LaunchFailedVia, game.Name, DisplayName, Strings.ProgramNotStarted)));

            Log.Write("Avvio di " + game.Name + " dall'eseguibile " + executable);
            return Result.Ok(Loc.F(Strings.GameLaunching, game.Name));
        }
        catch (Exception ex)
        {
            return Log.Write("Avvio di " + game.Name, Result.Fail(Loc.F(Strings.LaunchFailedVia, game.Name, DisplayName, ex.Message)));
        }
    }

    public bool IsLauncherRunning() => platform.IsProcessRunning("GalaxyClient");

    /// <summary>
    /// GOG non dice se il cloud è attivo: Unknown.
    /// </summary>
    public CloudState GetCloudState(GameAccount account, GameInfo game) => CloudState.Unknown;

    /// <summary>
    /// Nessuna regola: GOG non dà le cartelle dei salvataggi (Galaxy le tiene in SQLite).
    /// </summary>
    public IReadOnlyList<SaveRule> SaveRules(GameAccount account, GameInfo game) => [];

    public IReadOnlyList<RootOverride> RootOverrides(GameAccount account, GameInfo game) => [];

    public string PrefixOf(GameInfo game) => "";

    /// <summary>
    /// La cartella di GOG in ProgramData e le cartelle che contengono i giochi.
    /// </summary>
    public IReadOnlyList<string> LauncherFolders(GameAccount account)
    {
        var folders = new List<string>(libraries);
        string programData = platform.GetKnownFolders().ProgramData;

        if (programData != "")
            folders.Add(Path.Combine(programData, "GOG.com"));

        return folders;
    }

    private IReadOnlyList<(int Id, DateTime? Start)> RunningProcesses(GameInfo game) =>
        game.InstallPath == "" ? [] : platform.FindProcessesUnder(game.InstallPath);

    /// <summary>
    /// Giochi nel registro la cui cartella esiste ancora. Il nome è gameName,
    /// o il nome della cartella se manca.
    /// </summary>
    private List<Installed> ReadGames()
    {
        var games = new List<Installed>();

        foreach (string id in platform.RegistrySubKeys(GamesKey))
        {
            string key = GamesKey + @"\" + id;
            string path = Path.TrimEndingDirectorySeparator(platform.ReadRegistryString(key, "path") ?? "");

            if (path == "" || !Directory.Exists(path))
                continue;

            string name = platform.ReadRegistryString(key, "gameName") ?? "";

            games.Add(new Installed(id, name != "" ? name : Path.GetFileName(path), path,
                platform.ReadRegistryString(key, "exe") ?? ""));
        }

        return games;
    }

    // Un gioco installato come lo scrive GOG nel registro.
    private sealed record Installed(string Id, string Name, string Folder, string Executable);
}
