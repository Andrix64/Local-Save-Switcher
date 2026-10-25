namespace Lss;

/// <summary>
/// Steam: account e giochi da userdata e dalle librerie, avvio con steam://,
/// processi e stato di Steam Cloud. Tutto ciò che è del sistema passa da
/// IPlatform.
/// </summary>
public sealed class SteamProvider(IPlatform platform) : IGameProvider
{
    public string Id => "steam";

    // Marchio: non si traduce.
    public string DisplayName => "Steam";

    /// <summary>
    /// Account con giochi gestibili in userdata; se non ce ne sono, quelli con
    /// almeno una cartella di gioco (vedi SaveManager.FindAccountsWithAnyGame).
    /// </summary>
    public IReadOnlyList<GameAccount> FindAccounts()
    {
        string? steam = platform.FindSteamRoots().FirstOrDefault();

        if (steam == null)
            return [];

        string userData = Path.Combine(steam, "userdata");

        if (!Directory.Exists(userData))
            return [];

        IReadOnlyList<GameAccount> accounts = SaveManager.FindAccountsWithGames(userData);

        return accounts.Count > 0 ? accounts : SaveManager.FindAccountsWithAnyGame(userData);
    }

    /// <summary>
    /// Giochi di un account, dalle librerie di Steam.
    /// </summary>
    public IReadOnlyList<GameInfo> FindGames(GameAccount account)
    {
        string? steam = platform.FindSteamRoots().FirstOrDefault();

        if (steam == null)
            return [];

        return SteamLibrary.FindGames(steam, account.Folder);
    }

    public bool CanDetectRunning(GameInfo game) => true;

    public bool IsGameRunning(GameInfo game) => platform.IsGameRunning(game);

    public DateTime? GameStartTime(GameInfo game) => platform.GameStartTime(game);

    public bool CanLaunch(GameInfo game) => true;

    /// <summary>
    /// Avvia il gioco tramite Steam (che si apre da solo se è chiuso).
    /// </summary>
    public Result Launch(GameInfo game)
    {
        try
        {
            platform.OpenSteamUri("steam://rungameid/" + game.Id);
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
    /// Stato di Steam Cloud per il gioco, letto da
    /// account\7\remote\sharedconfig.vdf. Senza impostazione vale Enabled.
    /// </summary>
    public CloudState GetCloudState(GameAccount account, GameInfo game)
    {
        try
        {
            string config = Path.Combine(account.Folder, "7", "remote", "sharedconfig.vdf");

            if (!File.Exists(config))
                return CloudState.Enabled;

            string? value = Vdf.Find(Vdf.Parse(File.ReadAllText(config)), "apps", game.Id, "cloudenabled");

            return value == "0" ? CloudState.Disabled : CloudState.Enabled;
        }
        catch
        {
            return CloudState.Unknown;
        }
    }
}
