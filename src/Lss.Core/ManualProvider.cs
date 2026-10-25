namespace Lss;

/// <summary>
/// I giochi aggiunti a mano (ManualGames): un solo account, "Questo computer".
/// Avvio con l'eseguibile dato; su Linux un .exe non si avvia da qui (serve Wine
/// o Proton, e il launcher lo avvia). Nessuna regola dei salvataggi: passano
/// dalla ricerca o da una cartella scelta.
/// Tutto ciò che è del sistema passa da IPlatform.
/// </summary>
public sealed class ManualProvider(IPlatform platform) : IGameProvider
{
    // Giochi letti dall'ultimo FindGames, per id.
    private readonly Dictionary<string, ManualGame> games = [];

    public string Id => "manual";

    // Nome dell'account, tradotto: è "Questo computer", non un marchio.
    public string DisplayName => Strings.ManualProviderName;

    /// <summary>
    /// L'account dei giochi aggiunti a mano, se ce n'è almeno uno.
    /// </summary>
    public IReadOnlyList<GameAccount> FindAccounts() =>
        ManualGames.ReadAll(AppData.Root).Count > 0
            ? [new GameAccount("manual", "local", DisplayName, ManualGames.AccountFolder(AppData.Root), null)]
            : [];

    /// <summary>
    /// I giochi aggiunti a mano. La cartella d'installazione è quella dell'eseguibile.
    /// </summary>
    public IReadOnlyList<GameInfo> FindGames(GameAccount account)
    {
        games.Clear();

        var found = new List<GameInfo>();

        foreach ((string id, ManualGame game) in ManualGames.ReadAll(AppData.Root))
        {
            games[id] = game;

            found.Add(new GameInfo(
                "manual",
                id,
                game.Name,
                "",
                "",
                game.Exe == "" ? "" : Path.GetDirectoryName(game.Exe) ?? "",
                [],
                SupportState.NoSaves,
                Source: SaveSource.None));
        }

        return SaveLookup.Apply(found, this, account, platform.GetKnownFolders(), AppData.Root);
    }

    public bool CanDetectRunning(GameInfo game) => game.InstallPath != "";

    public bool IsGameRunning(GameInfo game) => game.InstallPath != "" && RunningProcesses(game).Count > 0;

    public DateTime? GameStartTime(GameInfo game)
    {
        var starts = RunningProcesses(game).Select(process => process.Start).OfType<DateTime>().ToList();

        return starts.Count > 0 ? starts.Min() : null;
    }

    public bool CanLaunch(GameInfo game) => games.TryGetValue(game.Id, out ManualGame? found) && found.Exe != "" && !WindowsProgramOnLinux(found.Exe);

    /// <summary>
    /// L'eseguibile dato per il gioco, o "".
    /// </summary>
    public string ExecutableOf(GameInfo game) => games.TryGetValue(game.Id, out ManualGame? found) ? found.Exe : "";

    public Result Launch(GameInfo game)
    {
        if (!CanLaunch(game))
            return Log.Write("Avvio di " + game.Name, Result.Fail(Loc.F(Strings.LaunchFailedVia, game.Name, DisplayName, Strings.ManualCannotStart)));

        ManualGame found = games[game.Id];

        try
        {
            if (!platform.StartProgram(found.Exe, found.Arguments, game.InstallPath))
                return Log.Write("Avvio di " + game.Name, Result.Fail(Loc.F(Strings.LaunchFailedVia, game.Name, DisplayName, Strings.ProgramNotStarted)));

            Log.Write("Avvio di " + game.Name + " dall'eseguibile " + found.Exe);
            return Result.Ok(Loc.F(Strings.GameLaunching, game.Name));
        }
        catch (Exception ex)
        {
            return Log.Write("Avvio di " + game.Name, Result.Fail(Loc.F(Strings.LaunchFailedVia, game.Name, DisplayName, ex.Message)));
        }
    }

    public bool IsLauncherRunning() => false;

    public CloudState GetCloudState(GameAccount account, GameInfo game) => CloudState.Unknown;

    public IReadOnlyList<SaveRule> SaveRules(GameAccount account, GameInfo game) => [];

    public IReadOnlyList<RootOverride> RootOverrides(GameAccount account, GameInfo game) => [];

    /// <summary>
    /// Prefisso Wine o Proton scelto per il gioco, o "".
    /// </summary>
    public string PrefixOf(GameInfo game) => games.TryGetValue(game.Id, out ManualGame? found) ? found.Prefix : "";

    public IReadOnlyList<string> LauncherFolders(GameAccount account) => [];

    // Processi dell'eseguibile del gioco (cartella d'installazione), o nessuno.
    private IReadOnlyList<(int Id, DateTime? Start)> RunningProcesses(GameInfo game) =>
        game.InstallPath == "" ? [] : platform.FindProcessesUnder(game.InstallPath);

    // Un .exe su Linux non si avvia da LSS.
    private static bool WindowsProgramOnLinux(string exe) =>
        OperatingSystem.IsLinux() && exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
}
